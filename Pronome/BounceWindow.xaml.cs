using System;
using System.Collections.Generic;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for BounceWindow.xaml
    /// </summary>
    public partial class BounceWindow : Window
    {
        /// <summary>
        /// The width of the ball area
        /// </summary>
        private int width;

        /// <summary>
        /// The amount of space between the edge of the screen and the bounds of the ball area.
        /// </summary>
        public static double widthPad = 350;//350; // difference in width between foreground and horizon

        /// <summary>
        /// The height of the drawing.
        /// </summary>
        public const int height = 900;

        /// <summary>
        /// Where the screen is divided between the lanes area and the ball area. 0 to 1.
        /// </summary>
        public static double divisionPoint = .38;

        /// <summary>
        /// The number of layers in the beat
        /// </summary>
        private int layerCount;

        /// <summary>
        /// Holds the Lane objects.
        /// </summary>
        protected Lane[] Lanes;

        /// <summary>
        /// The window instance.
        /// </summary>
        public static BounceWindow Instance;

        /// <summary>
        /// Is true if the window is not hidden.
        /// </summary>
        public bool SceneDrawn = false;

        /// <summary>
        /// Holds the ball objects
        /// </summary>
        protected Ball[] Balls;

        /// <summary>
        /// Timer for syncing animation
        /// </summary>
        protected AnimationTimer timer;

        /// <summary>
        /// True if beat is not playing.
        /// </summary>
        protected bool IsStopped = true;

        /// <summary>
        /// The radius of the ball drawings.
        /// </summary>
        const double ballRadius = 70;

        /// <summary>
        /// The amount of padding on the left of right of each ball.
        /// </summary>
        const double ballPadding = 20;

        /// <summary>
        /// The unit position of the line that seperates the lanes and balls.
        /// </summary>
        protected static double divisionLine = height / (1 / divisionPoint);

        /// <summary>
        /// The position of the center line of the balls when at base level.
        /// </summary>
        protected static double ballBase = height - divisionLine - ballRadius;

        public BounceWindow()
        {
            Instance = this;
            InitializeComponent();
            Metronome.AfterBeatParsed += new EventHandler(DrawScene);
        }

        protected void DrawScene(object sender, EventArgs e)
        {
            if (SceneDrawn)
            {
                DrawScene();
            }
        }

        /// <summary>
        /// Draw the scene components
        /// </summary>
        public void DrawScene()
        {
            Metronome met = Metronome.GetInstance();
            if (met.Layers.Count == 0) return; // do nothing if beat is empty

            // remove existing graphics
            drawingGroup.Children.Clear();

            layerCount = met.Layers.Count;
            Balls = new Ball[layerCount];
            Lanes = new Lane[layerCount];
            width = (int)(layerCount * (ballRadius * 2 + ballPadding * 2));
            divisionLine = height / (1 / divisionPoint);
            ballBase = height - divisionLine - ballRadius;

            // draw sizer element
            var size = new RectangleGeometry(new Rect(0, 0, width + 2 * widthPad, height));
            drawingGroup.Children.Add(new GeometryDrawing(Brushes.Transparent, null, size));

            // draw lanes
            DrawLanes();

            // draw horizon
            //var horizonLine = new LineGeometry(new Point(0, height/2), new Point(width + 2 * widthPad, height/2));
            //drawingGroup.Children.Add(new GeometryDrawing(null, new Pen(Brushes.Aqua, 2), horizonLine));

            // draw balls
            for (int i=0; i<layerCount; i++)
            {
                var ball = MakeBall(i);

                drawingGroup.Children.Add(ball);
            }

            // Animate

            if (Metronome.GetInstance().PlayState != Metronome.State.Stopped)
            {
                // set the initial position (if beat was playing before graph opened)
                if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
                {
                    Metronome.GetInstance().UpdateElapsedQuarters();
                }

                // sync balls and lanes to elapsed time
                foreach (Ball ball in Balls)
                {
                    ball.Sync(Metronome.GetInstance().ElapsedQuarters);
                }

                foreach (Lane lane in Lanes)
                {
                    lane.Sync(Metronome.GetInstance().ElapsedQuarters);
                }
            }

            timer = new AnimationTimer();

            // attach to frame rendering event
            CompositionTarget.Rendering += DrawFrame;

            SceneDrawn = true;
        }

        /// <summary>
        /// Make the lane drawing and instantiate Lane objects for each layer.
        /// </summary>
        protected void DrawLanes()
        {
            Pen lanePen = new Pen(Brushes.White, 3);
            var leftBound = new LineGeometry(new Point(0, height), new Point(widthPad, height - divisionLine));
            drawingGroup.Children.Add(new GeometryDrawing(null, lanePen, leftBound));
            double xCoord = (width + 2 * widthPad) / layerCount;
            for (int i = 0; i < layerCount; i++)
            {
                var start = new Point(xCoord * (i + 1), height);
                var end = new Point(widthPad + width / layerCount * (i + 1), height - divisionLine);

                drawingGroup.Children.Add(new GeometryDrawing(null, lanePen, new LineGeometry(start, end)));

                Lanes[i] = new Lane(Metronome.GetInstance().Layers[i], ColorHelper.ColorWheel(i),
                    widthPad + (width / layerCount) * i - xCoord * i,
                    widthPad + (width / layerCount) * (i + 1) - xCoord * (i + 1), i);
            }
        }

        /// <summary>
        /// Handle the frame render event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void DrawFrame(object sender, EventArgs e)
        {
            if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
            {
                double elapsed = timer.GetElapsedTime();

                foreach (Ball ball in Balls)
                {
                    ball.SetPosition(elapsed);
                }

                foreach (Lane lane in Lanes)
                {
                    lane.ProcFrame(elapsed);
                }

                IsStopped = false;
            }
            else if (Metronome.GetInstance().PlayState == Metronome.State.Paused)
            {
                timer.Reset();
            }
            else if (Metronome.GetInstance().PlayState == Metronome.State.Stopped)
            {
                timer.Reset();

                if (!IsStopped)
                {
                    DrawScene();
                }
                IsStopped = true;
            }
        }

        /// <summary>
        /// Create a ball drawing.
        /// </summary>
        /// <param name="index">Index of layer</param>
        /// <returns>Ball geometry</returns>
        protected GeometryDrawing MakeBall(int index)
        {
            Point center = new Point(width / (layerCount * 2) * (index * 2 + 1) + widthPad, ballBase);
            EllipseGeometry ball = new EllipseGeometry(center, ballRadius, ballRadius);
            Balls[index] = new Ball(index, ball);
            Color color = ColorHelper.ColorWheel(index);
            GeometryDrawing result = new GeometryDrawing(
                new RadialGradientBrush(color, Colors.Transparent) {
                    GradientStops = new GradientStopCollection(new GradientStop[] {
                        new GradientStop(color, .15),
                        new GradientStop(Colors.Black, 1.75)
                    }),
                    GradientOrigin = new Point(.25, .25),
                    Center = new Point(.25, .25),
                    ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation
                },//new SolidColorBrush(color),
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

                SceneDrawn = false;
                CompositionTarget.Rendering -= DrawFrame;
            }
        }

        /// <summary>
        /// A class to represent a lane.
        /// </summary>
        public class Lane
        {
            /// <summary>
            /// Thickness of the top tick
            /// </summary>
            const double topTickWidth = 3.5;

            /// <summary>
            /// Thickness of non-top tick
            /// </summary>
            const double normTickWidth = 2;

            /// <summary>
            /// Width of a lane at the base.
            /// </summary>
            protected double LaneWidth;

            protected Layer Layer; // the layer represented by this lane
            protected SolidColorBrush Brush;

            protected double LeftXDiff; // difference in X coords of the two left end points
            protected double RightXDiff;
            protected int Index;

            protected Queue<Tick> Ticks;

            protected double CurInterval; // when this is zero, it's time to cue a new tick
            protected int beatIndex = 0;

            public Lane(Layer layer, Color color, double leftXDiff, double rightXDiff, int index)
            {
                Layer = layer;
                Brush = new SolidColorBrush(color);
                LeftXDiff = leftXDiff;
                RightXDiff = rightXDiff;
                Index = index;

                double _width = (widthPad * 2 + Instance.width) / Instance.layerCount;
                LaneWidth = _width;

                Ticks = new Queue<Tick>();

                // generate initial ticks and find current interval
                InitTicks(Layer.Offset);
            }

            protected void InitTicks(double offset)
            {
                Tick.InitConstants(); // calculate the constants used for tick positioning

                double accumulator = offset;
                bool isFirst = true;
                while (accumulator <= Tick.EndPoint)
                {
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                    if (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                    {
                        while (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                        {
                            accumulator += Layer.Beat[beatIndex].Bpm;
                            beatIndex++;
                        }
                        continue;
                    }
                    // create tick
                    double factor = Tick.Ease((Tick.EndPoint - accumulator) / Tick.EndPoint);
                    var startPoint = new Point(LaneWidth * Index + factor * LeftXDiff, height);
                    var endPoint = new Point(LaneWidth * (Index + 1) + factor * RightXDiff, height);
                    var line = new LineGeometry(startPoint, endPoint);
                    var lineTrans = new TranslateTransform(0, -divisionLine * factor);
                    line.Transform = lineTrans;
                    var geoDrawing = new GeometryDrawing(null, 
                        new Pen(Brush, isFirst ? topTickWidth : normTickWidth), // make first tick thicker
                        line);
                    Instance.drawingGroup.Children.Add(geoDrawing);
                    isFirst = false;

                    var tick = new Tick(LeftXDiff, RightXDiff, line, geoDrawing, this, Tick.EndPoint - accumulator);

                    Ticks.Enqueue(tick);

                    accumulator += Layer.Beat[beatIndex].Bpm;

                    beatIndex++;
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                    if (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                    {
                        while (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                        {
                            accumulator += Layer.Beat[beatIndex].Bpm;
                            beatIndex++;
                            if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                        }

                    }
                }

                // get current interval.
                accumulator -= Layer.Beat[beatIndex == Layer.Beat.Count ? 0 : beatIndex].Bpm;
                CurInterval = Layer.Beat[beatIndex == Layer.Beat.Count ? 0 : beatIndex].Bpm - (Tick.EndPoint - accumulator);
            }

            public void DequeueTick()
            {
                Ticks.Dequeue();

                if (Ticks.Count != 0)
                {
                    Tick tick = Ticks.Peek();

                    if (tick != null)
                    {
                        tick.GeoDrawing.Pen.Thickness = topTickWidth;
                    }
                }

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

                    double factor = Tick.Ease(-CurInterval) / Tick.EndPoint;
                    var startPoint = new Point(LaneWidth * Index + factor * LeftXDiff, height);
                    var endPoint = new Point(LaneWidth * (Index + 1) + factor * RightXDiff, height);
                    var line = new LineGeometry(startPoint, endPoint);
                    var lineTrans = new TranslateTransform(0, -divisionLine * factor);
                    line.Transform = lineTrans;
                    var geoDrawing = new GeometryDrawing(null, 
                        new Pen(Brush, Ticks.Count == 0 ? topTickWidth : normTickWidth), 
                        line);
                    Instance.drawingGroup.Children.Add(geoDrawing);

                    Ticks.Enqueue(new Tick(LeftXDiff, RightXDiff, line, geoDrawing, this, -CurInterval));

                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                    do
                    {
                        CurInterval += Layer.Beat[beatIndex].Bpm;
                        beatIndex++;
                        if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                    }
                    while (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName);
                }
            }

            public void Sync(double elapsedBpm)
            {
                elapsedBpm = elapsedBpm % Layer.GetTotalBpmValue();
                beatIndex = 0;

                // Remove existing ticks
                foreach(Tick tick in Ticks)
                {
                    Instance.drawingGroup.Children.Remove(tick.GeoDrawing);
                }

                Ticks.Clear();

                // sync up to elapsed
                //if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                double bpm = 0;
                while (bpm <= elapsedBpm || Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                {
                    bpm += Layer.Beat[beatIndex].Bpm;
                    beatIndex++;
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                }
                //beatIndex--;
                //if (beatIndex == -1) beatIndex = Layer.Beat.Count - 1;

                // fill in initial ticks
                InitTicks(bpm - elapsedBpm);
            }
        }

        public class Tick
        {
            public static double EndPoint = 6; // number of qtr notes to show. Duration of animation for each tick

            protected double ElapsedInterval = 0;
            protected bool IsComplete = false;

            protected double LeftDisplace; // the distance that left point needs to move inwards over course of animation.
            protected double RightDisplace;
            protected double LeftStart;
            protected double RightStart;

            public GeometryDrawing GeoDrawing;
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
                LeftStart = line.StartPoint.X - Ease(ElapsedInterval / EndPoint) * LeftDisplace;
                RightStart = line.EndPoint.X - Ease(ElapsedInterval / EndPoint) * RightDisplace;
            }

            public void Move(double timeChange)
            {
                // add elapsed qtr notes to interval
                ElapsedInterval += timeChange * (Metronome.GetInstance().Tempo / 60);

                double fraction = ElapsedInterval / EndPoint;
                double transY = Ease(fraction);

                // move line up
                LineTranslate.Y = -divisionLine * transY;

                if (fraction >= 1)
                {
                    // remove element when animation finished
                    Instance.drawingGroup.Children.Remove(GeoDrawing);
                    if (!IsComplete)
                    {
                        Lane.DequeueTick();
                    }
                    else IsComplete = true;
                }
                else
                {
                    // reposition end points
                    Line.StartPoint = new Point(LeftStart + transY * LeftDisplace, Line.StartPoint.Y);
                    Line.EndPoint = new Point(RightStart + transY * RightDisplace, Line.EndPoint.Y);
                }
            }

            static public void InitConstants()
            {
                leftSlope = divisionLine / widthPad;
                apex = leftSlope * (widthPad + Instance.width / 2);
                factor = -Math.Log(1 - (1 / (apex / divisionLine)), 2);
                denominator = -Math.Pow(2, -factor) + 1;
            }

            static double leftSlope;
            static double apex;
            static double denominator;
            static double factor;
            static public double Ease(double fraction)
            {
                if (widthPad == 0)
                {
                    return fraction;
                }

                //var input = fraction * (1 - startingVal) + startingVal;
                // find vanishing point

                // init these values
                //if (apex == default(double))
                //{
                //}
                //if (shim == default(double))
                //{
                //    shim = Math.Sqrt(startingVal * (height / 2) / apex);
                //}
                //if (ratio == default(double))
                //{
                //    ratio = Math.Sqrt(height / 2 / apex) - shim;
                //}

                //var position = Math.Sqrt(input * (height / 2) / apex) - shim;

                //return position / ratio;
                // 1 / (-Math.Pow(2, x) + 1) = apex / .5height
                // -2^-x + 1
                return (-1 / (Math.Pow(2, fraction*factor))+1) / denominator; //2.3 for 1, 1.55 for 2
            }
        }

        protected class Ball
        {
            protected int Index = 0;
            public EllipseGeometry Geometry;
            protected TranslateTransform Transform;
            protected Layer Layer;
            public double currentInterval = 0; // length in quarters of the current beat interval
            protected double countDown = 0; // quarter notes remaining before going to next interval
            protected double defaultFactor; //4500
            protected float currentTempo;
            protected double factor;// = defaultFactor; // control the max height of the bounce

            public Ball(int index, EllipseGeometry geometry)
            {
                Geometry = geometry;
                Layer = Metronome.GetInstance().Layers[index];
                countDown += Layer.Offset;//BpmToSec(Layer.Offset);
                AddSilence(); // account for silent beats at start
                currentInterval = countDown * 2; // put it at the apex
                Transform = new TranslateTransform();
                Geometry.Transform = Transform;

                currentTempo = Metronome.GetInstance().Tempo;
                defaultFactor = 1000 * (120 / Metronome.GetInstance().Tempo);
                SetFactor();
                SetPosition(0);
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

            public void SetPosition(double elapsedTime)
            {
                countDown -= elapsedTime * (Metronome.GetInstance().Tempo / 60);

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
                    double time = bpm;//BpmToSec(bpm);
                    countDown += time;
                    return time;
                }

                return 0;
            }

            public void Sync(double elapsedBpm)
            {
                elapsedBpm = elapsedBpm % Layer.GetTotalBpmValue();

                double bpm = 0;
                double curIntv = 0;
                Index = 0;

                // catch up with elapsed bpm
                //if (Index == Layer.Beat.Count) Index = 0;
                while (bpm <= elapsedBpm || Layer.Beat[Index].SourceName == WavFileStream.SilentSourceName)
                {

                    if (Layer.Beat[Index].SourceName == WavFileStream.SilentSourceName)
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
                SetPosition(0);
            }

            protected void SetFactor()
            {
                double halfInterval = currentInterval / 2;
                double bounceHeight = ballBase - defaultFactor * (-halfInterval * halfInterval + currentInterval * halfInterval);
                if (bounceHeight < ballRadius)
                {
                    factor = (ballBase - ballRadius) / (-halfInterval * halfInterval + currentInterval * halfInterval);
                }
                else
                {
                    factor = defaultFactor;
                }
            }
        }
    }
}
