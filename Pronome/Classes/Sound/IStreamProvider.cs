using NAudio.Wave;

namespace Pronome
{
    public interface IStreamProvider
    {
        //bool IsPitch { get; }
        /// <summary>
        /// Contains info about the source.
        /// </summary>
        ISoundSource SoundSource { get; set; }

        long GetNextInterval();

        double Volume { get; set; }

        /**<summary>The pan setting for this sound source. -1 to 1.</summary>*/
        float Pan { get; set; }

        double Frequency { get; set; }

        void Dispose();

        void Reset();

        int BytesPerSec { get; }

        int BlockAlignment { get; }

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

        bool IsSilentIntervalSilent(long interval);

        void SetInitialMuting();

        /// <summary>
        /// The partial sample that are accumulated and added back in when >= 1
        /// </summary>
        double SampleRemainder { get; set; }

        Layer Layer { get; set; }

        SourceBeatCollection BeatCollection { get; set; }

        WaveFormat WaveFormat { get; }

        void MultiplyByteInterval();

        bool ProduceBytes { get; set; }
    }
}
