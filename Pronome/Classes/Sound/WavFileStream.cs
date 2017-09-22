using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;

namespace Pronome
{
    /** <summary>Handles the reading of .wav file sound sources.</summary> */
    public class WavFileStream : WaveStream, IStreamProvider
    {
        WaveStream sourceStream;

        /**<summary>Used to implement panning.</summary>*/
        public PanningSampleProvider Panner { get; set; }

        /// <summary>
        /// Implements volume control. Exposed to mixer.
        /// </summary>
        public VolumeSampleProvider VolumeProvider { get; set; }

        //public bool IsPitch { get { return false; } }
        public ISoundSource SoundSource { get; set; }

        /**<summary>The layer that this sound is associated with</summary>*/
        public Layer Layer { get; set; }

        /**<summary>Holds the byte interval values and HiHat duration for HH open sounds.</summary>*/
        public SourceBeatCollection BeatCollection { get; set; }

        /**<summary>The byte rate for this stream.</summary>*/
        public int BytesPerSec { get; set; }

        public bool ProduceBytes { get; set; } = true;

        Stream rawStream;

        /**<summary>Constructor</summary>*/
        public WavFileStream(ISoundSource source)
        {
            SoundSource = source;
            //this.fileName = fileName;
            // check if it's an outside source or in assembly
            //Stream s = null;
            if (source.Uri.IndexOf("Pronome") == 0)
            {
                Assembly myAssembly = Assembly.GetExecutingAssembly();
                rawStream = myAssembly.GetManifestResourceStream(source.Uri);
                sourceStream = new WaveFileReader(rawStream);
            }
            else
            {
                if (File.Exists(source.Uri))
                {
                    sourceStream = new WaveFileReader(source.Uri);
                }
                else
                {
                    // if file doesn't exist, use silence
                    Assembly myAssembly = Assembly.GetExecutingAssembly();
                    rawStream = myAssembly.GetManifestResourceStream(InternalSource.Library[0].Uri);
                    sourceStream = new WaveFileReader(rawStream);
                }
            }

            
            ISampleProvider provider = null;

            // convert to mono
            if (sourceStream.WaveFormat.Channels == 2)
            {
                provider = new StereoToMonoSampleProvider(this.ToSampleProvider());
            }
            else
            {
                provider = this.ToSampleProvider();
            }

            Panner = new PanningSampleProvider(provider);
            Panner.Pan = 0;
            Panner.PanStrategy = new StereoPanStrategy();
            VolumeProvider = new VolumeSampleProvider(Panner);
            BytesPerSec = VolumeProvider.WaveFormat.AverageBytesPerSecond;            

            Metronome met = Metronome.GetInstance();
            // set audible/silent interval if already exists
            if (met.IsSilentInterval)
                SetSilentInterval(met.AudibleInterval, met.SilentInterval);

            chunkSizeOverflow = 3520 * WaveFormat.BlockAlign;
        }

        /**<summary>The volume for this sound source.</summary>*/
        public double Volume
        {
            get { return VolumeProvider.Volume; }
            set { VolumeProvider.Volume = (float)value; }
        }

        /**<summary>The pan control for this sound. -1 to 1</summary>*/
        public float Pan
        {
            get => Panner.Pan; set => Panner.Pan = value;
        }

        /**<summary>Gets the wave format object for this stream.</summary>*/
        public override WaveFormat WaveFormat
        {
            get => sourceStream.WaveFormat;
        }

        /**<summary>Reset this sound so that it will play from the start.</summary>*/
        public void Reset()
        {
            BeatCollection.Enumerator = BeatCollection.GetEnumerator();
            ByteInterval = 0;
            previousByteInterval = 0;

            HiHatOpenIsMuted = false;
            //HiHatMuteInitiated = false;
            HiHatCycleToMute = 0;


            // set stream back to start.
            Position = 0;

            if (initialOffset > 0)
                SetOffset(initialOffset);

            if (Metronome.GetInstance().IsRandomMute)
            {
                randomMuteCountdown = null; // will be reinitialized
                currentlyMuted = false;
            }
            silentIntvlSilent = false;
            if (Metronome.GetInstance().IsSilentInterval)
            { // setInitialMuting is called in this method
                SetSilentInterval(Metronome.GetInstance().AudibleInterval, Metronome.GetInstance().SilentInterval);
            }
            else
            {
                // will first muting occur for first sound?
                SetInitialMuting();
            }
        }

        /**<summary>Get the length of the source file in bytes.</summary>*/
        public override long Length
        {
            get => sourceStream.Length;
        }

        /**<summary>Not used for wav streams.</summary>*/
        public double Frequency { get; set; }

        /**<summary>Get or set the source stream's position.</summary>*/
        public override long Position
        {
            get { return sourceStream.Position; }
            set { sourceStream.Position = value; }
        }

        public int BlockAlignment
        {
            get => sourceStream.BlockAlign;
        }

        /// <summary>
        /// If a chunksize is requested at this amount, skip it.
        /// </summary>
        public int chunkSizeOverflow;

        /**<summary>Get the next byte interval while also setting the mute status.</summary>*/
        public long GetNextInterval()
        {
            BeatCollection.Enumerator.MoveNext();
            long result = BeatCollection.Enumerator.Current;

            previousByteInterval = result;

            if (IsSilentIntervalSilent())
            {
                return result;
            }

            currentlyMuted = IsRandomMuted();

            return result;
        }

        object _multLock = new object();

        /**<summary>Perform a cued byte interval multiplication.</summary>*/
        public void MultiplyByteInterval()
        {

            BeatCollection.ConvertBpmValues();

            double intervalMultiplyFactor = Metronome.GetInstance().TempoChangeRatio;

            double div = ByteInterval / BlockAlignment;
            div *= intervalMultiplyFactor;
            Layer.Remainder *= intervalMultiplyFactor; // multiply remainder as well
            Layer.Remainder += div - (int)div;
            ByteInterval = (int)div * BlockAlignment;
                
            if (Layer.Remainder >= 1)
            {
                ByteInterval += (int)Layer.Remainder * BlockAlignment;
                Layer.Remainder -= (int)Layer.Remainder;
            }

            // multiply the offset aswell
            if (hasOffset)
            {
                div = totalOffset / BlockAlignment;
                div *= intervalMultiplyFactor;
                offsetRemainder *= intervalMultiplyFactor;
                offsetRemainder += div - (int)div;
                totalOffset = (int)div * BlockAlignment;
            }
            if (initialOffset > 0)
            {
                initialOffset *= intervalMultiplyFactor;
            }
                
            // multiply the silent interval
            if (Metronome.GetInstance().IsSilentInterval)
            {
                double sid = currentSlntIntvl / BlockAlignment;
                sid *= intervalMultiplyFactor;
                SilentIntervalRemainder *= intervalMultiplyFactor;
                SilentIntervalRemainder += sid - (int)sid;
                currentSlntIntvl = (int)sid * BlockAlignment;
                SilentInterval *= intervalMultiplyFactor;
                AudibleInterval *= intervalMultiplyFactor;
            }

            intervalMultiplyCued = false;

        }
        /**<summary>Cue a multiply byte interval operation to occur at next read cycle start.</summary>
         * <param name="factor">The value by which to multiply</param>
         */
        public void MultiplyByteInterval(double factor)
        {
            lock (_multLock)
            {
                if (!intervalMultiplyCued)
                {
                    intervalMultiplyFactor = factor;
                    intervalMultiplyCued = true;
                }
            }
        }
        bool intervalMultiplyCued = false;
        double intervalMultiplyFactor;

        public void SetInitialMuting()
        {
            if (ByteInterval == 0)
            {
                Position = Length;

                // determine mutings
                Metronome met = Metronome.GetInstance();
                if (met.IsRandomMute)
                    currentlyMuted = IsRandomMuted();
                if (met.IsSilentInterval)
                    silentIntvlSilent = IsSilentIntervalSilent();
            }
        }

        protected bool IsSilentIntervalSilent() // check if silent interval is currently silent or audible. Perform timing shifts
        {
            if (!Metronome.GetInstance().IsSilentInterval) return false;
            currentSlntIntvl -= previousByteInterval;
            if (currentSlntIntvl <= 0)
            {
                do
                {
                    silentIntvlSilent = !silentIntvlSilent;
                    double nextInterval = silentIntvlSilent ? SilentInterval : AudibleInterval;
                    currentSlntIntvl += (long)nextInterval;
                    SilentIntervalRemainder += nextInterval - (long)nextInterval;

                    if (SilentIntervalRemainder >= 1)
                    {
                        int rounded = (int)SilentIntervalRemainder;
                        currentSlntIntvl += rounded;
                        SilentIntervalRemainder -= rounded;
                    }

                } while (currentSlntIntvl < 0);
            }

            return silentIntvlSilent;
        }

        protected long? randomMuteCountdown = null;
        protected long randomMuteCountdownTotal;
        protected bool currentlyMuted = false;

        protected bool IsRandomMuted()
        {
            bool result;
            if (!Metronome.GetInstance().IsRandomMute)
            {
                currentlyMuted = false;
                return false;
            }

            // init countdown
            if (randomMuteCountdown == null && Metronome.GetInstance().RandomMuteSeconds > 0)
            {
                randomMuteCountdown = randomMuteCountdownTotal = Metronome.GetInstance().RandomMuteSeconds * BytesPerSec - totalOffset;
            }

            int rand = Metronome.GetRandomNum();
            if (randomMuteCountdown == null)
            {
                result = rand < Metronome.GetInstance().RandomMutePercent;
            }
            else
            {
                // countdown
                if (randomMuteCountdown > 0) randomMuteCountdown -= previousByteInterval; //previousByteInterval;
                if (randomMuteCountdown < 0) randomMuteCountdown = 0;

                float factor = (float)(randomMuteCountdownTotal - randomMuteCountdown) / randomMuteCountdownTotal;
                result = rand < Metronome.GetInstance().RandomMutePercent * factor;
            }

            return result;
        }

        public void SetOffset(double value)
        {
            initialOffset = value;
            totalOffset = ((int)value) * BlockAlignment;
            offsetRemainder = value - (int)value;
            
            hasOffset = totalOffset > 0;
            // is first sound muted?
            //SetInitialMuting();
        }

        public double GetOffset()
        {
            return initialOffset + offsetRemainder;
        }

        protected double initialOffset = 0; // the offset to reset to.
        protected int totalOffset = 0;
        protected double offsetRemainder = 0;
        protected bool hasOffset = false;

        protected double SilentInterval; // remaining samples in silent interval
        protected double AudibleInterval; // remaining samples in audible interval
        protected long currentSlntIntvl;
        protected bool silentIntvlSilent = false;
        protected double SilentIntervalRemainder; // fractional portion
        //protected bool IsHiHatOpen = false; // is this an open hihat sound?
        //protected bool IsHiHatClose = false; // is this a close hihat sound?
        protected bool HiHatOpenIsMuted = false; // an open hihat sound was muted so currenthihatduration should not be increased by closed sounds being muted.

        public void SetSilentInterval(double audible, double silent)
        {
            AudibleInterval = BeatCell.ConvertFromBpm(audible, this) * BlockAlignment;
            SilentInterval = BeatCell.ConvertFromBpm(silent, this) * BlockAlignment;
            currentSlntIntvl = (long)(AudibleInterval - initialOffset * BlockAlignment - BlockAlignment);
            SilentIntervalRemainder = audible - (int)audible + offsetRemainder;

            SetInitialMuting();
        }

        protected long previousByteInterval = 0;

        public long ByteInterval;

        public long HiHatCycleToMute;
        public long HiHatByteToMute;
        int CurrentHiHatDuration = -1;
        SortedSet<int> HiHatBytesToMute = new SortedSet<int>();

        public override int Read(byte[] buffer, int offset, int count)
        {
            //if (count == chunkSizeOverflow) { return count; } // somtimes count is double at start for some reason

            int bytesCopied = 0;

            // get the first queued hihat down time if this is an open sound
            if (Layer.HasHiHatClosed && SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open && HiHatBytesToMute.Any())
            {
                CurrentHiHatDuration = HiHatBytesToMute.Min;
            }

            while (bytesCopied < count)
            {

                if (hasOffset)
                {
                    int subtract = totalOffset > count - bytesCopied ? count - bytesCopied : totalOffset;
                    totalOffset -= subtract;
                    Array.Copy(new byte[subtract], 0, buffer, bytesCopied + offset, subtract);
                    bytesCopied += subtract;

                    if (totalOffset == 0)
                    {
                        Layer.Remainder += offsetRemainder;
                        hasOffset = false;
                    }
                    continue;
                }

                if (ByteInterval == 0)
                {
                    if (!silentIntvlSilent && !currentlyMuted)
                    {
                        sourceStream.Position = 0;
                        CurrentHiHatDuration = -1;
                    }

                    // if this is a hihat closed sound, pass it's position to preceding open hihat sounds
                    if (SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Closed && Layer.HasHiHatOpen && !silentIntvlSilent && !currentlyMuted)
                    {
                        foreach (WavFileStream hho in Layer.GetAllSources().Where(x => x.SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open))
                        {
                            hho.HiHatBytesToMute.Add(bytesCopied);
                        }
                    }
                    else if (Layer.HasHiHatClosed && SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open && HiHatBytesToMute.Any())
                    {
                        CurrentHiHatDuration = HiHatBytesToMute.SkipWhile(x => x < bytesCopied).FirstOrDefault();
                    
                        if (CurrentHiHatDuration == 0 && bytesCopied > 0) CurrentHiHatDuration = -1;
                    }

                    ByteInterval = GetNextInterval();
                }

                int chunkSize = (int)new long[] { ByteInterval, count - bytesCopied }.Min();

                // if this is a hihat open sound, determine when it should be stopped by a hihat close sound.
                if (SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open && Layer.HasHiHatClosed && CurrentHiHatDuration > 0)
                {
                    if (chunkSize >= CurrentHiHatDuration)
                    {
                        chunkSize = (int)CurrentHiHatDuration;
                    }
                }

                int result = 0;
                int result2 = 0;


                // read from file if producing
                if (ProduceBytes)
                {
                    if (!Layer.IsMuted && !(Pronome.Layer.SoloGroupEngaged && !Layer.IsSoloed) && !(SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open && CurrentHiHatDuration == 0))
                        result = sourceStream.Read(buffer, offset + bytesCopied, chunkSize);
                    else // progress stream silently
                        result2 = sourceStream.Read(new byte[buffer.Length], offset + bytesCopied, chunkSize);
                }
                else
                {
                    result = chunkSize;
                }

            
                if (result == 0) // silence
                {
                    bool use2 = result2 > 0; // true if the sourcestream was progressed silently.

                    Array.Copy(new byte[chunkSize], 0, buffer, offset + bytesCopied, use2 ? result2 : chunkSize);

                    ByteInterval -= use2 ? result2 : chunkSize;
                    bytesCopied += use2 ? result2 : chunkSize;
                }
                else
                {
                    ByteInterval -= result;
                    bytesCopied += result;
                }

                if (Layer.HasHiHatClosed && SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open && CurrentHiHatDuration > 0)
                {
                    // open sound will be cut off when this hits 0
                    CurrentHiHatDuration -= result + result2;
                }

            }

            if (Layer.HasHiHatClosed && SoundSource.HiHatStatus == InternalSource.HiHatStatuses.Open)
            {
                // clear out the queued hihat mute points
                HiHatBytesToMute.Clear();
            }

            return count;
        }

        public const string SilentSourceName = "Pronome.wav.silence.wav";

        void IStreamProvider.Dispose()
        {
            sourceStream.Dispose();
            rawStream?.Dispose();
            Dispose();
        }
    }
}
