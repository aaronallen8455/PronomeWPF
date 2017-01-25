using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for BounceWindow.xaml
    /// </summary>
    public partial class BounceWindow : Window
    {
        private int width;
        const double widthPad = 350; // difference in width between foreground and horizon
        public const int height = 900;
        private int layerCount;
        protected int[] layerIndexes;

        public static BounceWindow Instance;
        protected Ball[] Balls;
        protected AnimationTimer timer;

        const double ballRadius = 70;
        const double ballPadding = 20;
        const double horizon = height / 2 - ballRadius;

        public BounceWindow()
        {
            Instance = this;
            InitializeComponent();
        }

        public void DrawScene()
        {
            drawingGroup.Children.Clear();

            Metronome met = Metronome.GetInstance();
            layerCount = met.Layers.Count;
            Balls = new Ball[layerCount];
            layerIndexes = new int[layerCount];
            width = (int)(layerCount * (ballRadius * 2 + ballPadding * 2));

            // draw sizer element
            var size = new RectangleGeometry(new Rect(0, 0, width + 2 * widthPad, height));
            drawingGroup.Children.Add(new GeometryDrawing(Brushes.Transparent, new Pen(Brushes.White, 5), size));

            // draw lanes
            Pen lanePen = new Pen(Brushes.White, 3);
            var leftBound = new LineGeometry(new Point(0, height), new Point(widthPad, height / 2));
            drawingGroup.Children.Add(new GeometryDrawing(null, lanePen, leftBound));
            double xCoord = (width + 2 * widthPad) / layerCount;
            for (int i=0; i<layerCount; i++)
            {
                var start = new Point(xCoord * (i + 1), height);
                var end = new Point(widthPad + width/layerCount * (i + 1), height / 2);

                drawingGroup.Children.Add(new GeometryDrawing(null, lanePen, new LineGeometry(start, end)));
            }

            // draw horizon
            var horizonLine = new LineGeometry(new Point(0, height/2), new Point(width + 2 * widthPad, height/2));
            drawingGroup.Children.Add(new GeometryDrawing(null, new Pen(Brushes.Aqua, 1), horizonLine));

            // draw balls
            for (int i=0; i<layerCount; i++)
            {
                var ball = MakeBall(i);

                drawingGroup.Children.Add(ball);
            }

            Title = FindSlopeOfLanes().ToString();

            timer = new AnimationTimer();

            // make test line
            testLine = new LineGeometry(new Point(0, height), new Point(200, height));
            var testTrans = new TranslateTransform();
            testLine.Transform = testTrans;
            drawingGroup.Children.Add(new GeometryDrawing(null, new Pen(Brushes.Aqua, 3), testLine));

            CompositionTarget.Rendering += DrawFrame;
        }

        LineGeometry testLine;

        protected void DrawFrame(object sender, EventArgs e)
        {
            if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
            {
                double elapsed = timer.GetElapsedTime();

                MoveLine(testLine, elapsed);

                foreach (Ball ball in Balls)
                {
                    ball.SetPosition(elapsed);
                }
            }
            else
            {
                timer.Reset();
            }
        }
        // Draw the lanes and ticks on the face of a 3D plane and rotate the plane to match
        // y= (2sqrt(x) + x) / 3
        double elapsedTime = 0;
        double endTime = 3; // this should be a BPM value. show 6 quarter notes ?
        protected void MoveLine(LineGeometry line, double interval)
        {
            TranslateTransform transform = line.Transform as TranslateTransform;

            elapsedTime += interval;

            double fraction = elapsedTime / endTime;
            double transY = Math.Sqrt(fraction);

            transform.Y = -height / 2 * transY;

            //transform.Y = -elapsedTime * 159 + Math.Pow(elapsedTime * 3.5, 2);// - Math.Sqrt(elapsedTime/100) * 1500; //-Math.Log(elapsedTime) * 20;// + offset;
        }

        protected GeometryDrawing MakeBall(int index)
        {
            Point center = new Point(width / (layerCount * 2) * (index * 2 + 1) + widthPad, horizon);
            EllipseGeometry ball = new EllipseGeometry(center, ballRadius, ballRadius);
            Balls[index] = new Ball(index, ball);
            Color color = ColorHelper.ColorWheel(index);
            GeometryDrawing result = new GeometryDrawing(
                new SolidColorBrush(color),
                null, ball);//new Pen(Brushes.Red, 2), ball);

            return result;
        }

        protected double FindSlopeOfLanes()
        {
            var bottom = (width + 2 * widthPad) / layerCount;
            var top = width / layerCount;
            var diff = bottom - top;
            return (height / 2) / diff;
        }

        public bool KeepOpen = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (KeepOpen)
            {
                Hide();
                e.Cancel = true;

                CompositionTarget.Rendering -= DrawFrame;
            }
        }

        protected class Ball
        {
            protected int Index = 0;
            public EllipseGeometry Geometry;
            protected TranslateTransform Transform;
            protected Layer Layer;
            public double currentInterval = 0; // in seconds
            protected double countDown = 0; // in seconds
            const double defaultFactor = 4500;
            protected double factor = defaultFactor; // control the max height of the bounce

            public Ball(int index, EllipseGeometry geometry)
            {
                Geometry = geometry;
                Layer = Metronome.GetInstance().Layers[index];
                countDown += BpmToSec(Layer.Offset);
                AddSilence(); // account for silent beats at start
                currentInterval = countDown * 2; // put it at the apex
                Transform = new TranslateTransform();
                Geometry.Transform = Transform;
            }

            public void AddNext()
            {
                double bpm = 0;

                bpm += Layer.Beat[Index].Bpm;

                Index++;

                if (Index == Layer.Beat.Count) Index = 0;

                double silence = AddSilence();

                double time = BpmToSec(bpm);
                countDown += time;

                currentInterval = time + silence;

                // check if factor needs to be changed
                double halfInterval = currentInterval / 2;
                double bounceHeight = horizon - defaultFactor * (-halfInterval * halfInterval + currentInterval * halfInterval);
                if (bounceHeight < ballRadius)
                {
                    factor = (horizon - ballRadius) / (-halfInterval * halfInterval + currentInterval * halfInterval);
                }
                else
                {
                    factor = defaultFactor;
                }
            }

            public void SetPosition(double elapsedTime)
            {
                countDown -= elapsedTime;

                if (countDown <= 0)
                {
                    AddNext();
                }

                double total = currentInterval - countDown;
                
                Transform.Y = -factor * (-total * total + currentInterval * total);
            }

            protected double AddSilence()
            {
                double bpm = 0;

                while (Layer.Beat[Index].SourceName == WavFileStream.SilentSourceName)
                {
                    bpm += Layer.Beat[Index].Bpm;
                    Index++;
                }

                if (bpm > 0)
                {
                    double time = BpmToSec(bpm);
                    countDown += time;
                    return time;
                }

                return 0;
            }

            protected double BpmToSec(double bpm)
            {
                return bpm * (60 / Metronome.GetInstance().Tempo);
            }
        }
    }
}
