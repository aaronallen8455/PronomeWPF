﻿using NAudio.Wave;

namespace Pronome
{
    public interface IStreamProvider
    {
        bool IsPitch { get; }

        long GetNextInterval();

        double Volume { get; set; }

        /**<summary>The pan setting for this sound source. -1 to 1.</summary>*/
        float Pan { get; set; }

        double Frequency { get; set; }

        void Dispose();

        void Reset();

        int BytesPerSec { get; set; }

        /// <summary>
        /// Set the amount of offset in bytes
        /// </summary>
        /// <param name="value"></param>
        void SetOffset(double value);

        /// <summary>
        /// Get the amount of offset in bytes
        /// </summary>
        /// <returns></returns>
        double GetOffset();

        void SetSilentInterval(double audible, double silent);

        //void MultiplyByteInterval(double factor);

        void SetInitialMuting();

        Layer Layer { get; set; }

        SourceBeatCollection BeatCollection { get; set; }

        WaveFormat WaveFormat { get; }
    }
}
