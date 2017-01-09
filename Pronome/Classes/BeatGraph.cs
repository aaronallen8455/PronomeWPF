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

        /**
         * <summary>Get an array of array of Point objects describing the tick marks for the beatcells in each layer.</summary>
         */
        static public BeatGraphLayer[] DrawGraph()
        {
            Metronome met = Metronome.GetInstance();

            double cycleLength = met.GetQuartersForCompleteCycle();
            // todo: check symetry.

            int layerCount = met.Layers.Count;

            tickSize = graphRadius / layerCount / 2;

            var result = new BeatGraphLayer[layerCount];

            // draw each layer
            for (int i=0; i<layerCount; i++)
            {
                // calculate the radius of ring
                double r = graphRadius / layerCount * (i + 1);

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

            // convert beatcells to degrees
            double[] angles = layer.Beat.Select(x => x.Bpm / cycleLength * twoPi).ToArray();

            double angleAccumulator = layer.Offset / cycleLength * twoPi;

            // draw the tick marks
            for (int i=0; i<timesRepeated; i++)
            {
                foreach (double angle in angles)
                {
                    // coordinates of point on circle
                    double xLeg = Math.Sin(angleAccumulator) * radius;
                    double yLeg = Math.Cos(angleAccumulator) * radius * -1;

                    // normalize the vector
                    double normX = xLeg / radius;
                    double normY = yLeg / radius;

                    // get line points
                    double centering = graphRadius;
                    Point inner = new Point(
                        xLeg - normX * tickSize + centering, 
                        yLeg - normY * tickSize + centering
                    );
                    Point outer = new Point(
                        xLeg + normX * tickSize + centering,
                        yLeg + normY * tickSize + centering
                    );

                    points.Add(inner);
                    points.Add(outer);

                    angleAccumulator += angle;
                }
            }

            return new BeatGraphLayer(points.ToArray(), radius, new Point(graphRadius / 2, graphRadius / 2 + radius));
        }
    }

    public struct BeatGraphLayer
    {
        public Point[] Ticks;
        public double Radius;
        public Point InitialPoint;

        public BeatGraphLayer(Point[] ticks, double radius, Point initialPoint)
        {
            Ticks = ticks;
            Radius = radius;
            InitialPoint = initialPoint;
        }
    }
}
