using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;

namespace Pronome
{
    /**<summary>Wraps the mixer allowing playback to be written to a wav file.</summary>*/
    public class StreamToWavFile : ISampleProvider, IDisposable
    {
        protected MixingSampleProvider _mixer;

        protected WaveFileWriter _writer;

        public bool IsRecording = false;

        public WaveFormat WaveFormat { get; private set; }

        protected PitchStream CountOffStream;

        private long _countOffLength;
        /// <summary>
        /// number of sample in which to play the countoff
        /// </summary>
        public long CountoffLength
        {
            get => _countOffLength;
            set
            {
                if (CountOffStream == null)
                {
                    CountOffStream = new PitchStream(InternalSource.GetDefault());
                    CountOffStream.BeatCollection = new SourceBeatCollection(new double[] { 1 }, CountOffStream);
                    CountOffStream.AddFrequency("A4", new BeatCell());
                }

                _countOffLength = value;
            }
            
        }
        /// <summary>
        /// number of samples added to front to align the countoff
        /// </summary>
        public int CountoffLeadIn;

        public StreamToWavFile(MixingSampleProvider mixer)
        {
            _mixer = mixer;
            WaveFormat = mixer.WaveFormat;
        }

        public void InitRecording(string fileName)
        {
            if (!IsRecording)
            {
                if (fileName.Substring(fileName.Length - 4).ToLower() != ".wav") // append wav extension
                    fileName += ".wav";
                _writer = new WaveFileWriter(fileName, WaveFormat);
                IsRecording = true;
            }
        }

        public void Stop()
        {
            if (IsRecording)
            {
                _writer?.Dispose();
                IsRecording = false;
            }
            cycle = 0;
        }

        /// <summary>
        /// True if the player has been primed. Occurs only at application startup.
        /// </summary>
        public bool IsInitialized = false;

        double cycle = 0;

        public int Read(float[] buffer, int offset, int count)
        {
            if (!IsInitialized)
            {
                Metronome.GetInstance().Player.Stop();
                IsInitialized = true;
                return 0;
            }

            int result = 0;
            try
            {
                var met = Metronome.GetInstance();
                // perform tempo changes here
                if (met.TempoChangeCued)
                {
                    // compensate the cycle
                    cycle *= met.TempoChangeRatio;

                    met.PerformTempoChanges();
                
                    met.TempoChangeCued = false;
                }

                // check if a dynamic beat change has occured. If so, pass on the current cycle number.

                // the new sound sources are instantiated on the main thread and pulled up to that cycle number.
                // the main thread then signals this thread and then the sources are topped off 
                // in the audio thread and added to the mixer.
                if (met.NeedsToChangeLayer == true)
                {
                    met.LayerChangeCycle = cycle + 1;
                    met.NeedsToChangeLayer = false;
                    met.LayerChangeTurnstile.Set();
                }
                else if (met.NeedsToChangeLayer == null)
                {
                    // top off the fast forward
                    double cycleDiff = cycle - met.LayerChangeCycle;
                    //double totalSamples = cycleDiff * count;

                    met.FastForwardChangedLayers(cycleDiff);

                    foreach (KeyValuePair<int, Layer> pair in met.LayersToChange)
                    {
                        Layer copy = pair.Value;
                        Layer real = met.Layers[pair.Key];

                        // remove old sources
                        foreach (IStreamProvider src in real.GetAllSources())
                        {
                            met.RemoveAudioSource(src);
                            src.Dispose();
                        }

                        // transfer sources to real layer
                        real.AudioSources = copy.AudioSources;
                        real.BaseAudioSource = copy.BaseAudioSource;
                        real.BasePitchSource = copy.BasePitchSource;
                        real.BaseSourceName = copy.BaseSourceName;
                        real.HasHiHatClosed = copy.HasHiHatClosed;
                        real.HasHiHatOpen = copy.HasHiHatOpen;
                        real.Beat = copy.Beat;
                        real.IsPitch = copy.IsPitch;
                        real.Volume = copy.Volume;
                        real.Pan = copy.Pan;

                        foreach (var src in real.GetAllSources())
                        {
                            src.Layer = real;
                        }

                        // needed to transfer volume setting to new sources
                        real.Volume = real.Volume;

                        // put in the new sources.
                        met.AddSourcesFromLayer(real);
                        copy.AudioSources = null;
                        copy.BaseAudioSource = null;
                        copy.BasePitchSource = null;
                        copy.Beat = null;
                        met.Layers.Remove(copy);
                    }

                    met.LayersToChange.Clear();

                    met.NeedsToChangeLayer = false;

                    met.TriggerAfterBeatParsed();
                }

                // insert count-off here
                if (CountoffLength > 0)
                {
                    if (CountoffLeadIn > 0)
                    {
                        if (count > CountoffLeadIn)
                        {
                            count -= CountoffLeadIn;

                            for (int i=0; i < CountoffLeadIn; i++)
                            {
                                buffer[i] = 0;
                            }

                            //Array.Copy(new float[CountoffLeadIn], buffer, offset);
                            result += CountoffLeadIn;
                            offset += CountoffLeadIn;
                            CountoffLength -= count;
                            CountoffLeadIn = 0;
                        }
                        else
                        {
                            CountoffLeadIn -= count;
                            for (int i = 0; i < count; i++)
                            {
                                buffer[i] = 0;
                            }
                            //Array.Copy(new float[count], buffer, offset);
                            result = count;
                            count = 0;
                        }
                    }
                    else
                    {
                        CountoffLength -= count;
                    }

                    result += CountOffStream.Read(buffer, offset, count);
                }
                else
                {
                    result = _mixer.Read(buffer, offset, count);
                }

                if (count > 0 && IsRecording)
                {
                    //write samples to file
                    _writer.WriteSamples(buffer, offset, count);
                }
            }
            catch (NullReferenceException) { }

            if (count == 0)
            {
                Dispose();
            }

            cycle++;

            return result;
        }

        public void Dispose()
        {
            IsRecording = false;
            _writer?.Dispose();
        }
    }
}
