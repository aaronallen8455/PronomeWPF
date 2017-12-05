using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using NAudio.Wave;

namespace Pronome
{
    /**<summary>An audio stream for pitch 'beeps'.</summary>*/
    public class PitchStream : ISampleProvider, IStreamProvider
    {
        /**<summary>The WaveFormat object for this stream.</summary>*/
        private readonly WaveFormat waveFormat;

        /**<summary>The BeatCollection object, contains enumerator for byte interval values.</summary>*/
        public SourceBeatCollection BeatCollection { get; set; }

        /**<summary>Test for whether this is a pitch source.</summary>*/
        public bool IsPitch { get { return true; } }

        protected IEnumerator<float> SinWave;

        // Const Math
        private const double TwoPi = 2 * Math.PI;

        /// <summary>
        /// Determines how fast the notes decay in seconds
        /// </summary>
        public static double DecayLength
        {
            get => _decayLength;
            set
            {
                _decayLength = value;
                // queue the new gain step value
                foreach (Layer layer in Metronome.GetInstance().Layers.Where(x => x.IsPitch))
                {
                    layer.Volume = layer.Volume;
                }
            }
        }
        protected static double _decayLength = .04;

        /**<summary>The number of bytes/Second for this audio stream.</summary>*/
        public int BytesPerSec { get; set; }

        /**<summary>The layer that this audiosource is used in.</summary>*/
        public Layer Layer { get; set; }

        public double SampleRemainder { get; set; }

        /**<summary>Used in sine wave generation.</summary>*/
        private float nSample;

        public ISoundSource SoundSource { get; set; }

        /**<summary>Constructor</summary>
         * <param name="channel">Number of channels</param>
         * <param name="sampleRate">Samples per second</param>
         */
        public PitchStream(ISoundSource source, int sampleRate = 44100, int channel = 2)
        {
            SoundSource = source;
            waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channel);
            //waveFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, sampleRate, channel, 176400, 4, 16);
            // Default
            Frequency = 0;
            //Frequency = BaseFrequency = 440.0;
            Pan = 0;
            BytesPerSec = waveFormat.AverageBytesPerSecond / 8;
            freqEnum = Frequencies.Values.GetEnumerator();

            // set audible/silent interval if already exists
            if (Metronome.GetInstance().IsSilentInterval)
                SetSilentInterval(Metronome.GetInstance().AudibleInterval, Metronome.GetInstance().SilentInterval);

            gainStep = 1 / (WaveFormat.SampleRate / DecayLength);
        }

        /**<summary>Add a frequency to the frequency enumerator que.</summary>
         * <param name="cell">The cell that uses the pitch.</param>
         * <param name="symbol">The pitch symbol. ex. A4</param>
         */
        public void AddFrequency(string symbol, BeatCell cell)
        {
            Frequencies.Add(cell, ConvertFromSymbol(symbol));
            freqEnum = Frequencies.Values.GetEnumerator(); // could be optimized so that this only called the last time
        }

        /**<summary>Convert a pitch symbol or raw number into a hertz value.</summary>
         * <param name="symbol">The symbol to convert from.</param>
         */
        public static double ConvertFromSymbol(string symbol)
        {
            // Remove leading P of raw pitch symbols
            symbol = symbol.TrimStart(new char[] { 'p', 'P' });

            string note = new string(symbol.TakeWhile((x) => !char.IsNumber(x)).ToArray()).ToLower();
            if (note == string.Empty) // raw pitch value
            {
                return Convert.ToDouble(symbol);
            }
            string o = new string(symbol.SkipWhile((x) => !char.IsNumber(x)).ToArray());
            int octave;
            if (o != string.Empty) octave = Convert.ToInt32(o);
            else octave = 4;

            float index = Notes[note] - 9;
            index += octave * 12;
            index = ApplyStretch(index);
            double frequency = 440 * Math.Pow(2, (index - 48) / 12);
            return frequency;
        }

        /**<summary>Used in converting symbols to pitches.</summary>*/
        protected static Dictionary<string, int> Notes = new Dictionary<string, int>
        {
            { "a", 9 }, { "a#", 10 }, { "bb", 10 }, { "b", 11 }, { "c", 0 },
            { "c#", 1 }, { "db", 1 }, { "d", 2 }, { "d#", 3 }, { "eb", 3 },
            { "e", 4 }, { "f", 5 }, { "f#", 6 }, { "gb", 6 }, { "g", 7 },
            { "g#", 8 }, { "ab", 8 }
        };

        protected static float ApplyStretch(float index)
        {
            float cents = 0;

            if (index > 48)
            {
                cents = StretchSharp.TakeWhile(x => x.Key <= index).Select(x => x.Value).Sum();
            }
            else if (index < 48)
            {
                cents = StretchFlat.TakeWhile(x => x.Key >= index).Select(x => x.Value).Sum();
            }

            return index + cents;
        }

        protected static Dictionary<int, float> StretchSharp = new Dictionary<int, float>
        {
            {54, .01f}, {60, .01f}, {64, .01f}, {68, .01f}, {70, .01f}, {72, .01f}, {73, .01f}, {74, .01f},
            {75, .01f}, {76, .01f}, {77, .01f}, {78, .01f}, {79, .01f}, {80, .01f}, {81, .02f}, {82, .02f},
            {83, .02f}, {84, .02f}, {85, .02f}, {86, .02f}, {87, .03f}
        };

        protected static Dictionary<int, float> StretchFlat = new Dictionary<int, float>
        {
            {47, -.01f}, {41, -.01f}, {24, -.01f}, {22, -.01f}, {17, -.01f}, {15, -.01f}, {13, -.01f},
            {12, -.01f}, {11, -.01f}, {10, -.01f}, {9, -.01f}, {8, -.01f}, {7, -.01f}, {6, -.01f}, {5, -.01f},
            {4, -.01f}, {3, -.01f}, {2, -.01f}, {1, -.01f}, {0, -.01f}
        };


        /// <summary>
        /// Check if a source name is a pitch source.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool IsPitchSourceName(string name)
        {
            return !name.Contains(".wav");
            //return System.Text.RegularExpressions.Regex.IsMatch(name, @"\w+\.[a-z]+$");
        }

        public int BlockAlignment
        {
            get => 2;
        }

        /**<summary>The format of this stream.</summary>*/
        public WaveFormat WaveFormat
        {
            get { return waveFormat; }
        }

        /**<summary>A dictionary of frequencies and the cells they are tied to.</summary>*/
        public Dictionary<BeatCell, double> Frequencies = new Dictionary<BeatCell, double>();
        /**<summary>Used to cycle through the pitch frequencies used by this source.</summary>*/
        protected IEnumerator<double> freqEnum;

        /**<summary>Reset state to default values.</summary>*/
        public void Reset()
        {
            if (Frequencies.Any())
            {
                // possibility that there's no frequencies, just wav modifiers
                freqEnum.Reset(); //= Frequencies.Values.GetEnumerator();
            }
            BeatCollection.Enumerator = BeatCollection.GetEnumerator();
            ByteInterval = 0;
            sampleValue = 0;
            SampleRemainder = 0;
            //cycle = 0;
            Gain = Volume;
            if (Metronome.GetInstance().IsSilentInterval)
            {
                SetSilentInterval(Metronome.GetInstance().AudibleInterval, Metronome.GetInstance().SilentInterval);
            }
            if (Metronome.GetInstance().IsRandomMute)
            {
                randomMuteCountdown = null;
                currentlyMuted = false;
            }
            if (initialOffset > 0)
                SetOffset(initialOffset);
        }

        /**<summary>Get the next frequency in the sequence.</summary>*/
        public double GetNextFrequency()
        {
            if (!Frequencies.Any()) return 0;
            if (freqEnum.MoveNext()) return freqEnum.Current;
            else
            {
                freqEnum.Reset();
                return GetNextFrequency();
            }
        }

        /**<summary>The current frequency in hertz.</summary>*/
        public double Frequency { get; set; }

        /**<summary>Used to create the fade out of the beep sound. Resets to the value of Volume on interval completetion.</summary>*/
        protected double Gain { get; set; }
        double gainStep; // the amount that gain is subtracted by for each byte to produce fade.
        double newGainStep; // set when Volume changes and takes effect when byte interval resets.

        /**<summary>If a multiply is cued, perform operation on all relevant members at the start of a stream read.</summary>*/
        public void MultiplyByteInterval()
        {

            BeatCollection.ConvertBpmValues();

            double intervalMultiplyFactor = Metronome.GetInstance().TempoChangeRatio;

            double mult = intervalMultiplyFactor * ByteInterval;
            ByteInterval = (long)mult;
            SampleRemainder *= intervalMultiplyFactor;
            SampleRemainder += mult - ByteInterval;

            if (SampleRemainder >= 1)
            {
                ByteInterval += (long)SampleRemainder;
                SampleRemainder -= (int)SampleRemainder;
            }

            // multiply the silent interval
            if (Metronome.GetInstance().IsSilentInterval)
            {
                double sim = currentSlntIntvl * intervalMultiplyFactor;
                currentSlntIntvl = (long)sim;
                SilentIntervalRemainder *= intervalMultiplyFactor;
                SilentIntervalRemainder += sim - currentSlntIntvl;
                SilentInterval *= intervalMultiplyFactor;
                AudibleInterval *= intervalMultiplyFactor;
            }

            // multiply the offset aswell
            if (hasOffset)
            {
                mult = intervalMultiplyFactor * totalOffset;
                totalOffset = (long)mult;
                OffsetRemainder *= intervalMultiplyFactor;
                OffsetRemainder += mult - totalOffset;
            }
            if (initialOffset > 0)
            {
                initialOffset *= intervalMultiplyFactor;
            }

        }


        /**<summary>The volume control for this stream.</summary>*/
        public double Volume
        {
            get { return _volume; }
            set
            {
                _volume = value;
                newGainStep = value / (WaveFormat.SampleRate * DecayLength); //DecayFactor;
                // 16000 BPS
            }
        }
        double _volume = 1;

        public double GainStep { get => gainStep; }

        private volatile float pan;
        /**<summary>Gets/sets the pan value. -1 to 1.</summary>*/
        public float Pan
        {
            get { return pan; }
            set
            {
                pan = value;

                left = (Pan + 1f) / 2;
                right = (2 - (Pan + 1f)) / 2;
            }
        }
        private float left;
        private float right;

        public float Left { get => left; }
        public float Right { get => right; }

        /**<summary>Gets the next interval value and determines if it will be muted.</summary>*/
        public long GetNextInterval()
        {
            BeatCollection.Enumerator.MoveNext();
            long result = BeatCollection.Enumerator.Current;
            // hand silent interval

            if (IsSilentIntervalSilent(ByteInterval))
            {
                //previousByteInterval = result;
                //return result;
            }
            
            currentlyMuted = IsRandomMuted();

            previousByteInterval = result;

            return result;
        }

        protected double SilentInterval; // total samples in silent interval
        protected double AudibleInterval; // total samples in audible interval
        protected long currentSlntIntvl; // samples in current interval (silent or audible)
        protected bool silentIntvlSilent = false; // currently silent
        protected double SilentIntervalRemainder; // fractional portion

        /**<summary>Sets the silent interval.</summary>
         * <param name="audible">Number of quarter notes audible.</param>
         * <param name="silent">Number of quarter notes silent.</param>
         */
        public void SetSilentInterval(double audible, double silent)
        {
            AudibleInterval = BeatCell.ConvertFromBpm(audible, this);
            SilentInterval = BeatCell.ConvertFromBpm(silent, this);
            currentSlntIntvl = (long)AudibleInterval - totalOffset;
            SilentIntervalRemainder = audible - (long)audible + OffsetRemainder;
        }

        protected long? randomMuteCountdown = null; // If the rand mute has a countdown, we track it here
        protected long randomMuteCountdownTotal; // The rand mute initial countdown value.
        protected bool currentlyMuted = false; // true if sound is randomly muted.

        /**<summary>Returns true if the note should be randomly muted.</summary>*/
        protected bool IsRandomMuted()
        {
            if (!Metronome.GetInstance().IsRandomMute)
            {
                currentlyMuted = false;
                return false;
            }

            // init countdown
            if (randomMuteCountdown == null && Metronome.GetInstance().RandomMuteSeconds > 0)
            {
                randomMuteCountdown = randomMuteCountdownTotal = (long)Metronome.GetInstance().RandomMuteSeconds * BytesPerSec - totalOffset;
            }

            int rand = Metronome.GetRandomNum();

            if (randomMuteCountdown == null)
                return rand < Metronome.GetInstance().RandomMutePercent;
            else
            {
                // countdown
                if (randomMuteCountdown > 0) randomMuteCountdown -= previousByteInterval;
                else if (randomMuteCountdown < 0) randomMuteCountdown = 0;

                float factor = (float)(randomMuteCountdownTotal - randomMuteCountdown) / randomMuteCountdownTotal;
                return rand < Metronome.GetInstance().RandomMutePercent * factor;
            }
        }

        /**<summary>Returns true if silent interval is currently silent.</summary>*/
        public bool IsSilentIntervalSilent(long interval) // check if silent interval is currently silent or audible. Perform timing shifts
        {
            if (!Metronome.GetInstance().IsSilentInterval) return false;

            bool isSilent = currentSlntIntvl <= SilentInterval;

            currentSlntIntvl -= interval;

            if (currentSlntIntvl <= 0)
            {
                currentSlntIntvl = (long)(currentSlntIntvl % (SilentInterval + AudibleInterval));

                currentSlntIntvl += (long)(SilentInterval + AudibleInterval);
            }

            return isSilent;
        }

        /**<summary>Empty for this pitches, muting is not determined beforehand.</summary>*/
        public void SetInitialMuting() { }

        /**<summary>Set the amount of offset in samples.</summary>
         * <param name="value">Value in samples.</param>
         */
        public void SetOffset(double value)
        {
            initialOffset = value;
            totalOffset = (long)value;
            OffsetRemainder = value - totalOffset;
            
            hasOffset = totalOffset > 0;
        }

        /**<summary>Get the current amount of offset in samples.</summary>*/
        public double GetOffset()
        {
            return totalOffset + OffsetRemainder;
        }

        protected double initialOffset = 0; // the offset value to reset to.
        protected long totalOffset = 0; // time to wait before reading source.
        protected double OffsetRemainder = 0;
        protected bool hasOffset = false;
        protected bool lastIntervalMuted = false; // used to cycle pitch if the last interval was randomly muted.

        protected long previousByteInterval;
        protected long ByteInterval;

        double WaveLength = 1; // used in sine wave calculation

        double sampleValue = 0;

        //uint cycle = 0; // cycle count, used to sync up all layers

        /**<summary>Reads from the audio stream.</summary>
         * <param name="buffer">Sample array buffer.</param>
         */
        public int Read(float[] buffer, int offset, int count)
        {
            //if (count == 7040) { return count; } // account for the occasional blip at start up.

            int outIndex = offset;
            
            // Complete Buffer
            for (int sampleCount = 0; sampleCount < count / waveFormat.Channels; sampleCount++)
            {
                // account for offset
                if (hasOffset)
                {
                    totalOffset -= 1;
            
                    buffer[outIndex++] = 0;
                    buffer[outIndex++] = 0;
            
                    if (totalOffset == 0)
                    {
                        hasOffset = false;
                        SampleRemainder += OffsetRemainder;
                    }
                    // add remainder to layer.R
                    continue;
                }
                bool freqChanged = false;
                // interval is over, reset
                if (ByteInterval == 0)
                {
                    double curFreq = Frequency;
                    Frequency = GetNextFrequency();
                    ByteInterval = GetNextInterval();
                    // handle volume and frequency consts if producing

                    if (!silentIntvlSilent && !currentlyMuted && Frequency != 0)
                    {
                        // what should nsample be to create a smooth transition?
                        if (sampleValue != 0 && Gain != 0)
                        {
                            double wavePosition = (float)((nSample % WaveLength) / WaveLength);

                            if (Frequency != curFreq)
                                WaveLength = waveFormat.SampleRate / Frequency;

                            nSample = (float)(Math.Asin(sampleValue / Volume) / TwoPi * WaveLength);

                            // reposition to correct quadrant of wave
                            if (wavePosition > .25 && wavePosition <= .5)
                            {
                                nSample += (float)(WaveLength / 4 - nSample) * 2;
                            }
                            else if (wavePosition > .5 && wavePosition <= .75)
                            {
                                nSample -= (float)(WaveLength / 4 + nSample) * 2;
                            }
                        }
                        else nSample = 0;

                        SinWave = new SinWaveGenerator(nSample, Frequency).GetEnumerator();
                        SinWave.MoveNext();

                        freqChanged = curFreq != Frequency;
                        Gain = Volume;
                        if (gainStep != newGainStep) // set new gainstep if volume was changed
                            gainStep = newGainStep;
                    }
                    else
                    {
                        Frequency = curFreq; //retain frequency if random/interval muting occurs.
                        // if first note is getting muted, set gain to 0
                        if (Gain == Volume) Gain = 0;
                    }
                }


                if (Gain <= 0)
                {
                    nSample = 0;
                    sampleValue = 0;
                }
                else
                {
                    // check for muting
                    if (Layer != null && (Layer.IsMuted || Layer.SoloGroupEngaged && !Layer.IsSoloed))
                    {
                        nSample = 0;
                        sampleValue = 0;
                    }
                    else
                    {
                        //// Sin Generator
                        //if (freqChanged)
                        //{
                        //    WaveLength = TwoPi * Frequency / waveFormat.SampleRate; // reuse this value
                        //    freqChanged = false;
                        //}
                        //
                        //sampleValue = Gain * Math.Sin(nSample * WaveLength);
                        sampleValue = SinWave.Current * Gain;
                        SinWave.MoveNext();

                        nSample++;

                    }
                    Gain -= gainStep;

                }

                // Set the pan amounts.
                for (int i = 0; i < waveFormat.Channels; i++)
                {
                    if (i == 0)
                        buffer[outIndex++] = (float)sampleValue * right;
                    else
                        buffer[outIndex++] = (float)sampleValue * left;
                }
            
                ByteInterval -= 1;
            }

            //cycle++;

            return count;
        }

       
        public void Dispose() { }

    }

}
