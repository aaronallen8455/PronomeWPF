using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows;

namespace Pronome
{
    class BeatGraph
    {
        public const double graphRadius = 500;

        const double twoPi = Math.PI * 2;

        public static double tickSize;

        public static double cycleLength;

        /**
         * <summary>Get an array of array of Point objects describing the tick marks for the beatcells in each layer.</summary>
         */
        static public BeatGraphLayer[] DrawGraph()
        {
            Metronome met = Metronome.GetInstance();

            cycleLength = met.GetQuartersForCompleteCycle();
            // todo: check symetry.

            int layerCount = met.Layers.Count;

            tickSize = graphRadius / (layerCount + 1);

            var result = new BeatGraphLayer[layerCount];

            // draw each layer
            for (int i=0; i<layerCount; i++)
            {
                // calculate the radius of ring
                double r = graphRadius / (layerCount + 1) * (i + 1);

                result[i] = getTickPoints(r, met.Layers[i], cycleLength);
            }

            return result;
        }

        /**<summary>Get the tick mark endpoints for each beat cell in a layer</summary>
         * <param name="cycleLength">The total beat cycle length</param>
         * <param name="layer">The layer to process</param>
         * <param name="radius">The overall radius of the graph</param>
         */
        static protected BeatGraphLayer getTickPoints(double radius, Layer layer, double cycleLength)
        {
            List<Point> points = new List<Point>();

            int timesRepeated = (int)Math.Round(cycleLength / layer.GetTotalBpmValue());

            // convert beatcells to degrees. Silent beats are a negative value
            double[] angles = layer.Beat.Select(x => {
                double result = x.Bpm / cycleLength * twoPi;
                if (x.SoundSource != null && x.SoundSource.Uri == WavFileStream.SilentSourceName)
                {
                    return result * -1;
                }
                return result;
                }
            ).ToArray();

            double angleAccumulator = layer.Offset / cycleLength * twoPi;

            // check if first beat is silent and to start position if so
            //if (angles[0] < 0) angleAccumulator -= angles[0];

            // draw the tick marks
            for (int i=0; i<timesRepeated; i++)
            {
                foreach (double angle in angles)
                {
                    // don't graph silent beats.
                    if (angle < 0) {
                        angleAccumulator -= angle;
                        continue;
                    }
                    // coordinates of point on circle
                    double xLeg = Math.Sin(angleAccumulator) * radius;
                    double yLeg = Math.Cos(angleAccumulator) * radius * -1;

                    // normalize the vector
                    double normX = xLeg / radius;
                    double normY = yLeg / radius;

                    // get line points
                    double centering = graphRadius;
                    Point inner = new Point(
                        xLeg - normX + centering,
                        yLeg - normY + centering
                    );
                    Point outer = new Point(
                        xLeg + normX * tickSize + centering,
                        yLeg + normY * tickSize + centering
                    );

                    points.Add(inner);
                    points.Add(outer);

                    // timeout if beat is asymmetrical
                    if (points.Count > 1000) throw new TimeoutException();

                    angleAccumulator += angle;
                }
            }

            return new BeatGraphLayer(points.ToArray(), radius);
        }
    }

    public struct BeatGraphLayer
    {
        public Point[] Ticks;
        public double Radius;

        public BeatGraphLayer(Point[] ticks, double radius)
        {
            Ticks = ticks;
            Radius = radius;
        }
    }
}
