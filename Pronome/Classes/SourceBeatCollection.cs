using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace Pronome
{
    public class SourceBeatCollection : IEnumerable<long>
    {
        Layer Layer;
        IStreamProvider Source;
        double[] Beats;
        double[] Bpm;
        public IEnumerator<long> Enumerator;
        public bool isWav;

        public SourceBeatCollection(Layer layer, double[] beats, IStreamProvider src)
        {
            Layer = layer;
            Source = src;
            Bpm = beats;
            Beats = beats.Select((x) => BeatCell.ConvertFromBpm(x, src)).ToArray();
            Enumerator = GetEnumerator();
            isWav = src.WaveFormat.AverageBytesPerSecond == 64000;
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

                if (isWav) whole *= 4; // multiply for wav files. 4 bytes per sample

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
        public void MultiplyBeatValues()
        {
            //if (_factor > 0)
            //    Beats = Beats.Select(x => x * _factor).ToArray();
            Beats = Bpm.Select((x) => BeatCell.ConvertFromBpm(x, Source)).ToArray();
        }
    }
}
