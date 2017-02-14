using System.Windows;
using System.Windows.Media;

namespace Pronome.Bounce
{
    /// <summary>
    /// Has methods for drawing the scene and holding state
    /// </summary>
    public static class Helper
    {
        /// <summary>
        /// The width of the ball area
        /// </summary>
        public static int width;

        /// <summary>
        /// The amount of space between the edge of the screen and the bounds of the ball area.
        /// </summary>
        public static double widthPad = 350;

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
        public static int layerCount;

        /// <summary>
        /// Holds the Lane objects.
        /// </summary>
        static Lane[] Lanes;

        /// <summary>
        /// Holds the ball objects
        /// </summary>
        static Ball[] Balls;

        /// <summary>
        /// Timer for syncing animation
        /// </summary>
        static AnimationTimer timer;

        /// <summary>
        /// True if beat is not playing.
        /// </summary>
        static bool IsStopped = true;

        /// <summary>
        /// The base radius of the ball drawings.
        /// </summary>
        const double baseBallRadius = 70;

        /// <summary>
        /// Ball radius based on image ratio.
        /// </summary>
        public static double ballRadius;

        /// <summary>
        /// The base amount of padding on the left of right of each ball.
        /// </summary>
        const double baseBallPadding = 20;

        /// <summary>
        /// Amount of ball padding based on image ratio
        /// </summary>
        static double ballPadding;

        /// <summary>
        /// The unit position of the line that seperates the lanes and balls.
        /// </summary>
        public static double divisionLine = height / (1 / divisionPoint);

        /// <summary>
        /// The position of the center line of the balls when at base level.
        /// </summary>
        public static double ballBase = height - divisionLine - ballRadius;

        /// <summary>
        /// The factor by which the drawn image is sized to the window
        /// </summary>
        public static double imageRatio = 1;

        /// <summary>
        /// Padding on left side so that image appears in center of window
        /// </summary>
        public static double imageWidthPad = 0;

        /// <summary>
        /// Padding on top so that image appears in center of window.
        /// </summary>
        public static double imageHeightPad = 0;

        /// <summary>
        /// Has the endpoints for the lane lines
        /// </summary>
        static Point[,] laneEndPoints;

        /// <summary>
        /// Pen used to draw lane lines
        /// </summary>
        static Pen lanePen = new Pen(Brushes.LightGray, 2);

        /// <summary>
        /// Number of quarter notes to show in the queue
        /// </summary>
        public static double TickQueueSize = 6;

        /// <summary>
        /// Draw the scene, instantiate all initial objects
        /// </summary>
        /// <param name="drawing"></param>
        public static void DrawScene(DrawingVisual drawing)
        {
            Metronome met = Metronome.GetInstance();
            if (met.Layers.Count == 0) return; // do nothing if beat is empty

            // remove existing graphics
            //drawingGroup.Children.Clear();
            ballRadius = baseBallRadius;
            ballPadding = baseBallPadding;

            layerCount = met.Layers.Count;
            Balls = new Ball[layerCount];
            Lanes = new Lane[layerCount];
            //LaneGeometries = new GeometryDrawing[layerCount + 1];
            width = (int)(layerCount * (ballRadius * 2 + ballPadding * 2));
            divisionLine = height / (1 / divisionPoint);
            ballBase = height - divisionLine - ballRadius;

            // calculate imageRatio values
            SetImageRatio(BounceWindow.Instance.ActualWidth, BounceWindow.Instance.ActualHeight);

            using (DrawingContext dc = drawing.RenderOpen())
            {
                // draw lanes
                DrawLanes(dc);

                // draw balls
                for (int i = 0; i < layerCount; i++)
                {
                    MakeBall(i, dc);

                    //drawingGroup.Children.Add(ball);
                }

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
                        ball.Sync(Metronome.GetInstance().ElapsedQuarters, dc);
                    }

                    foreach (Lane lane in Lanes)
                    {
                        lane.Sync(Metronome.GetInstance().ElapsedQuarters, dc);
                    }
                }
            }

            // draw horizon
            //var horizonLine = new LineGeometry(new Point(0, height/2), new Point(width + 2 * widthPad, height/2));
            //drawingGroup.Children.Add(new GeometryDrawing(null, new Pen(Brushes.Aqua, 2), horizonLine));


            timer = new AnimationTimer();
        }

        /// <summary>
        /// Draw the lanes and optionally instantiate the lane objects.
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="instantiateLayers">Whether to run the instantiation routine</param>
        static void DrawLanes(DrawingContext dc, bool instantiateLayers = true)
        {
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
        /// Draw the current frame.
        /// </summary>
        /// <param name="dc"></param>
        public static void DrawFrame(DrawingVisual drawing)
        {
            if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
            {
                double elapsed = timer.GetElapsedTime();

                using (DrawingContext dc = drawing.RenderOpen())
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
                    DrawScene(drawing);
                }
                IsStopped = true;
            }
        }

        /// <summary>
        /// Create a ball drawing.
        /// </summary>
        /// <param name="index">Index of layer</param>
        /// <returns>Ball geometry</returns>
        static void MakeBall(int index, DrawingContext dc)
        {
            double xOffset = (width / (layerCount * 2) * (index * 2 + 1) + widthPad) * imageRatio + imageWidthPad;

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
        public static void SetImageRatio(double winWidth, double winHeight)
        {
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
    }
}
