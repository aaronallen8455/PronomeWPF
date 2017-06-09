using System.Windows;
using System.Windows.Media;

namespace Pronome.Bounce
{
    public class Ball
    {
        protected Helper Helper;
        protected int Index = 0;
        //public EllipseGeometry Geometry;
        //protected TranslateTransform Transform;
        protected Layer Layer;
        public double currentInterval = 0; // length in quarters of the current beat interval
        protected double countDown = 0; // quarter notes remaining before going to next interval
        protected double defaultFactor; //4500
        protected float currentTempo;
        protected double factor;// = defaultFactor; // control the max height of the bounce
        protected RadialGradientBrush gradient;
        protected double XOffset;
        double factorImageRatio; // factor * imageRatio
        double factorImageRatioCurInterval; // factorImageRatio * currentInterval
        double ballBaseImageRatioPad; // bb * ir + hpad

        public Ball(int index, RadialGradientBrush grad, double xOffset, DrawingContext dc, Helper helper)
        {
            Layer = Metronome.GetInstance().Layers[index];
            countDown += Layer.Offset;//BpmToSec(Layer.Offset);
            AddSilence(); // account for silent beats at start
            currentInterval = countDown * 2; // put it at the apex
                                             //Transform = new TranslateTransform();
                                             //Geometry.Transform = Transform;
            gradient = grad;
            XOffset = xOffset;
            Helper = helper;

            currentTempo = Metronome.GetInstance().Tempo;
            defaultFactor = 1000 * (120 / Metronome.GetInstance().Tempo);
            SetFactor();
            ballBaseImageRatioPad = Helper.ballBase * Helper.imageRatio + Helper.imageHeightPad;
            SetPosition(0, dc);
        }

        public void AddNext()
        {
            if (Metronome.GetInstance().Tempo != currentTempo)
            {
                defaultFactor = 1000 * (120 / Metronome.GetInstance().Tempo);
                currentTempo = Metronome.GetInstance().Tempo;
            }

            double bpm = 0;

            bpm += Layer.Beat[Index].Bpm;

            Index++;

            if (Index == Layer.Beat.Count) Index = 0;

            double silence = AddSilence();

            double time = bpm;//BpmToSec(bpm);
            countDown += time;

            currentInterval = time + silence;

            // check if factor needs to be changed
            SetFactor();
        }

        public void SetPosition(double elapsedTime, DrawingContext dc)
        {
            countDown -= elapsedTime * (Metronome.GetInstance().Tempo / 60);

            if (countDown <= 0)
            {
                AddNext();
            }

            double total = currentInterval - countDown;

            //Transform.Y = -factor * (-total * total + currentInterval * total);

            double y = ballBaseImageRatioPad - factorImageRatio * -total * total - factorImageRatioCurInterval * total;

            dc.DrawEllipse(
                gradient,
                null,
                new Point(XOffset, y),
                Helper.ballRadius, Helper.ballRadius);
        }

        protected double AddSilence()
        {
            double bpm = 0;

            while (Layer.Beat[Index].SoundSource.Uri == WavFileStream.SilentSourceName)
            {
                bpm += Layer.Beat[Index].Bpm;
                Index++;
            }

            if (bpm > 0)
            {
                double time = bpm;//BpmToSec(bpm);
                countDown += time;
                return time;
            }

            return 0;
        }

        public void Sync(double elapsedBpm, DrawingContext dc)
        {
            elapsedBpm -= Layer.Offset;
            elapsedBpm = elapsedBpm % Layer.GetTotalBpmValue();

            double bpm = 0;
            double curIntv = 0;
            Index = 0;

            // catch up with elapsed bpm
            //if (Index == Layer.Beat.Count) Index = 0;
            while (bpm <= elapsedBpm || Layer.Beat[Index].SoundSource.Uri == WavFileStream.SilentSourceName)
            {

                if (Layer.Beat[Index].SoundSource.Uri == WavFileStream.SilentSourceName)
                {
                    curIntv += Layer.Beat[Index].Bpm; // count consecutive silences as one interval
                }
                else
                {
                    curIntv = Layer.Beat[Index].Bpm;
                }

                bpm += Layer.Beat[Index].Bpm;
                Index++;
                if (Index == Layer.Beat.Count) Index = 0;
            }

            currentInterval = curIntv;
            countDown = bpm - elapsedBpm;

            // set the current interval and countdown interval appropriately
            SetFactor();
            SetPosition(0, dc);
        }

        protected void SetFactor()
        {
            double halfInterval = currentInterval / 2;
            double bounceHeight = Helper.ballBase - defaultFactor * (-halfInterval * halfInterval + currentInterval * halfInterval);
            if (bounceHeight < Helper.ballRadius)
            {
                factor = (Helper.ballBase - Helper.ballRadius) / (-halfInterval * halfInterval + currentInterval * halfInterval);
            }
            else
            {
                factor = defaultFactor;
            }
            factorImageRatio = factor * Helper.imageRatio;
            factorImageRatioCurInterval = factorImageRatio * currentInterval;
        }
    }
}
