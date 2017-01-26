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

            timer = new AnimationTimer();

            CompositionTarget.Rendering += DrawFrame;
        }

        protected void DrawFrame(object sender, EventArgs e)
        {
            if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
            {
                double elapsed = timer.GetElapsedTime();

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

        protected class Lane
        {
            protected double LaneWidth = 0;

            protected Layer Layer; // the layer represented by this lane

            protected double LeftXDiff; // difference in X coords of the two left end points
            protected double RightXDiff;
            protected int Index;

            protected Queue<Tick> Ticks;

            protected double CurInterval; // when this is zero, it's time to cue a new tick
            protected int beatIndex = 0;

            public Lane(Layer layer, double leftXDiff, double rightXDiff, int index)
            {
                Layer = layer;
                LeftXDiff = leftXDiff;
                RightXDiff = rightXDiff;
                Index = index;

                double _width = (widthPad * 2 + Instance.width) / Instance.layerCount;
                LaneWidth = _width;

                // generate initial ticks and find current interval
                double accumulator = Layer.Offset;
                while (accumulator <= Tick.EndPoint)
                {
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                    accumulator += Layer.Beat[beatIndex].Bpm;
                    if (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                    {
                        beatIndex++;
                        continue;
                    }

                    // create tick
                    if (accumulator <= Tick.EndPoint)
                    {
                        double factor = Math.Sqrt((Tick.EndPoint - accumulator) / Tick.EndPoint);
                        var startPoint = new Point(_width * index + factor * leftXDiff, height);
                        var endPoint = new Point(_width * (index + 1) + factor * rightXDiff, height);
                        var line = new LineGeometry(startPoint, endPoint);
                        var lineTrans = new TranslateTransform(0, -height / 2 * factor);
                        line.Transform = lineTrans;
                        var geoDrawing = new GeometryDrawing(null, new Pen(Brushes.White, 2), line);
                        Instance.drawingGroup.Children.Add(geoDrawing);

                        Ticks.Enqueue(new Tick(leftXDiff, rightXDiff, line, geoDrawing, this, Tick.EndPoint - accumulator));
                    }
                    else
                    {
                        accumulator -= Layer.Beat[beatIndex].Bpm;
                    }

                    beatIndex++;
                }
                if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                // get current interval.
                CurInterval = Layer.Beat[beatIndex - 1].Bpm - (Tick.EndPoint - accumulator);
            }

            public void DequeueTick()
            {
                Tick t = Ticks.Dequeue();
            }

            public void ProcFrame(double elapsedTime)
            {
                // animate ticks
                foreach (Tick tick in Ticks.ToArray())
                {
                    tick.Move(elapsedTime);
                }

                CurInterval -= elapsedTime * (Metronome.GetInstance().Tempo / 60);

                // queue new tick if its time
                if (CurInterval <= 0)
                {
                    //if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                    double factor = Math.Sqrt(-CurInterval) / Tick.EndPoint;
                    var startPoint = new Point(LaneWidth * Index + factor * LeftXDiff, height);
                    var endPoint = new Point(LaneWidth * (Index + 1) + factor * RightXDiff, height);
                    var line = new LineGeometry(startPoint, endPoint);
                    var lineTrans = new TranslateTransform(0, -height / 2 * factor);
                    line.Transform = lineTrans;
                    var geoDrawing = new GeometryDrawing(null, new Pen(Brushes.White, 2), line);
                    Instance.drawingGroup.Children.Add(geoDrawing);

                    Ticks.Enqueue(new Tick(LeftXDiff, RightXDiff, line, geoDrawing, this, CurInterval + Tick.EndPoint));

                    do
                    {
                        CurInterval += Layer.Beat[beatIndex].Bpm;
                        beatIndex++;
                        if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                    }
                    while (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName);
                }
            }
        }

        protected class Tick
        {
            public const double EndPoint = 6; // number of qtr notes to show. Duration of animation for each tick

            protected double ElapsedInterval = 0;

            protected double LeftDisplace; // the distance that left point needs to move inwards over course of animation.
            protected double RightDisplace;

            protected GeometryDrawing GeoDrawing;
            protected TranslateTransform LineTranslate;
            protected LineGeometry Line;

            protected Lane Lane;

            public Tick(
                double leftDisplace,
                double rightDisplace, 
                LineGeometry line, 
                GeometryDrawing geoDrawing,
                Lane lane,
                double elapsedInterval = 0)
            {
                LeftDisplace = leftDisplace;
                RightDisplace = rightDisplace;
                Line = line;
                LineTranslate = line.Transform as TranslateTransform;
                GeoDrawing = geoDrawing;
                Lane = lane;
                ElapsedInterval = elapsedInterval;
            }

            public void Move(double timeChange)
            {
                // add elapsed qtr notes to interval
                ElapsedInterval = timeChange * (Metronome.GetInstance().Tempo / 60);

                double fraction = ElapsedInterval / EndPoint;
                double transY = Math.Sqrt(fraction);

                // move line up
                LineTranslate.Y = -height / 2 * transY;

                if (fraction >= 1)
                {
                    // remove element when animation finished
                    Instance.drawingGroup.Children.Remove(GeoDrawing);
                    Lane.DequeueTick();
                }
                else
                {
                    // reposition end points

                }
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
