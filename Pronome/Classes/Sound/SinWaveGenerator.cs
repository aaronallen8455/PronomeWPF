using System;
using System.Collections.Generic;
using System.Collections;

namespace Pronome
{
    public class SinWaveGenerator : IEnumerable<float>
    {

        private float SinBack2;
        private float SinBack1;
        private float TwoCosB;
        private float InitPhase;
        private float Freq;
        const double TwoPi = 2 * Math.PI;

        public SinWaveGenerator(float initPhase, double freq)
        {
            InitPhase = initPhase;
            Freq = (float)freq;
            double b = TwoPi * Freq / 44100;
            TwoCosB = (float)(2 * Math.Cos(b));
            SinBack2 = (float)Math.Sin(InitPhase * b);
            SinBack1 = (float)Math.Sin((InitPhase + 1) * b);
        }

        public IEnumerator<float> GetEnumerator()
        {
            yield return SinBack2;

            yield return SinBack1;

            while (true)
            {
                float cur = TwoCosB * SinBack1 - SinBack2;
                SinBack2 = SinBack1;
                SinBack1 = cur;
                yield return cur;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}