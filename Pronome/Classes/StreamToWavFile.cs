﻿using System;
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

        public StreamToWavFile(MixingSampleProvider mixer)
        {
            _mixer = mixer;
            WaveFormat = mixer.WaveFormat;
            //_writer = new WaveFileWriter("test.wav", WaveFormat);
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
                    cycle /= met.TempoChangeRatio;

                    met.PerformTempoChanges();
                
                    met.TempoChangeCued = false;
                }

                // check if a dynamic beat change has occured. If so, pass on the current cycle number.

                // the new sound sources are instantiated on the main thread and pulled up to that cycle number.
                // the main thread then signals this thread and then the sources are topped off 
                // in the audio thread and added to the mixer.
                if (met.NeedsToChangeLayer == true)
                {
                    met.LayerChangeCycle = cycle;
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

                        foreach (var src in real.GetAllSources())
                        {
                            src.Layer = real;
                        }

                        // put in the new sources.
                        met.AddSourcesFromLayer(real);
                    }

                    met.LayersToChange.Clear();

                    met.NeedsToChangeLayer = false;
                }

                result = _mixer.Read(buffer, offset, count);

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
