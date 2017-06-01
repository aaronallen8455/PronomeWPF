using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Pronome
{
    public class SourceBeatCollection : IEnumerable<long>
    {
        Layer Layer;
        public IStreamProvider Source;
        double[] Beats;
        double[] Bpm;
        public IEnumerator<long> Enumerator;
        public bool isWav;

        public SourceBeatCollection(Layer layer, double[] beats, IStreamProvider src)
        {
            Layer = layer;
            Source = src;
            Bpm = beats;
            ConvertBpmValues();
            //Beats = beats.Select((x) => BeatCell.ConvertFromBpm(x, src)).ToArray();
            Enumerator = Beats.Length == 1 && Beats[0] == 0 ? null : GetEnumerator();
            isWav = !src.IsPitch;
        }

        public IEnumerator<long> GetEnumerator()
        {
            for (int i = 0; ; i++)
            {
                if (i == Beats.Count()) i = 0; // loop over collection

                double bpm = Beats[i];//BeatCell.ConvertFromBpm(Beats[i], BytesPerSec);

                long whole = (long)bpm;

                Layer.Remainder += bpm - whole; // add to layer's remainder accumulator

                if (Layer.Remainder >= 1)
                {
                    int rounded = (int)Layer.Remainder;
                    whole += rounded;
                    Layer.Remainder -= rounded;
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
