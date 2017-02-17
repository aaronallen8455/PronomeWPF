using System;
using NAudio.Wave.SampleProviders;


namespace Pronome
{
    class StereoPanStrategy : IPanStrategy
    {
        /// <summary>
        /// Gets the left and right channel multipliers for this pan value
        /// </summary>
        /// <param name="pan">Pan value, between -1 and 1</param>
        /// <returns>Left and right multipliers</returns>
        public StereoSamplePair GetMultipliers(float pan)
        {
            float leftChannel = (pan <= 0) ? 1.0f : (float)Math.Sin(((1 - pan) / 2.0f) / 2 * Math.PI);
            //float leftChannel = (pan <= 0) ? 1.0f : 1 - pan*pan;
            float rightChannel = (pan >= 0) ? 1.0f : (float)Math.Sin(((pan + 1) / 2.0f) / 2 * Math.PI);
            //float rightChannel = (pan >= 0) ? 1.0f : 1 - pan*pan;
            return new StereoSamplePair() { Left = leftChannel, Right = rightChannel };
        }
    }
}
