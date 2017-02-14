using System;
using System.Windows;
using System.Windows.Media;

namespace Pronome.Bounce
{
    class Tick
    {
        protected double ElapsedInterval = 0;
        protected bool IsComplete = false;

        protected double LeftDisplace; // the distance that left point needs to move inwards over course of animation.
        protected double RightDisplace;
        protected double LeftStart; // starting position of left endpoint
        protected double RightStart; // starting position of right endpoint

        static double leftSlope;
        static double apex;
        static double denominator;
        static double factor;

        protected Lane Lane;

        public Pen Pen;

        public Tick(
            double leftDisplace,
            double rightDisplace,
            double leftStart,
            double rightStart,
            Lane lane,
            Pen pen,
            double elapsedInterval = 0)
        {
            LeftDisplace = leftDisplace;
            RightDisplace = rightDisplace;
            Lane = lane;
            ElapsedInterval = elapsedInterval;
            LeftStart = leftStart - Ease(ElapsedInterval / Helper.TickQueueSize) * LeftDisplace;
            RightStart = rightStart - Ease(ElapsedInterval / Helper.TickQueueSize) * RightDisplace;
            Pen = pen;
        }

        public void Move(double timeChange, DrawingContext dc)
        {
            // add elapsed qtr notes to interval
            ElapsedInterval += timeChange * (Metronome.GetInstance().Tempo / 60);

            double fraction = ElapsedInterval / Helper.TickQueueSize;
            double transY = Ease(fraction);

            // move line up
            double yCoord = -Helper.divisionLine * transY;

            if (fraction >= 1)
            {
                // remove element when animation finished
                //Instance.drawingGroup.Children.Remove(GeoDrawing);
                if (!IsComplete)
                {
                    Lane.DequeueTick();
                }
                else IsComplete = true;
            }
            else
            {
                // reposition end points
                //Line.StartPoint = new Point(LeftStart + transY * LeftDisplace, Line.StartPoint.Y);
                //Line.EndPoint = new Point(RightStart + transY * RightDisplace, Line.EndPoint.Y);

                double y = (Helper.height - (Helper.divisionLine * transY)) * Helper.imageRatio + Helper.imageHeightPad;
                Point start = new Point((LeftStart + transY * LeftDisplace) * Helper.imageRatio + Helper.imageWidthPad, y);
                Point end = new Point((RightStart + transY * RightDisplace) * Helper.imageRatio + Helper.imageWidthPad, y);

                dc.DrawLine(Pen, start, end);
            }
        }

        static public void InitConstants()
        {
            leftSlope = Helper.divisionLine / Helper.widthPad;
            apex = leftSlope * (Helper.widthPad + Helper.width / 2);
            factor = -Math.Log(1 - (1 / (apex / Helper.divisionLine)), 2);
            denominator = -Math.Pow(2, -factor) + 1;
        }
        
        static public double Ease(double fraction)
        {
            if (Helper.widthPad <= 10)
            {
                return fraction;
            }

            return (-1 / (Math.Pow(2, fraction * factor)) + 1) / denominator; //2.3 for 1, 1.55 for 2
        }
    }
}
