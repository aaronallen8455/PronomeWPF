using System;
using System.Windows.Media;

namespace Pronome
{
    public class ColorHelper
    {
        /// <summary>
        /// A random number to offset the color wheel by.
        /// </summary>
        static int RgbSeed;

        static bool seedSet = false;

        /// <summary>
        /// Gets a color from color wheel based on index and rgbSeed.
        /// </summary>
        /// <param name="index">Index of the layer</param>
        /// <param name="saturation">Saturation value</param>
        /// <param name="RgbSeed">Amount to offset the wheel by</param>
        /// <returns></returns>
        public static Color ColorWheel(int index, float saturation = 1f)
        {
            if(!seedSet)
            {
                RgbSeed = Metronome.GetRandomNum();
                seedSet = true;
            }

            int degrees = ((25 * index) + (int)(360 * RgbSeed / 100)) % 360;
            byte min = (byte)(255 - 255 * saturation);
            int degreesMod = degrees == 0 ? 0 : degrees % 60 == 0 ? 60 : degrees % 60;
            float stepSize = (255 - min) / 60;
            byte red, green, blue;

            if (degrees <= 60)
            {
                red = 255;
                green = Convert.ToByte(min + Math.Round(degreesMod * stepSize));
                blue = min;
            }
            else if (degrees <= 120)
            {
                red = Convert.ToByte(255 - Math.Round(degreesMod * stepSize));
                green = 255;
                blue = min;
            }
            else if (degrees <= 180)
            {
                red = min;
                green = 255;
                blue = Convert.ToByte(min + Math.Round(degreesMod * stepSize));
            }
            else if (degrees <= 240)
            {
                red = min;
                green = Convert.ToByte(255 - Math.Round(degreesMod * stepSize));
                blue = 255;
            }
            else if (degrees <= 300)
            {
                red = Convert.ToByte(min + Math.Round(degreesMod * stepSize));
                green = min;
                blue = 255;
            }
            else
            {
                red = 255;
                green = min;
                blue = Convert.ToByte(255 - Math.Round(degreesMod * stepSize));
            }

            return new Color()
            {
                R = red,
                G = green,
                B = blue,
                A = 255,
            };
        }

        /// <summary>
        /// Get new colors the next time ColorWheel is called.
        /// </summary>
        static public void ResetRgbSeed()
        {
            seedSet = false;
        }
    }
}
