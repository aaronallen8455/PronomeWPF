using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Pronome
{
    public class SourceBeatCollection : IEnumerable<long>
    {
        public IStreamProvider Source;
        double[] Beats;
        double[] Bpm;
        public IEnumerator<long> Enumerator;
        public bool isWav;

        public SourceBeatCollection(double[] beats, IStreamProvider src)
        {
            Source = src;
            Bpm = beats;
            ConvertBpmValues();
            //Beats = beats.Select((x) => BeatCell.ConvertFromBpm(x, src)).ToArray();
            isWav = !src.SoundSource.IsPitch;
            Enumerator = Beats.Length == 1 && Beats[0] == 0 ? null : GetEnumerator();
        }

        public IEnumerator<long> GetEnumerator()
        {
            for (int i = 0; ; i++)
            {
                if (i == Beats.Count()) i = 0; // loop over collection

                double bpm = Beats[i];//BeatCell.ConvertFromBpm(Beats[i], BytesPerSec);

                long whole = (long)bpm;

                Source.SampleRemainder += bpm - whole; // add to layer's remainder accumulator

                if (Source.SampleRemainder >= 1)
                {
                    int rounded = (int)Source.SampleRemainder;
                    whole += rounded;
                    Source.SampleRemainder -= rounded;
                }

                if (isWav)
                {
                    whole *= Source.BlockAlignment; // multiply for wav files. 4 bytes per sample
                }

                yield return whole;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        //double _factor;
        //public void MultiplyBeatValues(double factor)
        //{
        //    _factor = factor;
        //}
        public void ConvertBpmValues()
        {
            //if (_factor > 0)
            //    Beats = Beats.Select(x => x * _factor).ToArray();
            Beats = Bpm.Select((x) => BeatCell.ConvertFromBpm(x, Source)).ToArray();
        }
    }
}
