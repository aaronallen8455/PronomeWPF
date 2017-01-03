﻿using System;
using System.Collections;
using System.Linq;
using NAudio.Wave;

namespace Pronome
{
    /** <summary>Handles the reading of .wav file sound sources.</summary> */
    public class WavFileStream : WaveStream, IStreamProvider
    {
        WaveFileReader sourceStream;

        /**<summary>This is added to the mixer. Exposes controls for volume and pan.</summary>*/
        public WaveChannel32 Channel { get; set; }

        public bool IsPitch { get { return false; } }

        /**<summary>The layer that this sound is associated with</summary>*/
        public Layer Layer { get; set; }

        /**<summary>Holds the byte interval values and HiHat duration for HH open sounds.</summary>*/
        public SourceBeatCollection BeatCollection { get; set; }

        /**<summary>The byte rate for this stream.</summary>*/
        public int BytesPerSec { get; set; }
        string fileName;
        /**<summary>Constructor</summary>*/
        public WavFileStream(string fileName)
        {
            this.fileName = fileName;
            sourceStream = new WaveFileReader(fileName);
            BytesPerSec = sourceStream.WaveFormat.AverageBytesPerSecond;
            Channel = new WaveChannel32(this);

            Metronome met = Metronome.GetInstance();
            // set audible/silent interval if already exists
            if (met.IsSilentInterval)
                SetSilentInterval(met.AudibleInterval, met.SilentInterval);

            // is this a hihat sound?
            if (BeatCell.HiHatOpenFileNames.Contains(fileName)) IsHiHatOpen = true;
            else if (BeatCell.HiHatClosedFileNames.Contains(fileName)) IsHiHatClose = true;
            
            // determine if first sound will be muted
            //if (met.IsRandomMute || met.IsSilentInterval) is now done in the layer, after all sources have been created
            //{
            //    SetInitialMuting();
            //}
        }

        /**<summary>The volume for this sound source.</summary>*/
        public double Volume
        {
            get { return Channel.Volume; }
            set { Channel.Volume = (float)value; }
        }

        /**<summary>The pan control for this sound. -1 to 1</summary>*/
        public float Pan
        {
            get { return Channel.Pan; }
            set { Channel.Pan = value; }
        }

        /**<summary>Gets the wave format object for this stream.</summary>*/
        public override WaveFormat WaveFormat
        {
            get { return sourceStream.WaveFormat; }
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
            cycle = 0;


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
            get { return sourceStream.Length; }
        }

        /**<summary>Not used for wav streams.</summary>*/
        public double Frequency { get; set; }

        /**<summary>Get or set the source stream's position.</summary>*/
        public override long Position
        {
            get { return sourceStream.Position; }
            set { sourceStream.Position = value; }
        }

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
            lock (_multLock)
            {
                if (intervalMultiplyCued)
                {
                    BeatCollection.MultiplyBeatValues();

                    double div = ByteInterval / 4;
                    div *= intervalMultiplyFactor;
                    Layer.Remainder *= intervalMultiplyFactor; // multiply remainder as well
                    Layer.Remainder += div - (int)div;
                    ByteInterval = (int)div * 4;
                    
                    if (Layer.Remainder >= 1)
                    {
                        ByteInterval += (int)Layer.Remainder * 4;
                        Layer.Remainder -= (int)Layer.Remainder;
                    }

                    // multiply the offset aswell
                    if (hasOffset)
                    {
                        div = totalOffset / 4;
                        div *= intervalMultiplyFactor;
                        offsetRemainder *= intervalMultiplyFactor;
                        offsetRemainder += div - (int)div;
                        totalOffset = (int)div * 4;
                    }
                    if (initialOffset > 0)
                    {
                        initialOffset *= intervalMultiplyFactor;
                    }
                    
                    // multiply the silent interval
                    if (Metronome.GetInstance().IsSilentInterval)
                    {
                        double sid = SilentInterval / 4;
                        sid *= intervalMultiplyFactor;
                        SilentIntervalRemainder *= intervalMultiplyFactor;
                        SilentIntervalRemainder += sid - (int)sid;
                        SilentInterval = (int)sid * 4;
                    }

                    //// do the hihat cutoff interval
                    if (IsHiHatOpen && CurrentHiHatDuration != 0)
                    {
                        div = CurrentHiHatDuration / 4;
                        CurrentHiHatDuration = (int)(div * intervalMultiplyFactor) * 4;
                    }

                    // recalculate the hihat count and byte to cutoff values
                    if (IsHiHatOpen && Layer.HasHiHatClosed)
                    {
                        long countDiff = HiHatCycleToMute - cycle;
                        long totalBytes = countDiff * 2560 + HiHatByteToMute;
                        totalBytes = (long)(totalBytes * intervalMultiplyFactor);
                        HiHatCycleToMute = cycle + totalBytes / 2560;
                        HiHatByteToMute = totalBytes % 2560;
                        HiHatByteToMute -= HiHatByteToMute % 4; // align
                    }

                    intervalMultiplyCued = false;
                }
            }
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

                // if this is a hihat down, pass it's time position to all hihat opens in this layer
                if (IsHiHatClose && Layer.HasHiHatOpen && !silentIntvlSilent && !currentlyMuted && hasOffset)
                {
                    int total = totalOffset;
                    int cycles = total / 2560;
                    int bytes = total % 2560;

                    // assign the hihat cutoff to all open hihat sounds.
                    IEnumerable hhos = Layer.AudioSources.Values.Where(x => !x.IsPitch && ((WavFileStream)x).IsHiHatOpen);
                    foreach (WavFileStream hho in hhos)
                    {
                        hho.HiHatByteToMute = bytes;
                        hho.HiHatCycleToMute = cycles;
                    }
                }
            }
        }

        protected bool IsSilentIntervalSilent() // check if silent interval is currently silent or audible. Perform timing shifts
        {
            if (!Metronome.GetInstance().IsSilentInterval) return false;
            //currentSlntIntvl -= previousByteInterval;
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
            totalOffset = ((int)value) * 4;
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
        protected bool IsHiHatOpen = false; // is this an open hihat sound?
        protected bool IsHiHatClose = false; // is this a close hihat sound?
        protected bool HiHatOpenIsMuted = false; // an open hihat sound was muted so currenthihatduration should not be increased by closed sounds being muted.

        public void SetSilentInterval(double audible, double silent)
        {
            AudibleInterval = BeatCell.ConvertFromBpm(audible, this) * 4;
            SilentInterval = BeatCell.ConvertFromBpm(silent, this) * 4;
            currentSlntIntvl = (long)(AudibleInterval - initialOffset * 4 - 4);
            SilentIntervalRemainder = audible - (int)audible + offsetRemainder;

            SetInitialMuting();
        }

        protected long previousByteInterval = 0;

        public long ByteInterval;

        public long HiHatCycleToMute;
        public long HiHatByteToMute;
        long CurrentHiHatDuration = 0;
        //bool HiHatMuteInitiated = false;
        int cycle = 0;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 5120) { return count; } // somtimes count is double at start for some reason

            int bytesCopied = 0;

            // perform interval multiplication if cued
            if (intervalMultiplyCued)
            {
                MultiplyByteInterval();
            }
            
            // set the upcoming hihat close time for hihat open sounds
            if (!hasOffset && IsHiHatOpen && cycle == HiHatCycleToMute - 1)
            {
                CurrentHiHatDuration = HiHatByteToMute + count;
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
                        if (IsHiHatOpen)
                        {
                            HiHatOpenIsMuted = false;
                        }
                        sourceStream.Position = 0;
                    }
                    
                    ByteInterval = GetNextInterval();

                    // if this is a hihat down, pass it's time position to all hihat opens in this layer
                    if (IsHiHatClose && Layer.HasHiHatOpen && !silentIntvlSilent && !currentlyMuted)
                    {
                        long total = bytesCopied + ByteInterval + offset;
                        long cycles = total / count + cycle;
                        long bytes = total % count;
                        
                        // assign the hihat cutoff to all open hihat sounds.
                        IEnumerable hhos = Layer.AudioSources.Where(x => BeatCell.HiHatOpenFileNames.Contains(x.Key)).Select(x => x.Value);
                        foreach (WavFileStream hho in hhos)
                        {
                            hho.HiHatByteToMute = bytes;
                            hho.HiHatCycleToMute = cycles;
                        }
                    }
                }

                int chunkSize = (int)new long[] { ByteInterval, count - bytesCopied }.Min();

                // if this is a hihat open sound, determine when it should be stopped by a hihat close sound.
                if (IsHiHatOpen && CurrentHiHatDuration > 0)
                {
                    if (chunkSize >= CurrentHiHatDuration)
                    {
                        chunkSize = (int)CurrentHiHatDuration;

                    }
                    else CurrentHiHatDuration -= chunkSize;
                }
                int result = 0;
                int result2 = 0;

                if (!Layer.IsMuted && !(Pronome.Layer.SoloGroupEngaged && !Layer.IsSoloed) && !HiHatOpenIsMuted)
                    result = sourceStream.Read(buffer, offset + bytesCopied, chunkSize);
                else // progress stream silently
                    result2 = sourceStream.Read(new byte[buffer.Length], offset + bytesCopied, chunkSize);

                if (result == 0) // silence
                {
                    bool use2 = result2 > 0; // true if the sourcestream was progressed silently.
                    // if hihat closing happens while hihat open sound is in silence
                    if (IsHiHatOpen && Layer.HasHiHatClosed && CurrentHiHatDuration > 0)
                    {
                        CurrentHiHatDuration -= use2 ? result2 : chunkSize;
                        if (CurrentHiHatDuration < 0)
                            CurrentHiHatDuration = 0;
                    }

                    Array.Copy(new byte[chunkSize], 0, buffer, offset + bytesCopied, use2 ? result2 : chunkSize);

                    ByteInterval -= use2 ? result2 : chunkSize;
                    bytesCopied += use2 ? result2 : chunkSize;
                }
                else
                {
                    if (IsHiHatOpen && CurrentHiHatDuration == chunkSize)
                    {
                        HiHatOpenIsMuted = true;
                        CurrentHiHatDuration = 0;
                    }

                    ByteInterval -= result;
                    bytesCopied += result;
                }
                
            }

            if (IsHiHatOpen || IsHiHatClose) cycle++;
            return count;
        }

        /**<summary>Returns the file name of a sound from the pretty name.</summary>*/
        static public string GetFileByName(string name)
        {
            int length = FileNameIndex.Length;
            string[] flat = new string[length];
            flat = FileNameIndex.Cast<string>().ToArray();
            return flat[Array.IndexOf(flat, name) - 1];
        }

        static public string GetSelectorNameByFile(string fileName)
        {
            string[] flat = new string[FileNameIndex.Length];
            flat = FileNameIndex.Cast<string>().ToArray();
            int index = Array.IndexOf(flat, fileName);
            if (index > -1)
            {
                string selector = flat[index + 1];
                // append the index number
                index -= 2; // silentbeat
                index /= 2;
                index += 1;
                selector = (index + ".").PadRight(4) + selector;

                return selector;
            }

            return string.Empty;
        }

        static public string[,] FileNameIndex = new string[,]
        {
            { "wav/silence.wav", "silentbeat" },                                  //0
            { "wav/crash1_edge_v5.wav", "Crash Edge V1" },                        //1
            { "wav/crash1_edge_v8.wav", "Crash Edge V2" },                        //2
            { "wav/crash1_edge_v10.wav", "Crash Edge V3" },                       //3
            { "wav/floortom_v6.wav", "FloorTom V1" },                             //4
            { "wav/floortom_v11.wav", "FloorTom V2" },                            //5
            { "wav/floortom_v16.wav", "FloorTom V3" },                            //6
            { "wav/hihat_closed_center_v4.wav", "HiHat Closed Center V1" },       //7
            { "wav/hihat_closed_center_v7.wav", "HiHat Closed Center V2" },       //8
            { "wav/hihat_closed_center_v10.wav", "HiHat Closed Center V3" },      //9
            { "wav/hihat_closed_edge_v7.wav", "HiHat Closed Edge V1" },           //10
            { "wav/hihat_closed_edge_v10.wav", "HiHat Closed Edge V2" },          //11
            { "wav/hihat_half_center_v4.wav", "HiHat Half Center V1" },           //12
            { "wav/hihat_half_center_v7.wav", "HiHat Half Center V2" },           //13
            { "wav/hihat_half_center_v10.wav", "HiHat Half Center V3" },          //14
            { "wav/hihat_half_edge_v7.wav", "HiHat Half Edge V1" },               //15
            { "wav/hihat_half_edge_v10.wav", "HiHat Half Edge V2" },              //16
            { "wav/hihat_open_center_v4.wav", "HiHat Open Center V1" },           //17
            { "wav/hihat_open_center_v7.wav", "HiHat Open Center V2" },           //18
            { "wav/hihat_open_center_v10.wav", "HiHat Open Center V3" },          //19
            { "wav/hihat_open_edge_v7.wav", "HiHat Open Edge V1" },               //20
            { "wav/hihat_open_edge_v10.wav", "HiHat Open Edge V2" },              //21
            { "wav/hihat_pedal_v3.wav", "HiHat Pedal V1" },                       //22
            { "wav/hihat_pedal_v5.wav", "HiHat Pedal V2" },                       //23
            { "wav/kick_v7.wav", "Kick Drum V1" },                                //24
            { "wav/kick_v11.wav", "Kick Drum V2" },                               //25
            { "wav/kick_v16.wav", "Kick Drum V3" },                               //26
            { "wav/racktom_v6.wav", "RackTom V1" },                               //27
            { "wav/racktom_v11.wav", "RackTom V2" },                              //28
            { "wav/racktom_v16.wav", "RackTom V3" },                              //29
            { "wav/ride_bell_v5.wav", "Ride Bell V1" },                           //30
            { "wav/ride_bell_v8.wav", "Ride Bell V2" },                           //31
            { "wav/ride_bell_v10.wav", "Ride Bell V3" },                          //32
            { "wav/ride_center_v5.wav", "Ride Center V1" },                       //33
            { "wav/ride_center_v6.wav", "Ride Center V2" },                       //34
            { "wav/ride_center_v8.wav", "Ride Center V3" },                       //35
            { "wav/ride_center_v10.wav", "Ride Center V4" },                      //36
            { "wav/ride_edge_v4.wav", "Ride Edge V1" },                           //37
            { "wav/ride_edge_v7.wav", "Ride Edge V2" },                           //38
            { "wav/ride_edge_v10.wav", "Ride Edge V3" },                          //39
            { "wav/snare_center_v6.wav", "Snare Center V1" },                     //40
            { "wav/snare_center_v11.wav", "Snare Center V2" },                    //41
            { "wav/snare_center_v16.wav", "Snare Center V3" },                    //42
            { "wav/snare_edge_v6.wav", "Snare Edge V1" },                         //43
            { "wav/snare_edge_v11.wav", "Snare Edge V2" },                        //44
            { "wav/snare_edge_v16.wav", "Snare Edge V3" },                        //45
            { "wav/snare_rim_v6.wav", "Snare Rim V1" },                           //46
            { "wav/snare_rim_v11.wav", "Snare Rim V2" },                          //47
            { "wav/snare_rim_v16.wav", "Snare Rim V3" },                          //48
            { "wav/snare_xstick_v6.wav", "Snare XStick V1" },                     //49
            { "wav/snare_xstick_v11.wav", "Snare XStick V2" },                    //50
            { "wav/snare_xstick_v16.wav", "Snare XStick V3" },                    //51
        };

        void IStreamProvider.Dispose()
        {
            //memStream.Dispose();
            sourceStream.Dispose();
            Channel.Dispose();
            Dispose();
        }
    }
}
