﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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
        const double baseBallRadius = 70;

        protected static double ballRadius;

        /// <summary>
        /// The amount of padding on the left of right of each ball.
        /// </summary>
        const double baseBallPadding = 20;

        protected static double ballPadding;

        /// <summary>
        /// The unit position of the line that seperates the lanes and balls.
        /// </summary>
        protected static double divisionLine = height / (1 / divisionPoint);

        /// <summary>
        /// The position of the center line of the balls when at base level.
        /// </summary>
        protected static double ballBase = height - divisionLine - ballRadius * 2;

        protected DrawingVisual Drawing = new DrawingVisual();

        protected static double imageRatio = 1;

        protected static double imageWidthPad = 0;

        protected static double imageHeightPad = 0;

        public BounceWindow()
        {
            Instance = this;
            InitializeComponent();
            //DrawScene();
            //this.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
            AddVisualChild(Drawing);
            AddLogicalChild(Drawing);
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
            ballRadius = baseBallRadius;
            ballPadding = baseBallPadding;

            layerCount = met.Layers.Count - met.LayersToChange.Count; // in case a field was changed as the bounce window opened.
            Balls = new Ball[layerCount];
            Lanes = new Lane[layerCount];
            width = (int)(layerCount * (ballRadius * 2 + ballPadding * 2));
            divisionLine = height / (1 / divisionPoint);
            ballBase = height - divisionLine - ballRadius * 2;

            // calculate imageRatio values
            SetImageRatio();

            using (DrawingContext dc = Drawing.RenderOpen())
            {
                // draw lanes
                DrawLanes(dc);

                // draw balls
                for (int i = 0; i < layerCount; i++)
                {
                    MakeBall(i, dc);
                }

                if (Metronome.GetInstance().PlayState != Metronome.State.Stopped)
                {
                    // set the initial position (if beat was playing before graph opened)
                    //if (Metronome.GetInstance().PlayState != Metronome.State.Paused)
                    //{
                        Metronome.GetInstance().UpdateElapsedQuarters();
                    //}

                    // sync balls and lanes to elapsed time
                    foreach (Ball ball in Balls)
                    {
                        ball.Sync(Metronome.GetInstance().ElapsedQuarters, dc);
                    }

                    foreach (Lane lane in Lanes)
                    {
                        lane.Sync(Metronome.GetInstance().ElapsedQuarters, dc);
                    }
                }
            }

            timer = new AnimationTimer();

            // attach to frame rendering event
            CompositionTarget.Rendering -= DrawFrame;
            CompositionTarget.Rendering += DrawFrame;

            SceneDrawn = true;
        }

        Point[,] laneEndPoints;
        Pen lanePen = new Pen(Brushes.LightGray, 2);

        /// <summary>
        /// Make the lane drawing and instantiate Lane objects for each layer.
        /// </summary>
        protected void DrawLanes(DrawingContext dc, bool instantiateLayers = true)
        {

            // init lane endpoints and lane objects
            if (instantiateLayers)
            {
                laneEndPoints = new Point[layerCount + 1, 2];

                laneEndPoints[0, 0] = new Point(0 + imageWidthPad, height * imageRatio + imageHeightPad);
                laneEndPoints[0, 1] = new Point(widthPad * imageRatio + imageWidthPad, (height - divisionLine) * imageRatio + imageHeightPad);

                double xCoord = (width + 2 * widthPad) / layerCount;
                // init each layer's lane endpoints
                for (int i = 1; i <= layerCount; i++)
                {
                    laneEndPoints[i, 0] = new Point(xCoord * i * imageRatio + imageWidthPad, height * imageRatio + imageHeightPad);
                    laneEndPoints[i, 1] = new Point((widthPad + width / layerCount * i) * imageRatio + imageWidthPad, (height - divisionLine) * imageRatio + imageHeightPad);

                    Lanes[i - 1] = new Lane(
                        Metronome.GetInstance().Layers[i - 1], ColorHelper.ColorWheel(i - 1),
                        (widthPad + (width / layerCount) * (i - 1) - xCoord * (i - 1)),
                        (widthPad + (width / layerCount) * i - xCoord * i),
                        i - 1, dc
                    );
                }
            }

            // draw lanes
            for (int i = 0; i <= layerCount; i++)
            {
                dc.DrawLine(lanePen, laneEndPoints[i, 0], laneEndPoints[i, 1]);
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

                using (DrawingContext dc = Drawing.RenderOpen())
                {
                    foreach (Ball ball in Balls)
                    {
                        ball.SetPosition(elapsed, dc);
                    }

                    foreach (Lane lane in Lanes)
                    {
                        lane.ProcFrame(elapsed, dc);
                    }

                    DrawLanes(dc, false);
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
        protected void MakeBall(int index, DrawingContext dc)
        {
            double xOffset = (width / (layerCount * 2) * (index * 2 + 1) + widthPad) * imageRatio + imageWidthPad - ballRadius;

            Color color = ColorHelper.ColorWheel(index);

            var gradient = new RadialGradientBrush(color, Colors.Transparent)
            {
                GradientStops = new GradientStopCollection(new GradientStop[] {
                        new GradientStop(color, .15),
                        new GradientStop(Colors.Black, 1.75)
                    }),
                GradientOrigin = new Point(.25, .25),
                Center = new Point(.25, .25),
                ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation
            };

            Balls[index] = new Ball(index, gradient, xOffset, dc);
        }

        /// <summary>
        /// Set the image ratio values based on window size
        /// </summary>
        protected void SetImageRatio()
        {
            double winWidth;
            double winHeight;

            //if (WindowState == WindowState.Maximized)
            //{
            //    winWidth = SystemParameters.PrimaryScreenWidth;
            //    winHeight = SystemParameters.PrimaryScreenHeight;
            //}
            //else
            //{
            winWidth = ActualWidth;
            winHeight = ActualHeight;
            //}

            if (height / (width + 2 * widthPad) < winHeight / winWidth)
            {
                imageRatio = (winWidth - 20) / (width + 2 * widthPad);

                imageHeightPad = (winHeight - 40 - height * imageRatio) / 2;
                imageWidthPad = 0;
            }
            else
            {
                imageRatio = (winHeight - 40) / height;

                imageWidthPad = (winWidth - 20 - (width + 2 * widthPad) * imageRatio) / 2;
                imageHeightPad = 0;
            }
            ballRadius = baseBallRadius * imageRatio;
            ballPadding = baseBallPadding * imageRatio;
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

            public Lane(Layer layer, Color color, double leftXDiff, double rightXDiff, int index, DrawingContext dc)
            {
                Layer = layer;
                Brush = new SolidColorBrush(color);
                LeftXDiff = leftXDiff;
                RightXDiff = rightXDiff;
                Index = index;

                LaneWidth = (widthPad * 2 + Instance.width) / Instance.layerCount;

                Ticks = new Queue<Tick>();

                // generate initial ticks and find current interval
                if (Metronome.GetInstance().PlayState != Metronome.State.Paused || !Instance.SceneDrawn)
                {
                    InitTicks(Layer.Offset, dc);
                }
            }

            /// <summary>
            /// Fill the queue with the first batch of ticks.
            /// </summary>
            /// <param name="offset">Amount of bpm before first tick</param>
            protected void InitTicks(double offset, DrawingContext dc)
            {
                Tick.InitConstants(); // calculate the constants used for tick positioning

                double accumulator = offset;
                bool isFirst = true;
                while (accumulator <= Tick.QueueSize)
                {
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                    if (Layer.Beat[beatIndex].SoundSource?.Uri == WavFileStream.SilentSourceName)
                    {
                        while (Layer.Beat[beatIndex].SoundSource?.Uri == WavFileStream.SilentSourceName)
                        {
                            accumulator += Layer.Beat[beatIndex].Bpm;
                            beatIndex++;
                        }
                        continue;
                    }
                    // create tick
                    double factor = Tick.Ease((Tick.QueueSize - accumulator) / Tick.QueueSize);
                    double leftStart = LaneWidth * Index + factor * LeftXDiff;
                    double rightStart = LaneWidth * (Index + 1) + factor * RightXDiff;
                    var startPoint = new Point(leftStart * imageRatio + imageWidthPad, (height - divisionLine * factor) * imageRatio + imageHeightPad);
                    var endPoint = new Point(rightStart * imageRatio + imageWidthPad, (height - divisionLine * factor) * imageRatio + imageHeightPad);
                    //var line = new LineGeometry(startPoint, endPoint);
                    //var lineTrans = new TranslateTransform(0, -divisionLine * factor);
                    //line.Transform = lineTrans;
                    //var geoDrawing = new GeometryDrawing(null, 
                    //    new Pen(Brush, isFirst ? topTickWidth : normTickWidth), // make first tick thicker
                    //    line);
                    //Instance.drawingGroup.Children.Add(geoDrawing);

                    dc.DrawLine(new Pen(Brush, isFirst ? topTickWidth : normTickWidth), startPoint, endPoint);

                    isFirst = false;

                    var tick = new Tick(LeftXDiff, RightXDiff, leftStart, rightStart, this, new Pen(Brush, isFirst ? topTickWidth : normTickWidth), Tick.QueueSize - accumulator);

                    Ticks.Enqueue(tick);

                    accumulator += Layer.Beat[beatIndex].Bpm;

                    beatIndex++;
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                    if (Layer.Beat[beatIndex].SoundSource?.Uri == WavFileStream.SilentSourceName)
                    {
                        while (Layer.Beat[beatIndex].SoundSource?.Uri == WavFileStream.SilentSourceName)
                        {
                            accumulator += Layer.Beat[beatIndex].Bpm;
                            beatIndex++;
                            if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                        }

                    }
                }

                // get current interval.
                accumulator -= Layer.Beat[beatIndex == Layer.Beat.Count ? 0 : beatIndex].Bpm;
                CurInterval = Layer.Beat[beatIndex == Layer.Beat.Count ? 0 : beatIndex].Bpm - (Tick.QueueSize - accumulator);
            }

            /// <summary>
            /// Remove the expired tick and shift highlight to new top beat.
            /// </summary>
            public void DequeueTick()
            {
                Ticks.Dequeue();

                if (Ticks.Count != 0)
                {
                    Tick tick = Ticks.Peek();

                    if (tick != null)
                    {
                        tick.Pen.Thickness = topTickWidth;
                    }
                }

            }

            /// <summary>
            /// Move ticks in queue, decrement the current interval, and add any new ticks.
            /// </summary>
            /// <param name="elapsedTime">Amount of time since last frame</param>
            public void ProcFrame(double elapsedTime, DrawingContext dc)
            {
                // animate ticks
                foreach (Tick tick in Ticks.ToArray())
                {
                    tick.Move(elapsedTime, dc);
                }

                CurInterval -= elapsedTime * (Metronome.GetInstance().Tempo / 60);

                // queue new tick if its time
                if (CurInterval <= 0)
                {
                    //if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                    double factor = Tick.Ease(-CurInterval) / Tick.QueueSize;
                    var startPoint = new Point(LaneWidth * Index + factor * LeftXDiff, height);
                    var endPoint = new Point(LaneWidth * (Index + 1) + factor * RightXDiff, height);
                    //var line = new LineGeometry(startPoint, endPoint);
                    //var lineTrans = new TranslateTransform(0, -divisionLine * factor);
                    //line.Transform = lineTrans;
                    //var geoDrawing = new GeometryDrawing(null, 
                    //    new Pen(Brush, Ticks.Count == 0 ? topTickWidth : normTickWidth), 
                    //    line);
                    //Instance.drawingGroup.Children.Add(geoDrawing);
                    Pen pen = new Pen(Brush, Ticks.Count == 0 ? topTickWidth : normTickWidth);

                    Ticks.Enqueue(new Tick(LeftXDiff, RightXDiff, startPoint.X, endPoint.X, this, pen, -CurInterval));

                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                    do
                    {
                        CurInterval += Layer.Beat[beatIndex].Bpm;
                        beatIndex++;
                        if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                    }
                    while (Layer.Beat[beatIndex].SoundSource?.Uri == WavFileStream.SilentSourceName);
                }
            }
            /// <summary>
            /// Set state of the drawing to match the position of the playing/paused beat.
            /// </summary>
            /// <param name="elapsedBpm">Number of Bpm that have elapsed.</param>
            public void Sync(double elapsedBpm, DrawingContext dc)
            {
                elapsedBpm -= Layer.Offset;
                elapsedBpm = elapsedBpm % Layer.GetTotalBpmValue();
                beatIndex = 0;

                Ticks.Clear();

                // sync up to elapsed
                double bpm = 0;
                while (bpm <= elapsedBpm || Layer.Beat[beatIndex].SoundSource?.Uri == WavFileStream.SilentSourceName)
                {
                    bpm += Layer.Beat[beatIndex].Bpm;
                    beatIndex++;
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                }

                // fill in initial ticks
                InitTicks(bpm - elapsedBpm, dc);
            }
        }

        public class Tick
        {
            public static double QueueSize = 6; // number of qtr notes to show. Duration of animation for each tick

            protected double ElapsedInterval = 0;
            protected bool IsComplete = false;

            protected double LeftDisplace; // the distance that left point needs to move inwards over course of animation.
            protected double RightDisplace;
            protected double LeftStart; // starting position of left endpoint
            protected double RightStart; // starting position of right endpoint

            double leftStartImageRatioPad; // LeftStart * imageRatio + imageWidthPad
            double leftDisplaceImageRatio; // LeftDisplace * imageRatio
            double rightStartImageRatioPad; // RightStart * imageRatio + imageWidthPad
            double rightDisplaceImageRatio; // RightDisplace * imageRatio

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
                LeftStart = leftStart - Ease(ElapsedInterval / QueueSize) * LeftDisplace;
                RightStart = rightStart - Ease(ElapsedInterval / QueueSize) * RightDisplace;
                Pen = pen;
                leftStartImageRatioPad = LeftStart * imageRatio + imageWidthPad;
                leftDisplaceImageRatio = LeftDisplace * imageRatio;
                rightStartImageRatioPad = RightStart * imageRatio + imageWidthPad;
                rightDisplaceImageRatio = RightDisplace * imageRatio;
            }

            public void Move(double timeChange, DrawingContext dc)
            {
                // add elapsed qtr notes to interval
                ElapsedInterval += timeChange * (Metronome.GetInstance().Tempo / 60);

                double fraction = ElapsedInterval / QueueSize;
                double transY = Ease(fraction);

                // move line up
                double yCoord = -divisionLine * transY;

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

                    double y = heightImageRatioPad - divisionLineImageRatio * transY;
                    Point start = new Point(leftStartImageRatioPad + transY * leftDisplaceImageRatio, y);
                    Point end = new Point(rightStartImageRatioPad + transY * rightDisplaceImageRatio, y);

                    dc.DrawLine(Pen, start, end);
                }
            }

            static public void InitConstants()
            {
                leftSlope = divisionLine / widthPad;
                apex = leftSlope * (widthPad + Instance.width / 2);
                factor = -Math.Log(1 - (1 / (apex / divisionLine)), 2);
                denominator = -Math.Pow(2, -factor) + 1;
                heightImageRatioPad = height * imageRatio + imageHeightPad;
                divisionLineImageRatio = divisionLine * imageRatio;
            }

            static double heightImageRatioPad; // height * imageRatio + heightPad
            static double divisionLineImageRatio; // divisionLine * imageRatio
            static double leftSlope;
            static double apex;
            static double denominator;
            static double factor;

            /// <summary>
            /// The math for caculating the position of the tick as it ascends.
            /// </summary>
            /// <param name="fraction"></param>
            /// <returns></returns>
            static public double Ease(double fraction)
            {
                if (widthPad <= 10)
                {
                    return fraction;
                }

                return (-1 / (Math.Pow(2, fraction * factor)) + 1) / denominator;
            }
        }

        protected class Ball
        {
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
            protected RenderTargetBitmap ballImage;
            protected double XOffset;

            public Ball(int index, RadialGradientBrush grad, double xOffset, DrawingContext dc)
            {
                //Geometry = geometry;
                Layer = Metronome.GetInstance().Layers[index];
                countDown += Layer.Offset;//BpmToSec(Layer.Offset);
                AddSilence(); // account for silent beats at start
                currentInterval = countDown * 2; // put it at the apex
                gradient = grad;
                XOffset = xOffset;

                currentTempo = Metronome.GetInstance().Tempo;
                defaultFactor = 1000 * (120 / Metronome.GetInstance().Tempo);
                SetFactor();
                ballBaseImageRatioPad = ballBase * imageRatio + imageHeightPad;
                
                ballImage = new RenderTargetBitmap((int)ballRadius * 2, (int)ballRadius * 2, 96, 96, PixelFormats.Pbgra32);

                var ball = new DrawingVisual();
                using (DrawingContext ctx = ball.RenderOpen())
                {
                    ctx.DrawEllipse(grad, null, new Point(ballRadius, ballRadius), ballRadius-1, ballRadius-1);
                }
                //t.CacheMode = new BitmapCache(1);

                ballImage.Render(ball);

                if (Metronome.GetInstance().PlayState != Metronome.State.Paused || !Instance.SceneDrawn)
                {
                    SetPosition(0, dc);
                }
            }

            public void AddNext()
            {
                if (Metronome.GetInstance().Tempo != currentTempo)
                {
                    defaultFactor = 1000 * (120 / Metronome.GetInstance().Tempo);
                    currentTempo = Metronome.GetInstance().Tempo;
                }

                double bpm = 0;

                //if (Index >= Layer.Beat.Count) Index %= Layer.Beat.Count;

                bpm += Layer.Beat[Index].Bpm;

                Index++;

                if (Index == Layer.Beat.Count) Index = 0;

                double silence = AddSilence();

                countDown += bpm;

                currentInterval = bpm + silence;
                // check if factor needs to be changed
                SetFactor();
            }

            double ballBaseImageRatioPad; // bb * ir + hpad

            /// <summary>
            /// Draw the ball at the correct position based on elapsed time since last frame.
            /// </summary>
            /// <param name="elapsedTime"></param>
            /// <param name="dc"></param>
            public void SetPosition(double elapsedTime, DrawingContext dc)
            {
                countDown -= elapsedTime * (Metronome.GetInstance().Tempo / 60);

                if (countDown <= 0)
                {
                    AddNext();
                }

                double total = currentInterval - countDown;

                double y = ballBaseImageRatioPad - factorImageRatio * -total * total - factorImageRatioCurInterval * total;

                dc.DrawImage(
                    ballImage, 
                    new Rect(XOffset, 
                    y, 
                    ballRadius * 2, 
                    ballRadius * 2));

                //dc.DrawRectangle(gradientBrush, null, new Rect(XOffset-ballRadius, y-ballRadius, ballRadius*2, ballRadius*2));
                //dc.DrawEllipse(
                //    gradientBrush,
                //    null,
                //    new Point(XOffset, y),
                //    ballRadius, ballRadius);

                //dc.DrawEllipse(
                //    gradient,
                //    null,
                //    new Point(XOffset, y),
                //    ballRadius, ballRadius);
            }

            double factorImageRatio; // factor * imageRatio
            double factorImageRatioCurInterval; // factorImageRatio * currentInterval

            /// <summary>
            /// Get the number of quarter notes of silence and forward the index pointer to the next non-silent cell.
            /// </summary>
            /// <returns></returns>
            protected double AddSilence()
            {
                double bpm = 0;

                while (Layer.Beat[Index].SoundSource?.Uri == WavFileStream.SilentSourceName)
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

            /// <summary>
            /// Sync the ball up with the elapsed time of the playback
            /// </summary>
            /// <param name="elapsedBpm"></param>
            /// <param name="dc"></param>
            public void Sync(double elapsedBpm, DrawingContext dc)
            {
                elapsedBpm -= Layer.Offset;
                elapsedBpm = elapsedBpm % Layer.GetTotalBpmValue();

                double bpm = 0;
                double curIntv = 0;
                Index = 0;

                // catch up with elapsed bpm
                while (bpm <= elapsedBpm || Layer.Beat[Index].SoundSource?.Uri == WavFileStream.SilentSourceName)
                {

                    if (Layer.Beat[Index].SoundSource?.Uri == WavFileStream.SilentSourceName)
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

            /// <summary>
            /// Set the factor which determines how high and how fast the ball will bounce based on interval size.
            /// </summary>
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
                factorImageRatio = factor * imageRatio;
                factorImageRatioCurInterval = factorImageRatio * currentInterval;
            }
        }

        protected override int VisualChildrenCount
        {
            get => 1;
        }

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException("index");

            return Drawing;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            DrawScene();
        }

        /// <summary>
        /// Enter full screen on ctrl-F
        /// </summary>
        /// <param name="e"></param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.F || e.SystemKey == Key.A
                && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (WindowStyle != WindowStyle.None)
                {
                    Mouse.OverrideCursor = Cursors.None;
                    WindowState = WindowState.Maximized;
                    WindowStyle = WindowStyle.None;
                    Hide();
                    Show();
                    Topmost = true;
                }
                else
                {
                    WindowState = WindowState.Normal;
                    WindowStyle = WindowStyle.SingleBorderWindow;
                    Mouse.OverrideCursor = default(Cursor);
                    Topmost = false;
                }
            }
        }
    }
}
