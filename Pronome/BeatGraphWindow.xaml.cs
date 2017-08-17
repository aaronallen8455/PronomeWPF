using System;
using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for BeatGraphWindow.xaml
    /// </summary>
    public partial class BeatGraphWindow : Window
    {
        /**<summary>Whether the blinking effect is rendered</summary>*/
        public static bool BlinkingIsEnabled = true;

        /**<summary>Used to rotate the needle element.</summary>*/
        protected RotateTransform needleRotation;

        protected AnimationTimer Timer;

        /**<summary>Array of blinking elements and related beat data.</summary>*/
        protected BlinkElement[] blinkElems;

        /**<summary>Whether is graph is stopped or is actively running.</summary>*/
        protected bool isStopped;

        /**<summary>Whether is graph is currently shown on screen.</summary>*/
        public bool GraphIsDrawn = false;

        /// <summary>
        /// The window instance.
        /// </summary>
        public static BeatGraphWindow Instance;

        ////**<summary>The RNG seed for determining colors.</summary>*/
        //private int rgbSeed;

        public BeatGraphWindow()
        {
            InitializeComponent();
            Metronome.AfterBeatParsed += new EventHandler(DrawGraph);
            Instance = this;
        }

        protected void DrawGraph(object sender, EventArgs e)
        {
            // draw graph when triggered by beat parsed event
            if (GraphIsDrawn)
                DrawGraph();
        }

        /// <summary>
        /// Draw the beat graph and start animation if beat is currently playing.
        /// </summary>
        /// <param name="changeColor">Whether to reset the color pallette</param>
        public void DrawGraph()
        {
            if (Metronome.GetInstance().Layers.Count == 0)
            {
                //throw new Exception("No layers to graph.");
                return;
            }

            timeoutError.Visibility = Visibility.Hidden; // hide the asymmetry error message.

            drawingGroup.Children.Clear();
            //if (changeColor)
            //{
            //    rgbSeed = Metronome.GetRandomNum();
            //}

            Point center = new Point(BeatGraph.graphRadius, BeatGraph.graphRadius);

            try
            {
                BeatGraphLayer[] graphLayers = BeatGraph.DrawGraph();

                blinkElems = new BlinkElement[graphLayers.Length];

                int index = 0;
                // pick a color and draw the ticks for each layer
                foreach (BeatGraphLayer layer in graphLayers)
                {
                    EllipseGeometry halo = new EllipseGeometry(
                        center,
                        layer.Radius + BeatGraph.tickSize, layer.Radius + BeatGraph.tickSize);

                    Color haloColor = ColorHelper.ColorWheel(index, 1);
                    Color blinkColor = ColorHelper.ColorWheel(index, .75f);

                    // draw background 'blink' layer
                    var blinkGeo = new GeometryDrawing();
                    var blinkBrush = MakeGradient(center, layer.Radius, layer.Radius + BeatGraph.tickSize, blinkColor, .6f);
                    blinkGeo.Brush = blinkBrush;
                    blinkGeo.Geometry = halo;
                    blinkBrush.Opacity = 0;
                    drawingGroup.Children.Add(blinkGeo);
                    blinkElems[index] = new BlinkElement(blinkBrush, index);

                    // draw halo circle
                    var haloGeo = new GeometryDrawing();
                    var grad = MakeGradient(center, layer.Radius, layer.Radius + BeatGraph.tickSize, haloColor);
                    haloGeo.Brush = grad;
                    haloGeo.Geometry = halo;
                    drawingGroup.Children.Add(haloGeo);

                    // stroke color
                    Color tickColor = new Color();
                    tickColor.ScR = 1f;
                    tickColor.ScB = 1f;
                    tickColor.ScG = 1f;
                    tickColor.ScA = 1f;

                    var geoDrawing = new GeometryDrawing();
                    geoDrawing.Pen = new Pen(
                        MakeGradient(center, layer.Radius, layer.Radius + BeatGraph.tickSize, tickColor),
                        1.4
                    );

                    var streamGeo = new StreamGeometry();
                    using (StreamGeometryContext context = streamGeo.Open())
                    {
                        for (int i=0; i<layer.Ticks.Length; i += 2)
                        {
                            context.BeginFigure(layer.Ticks[i], false, false);
                            context.LineTo(layer.Ticks[i + 1], true, false);
                        }
                    }

                    geoDrawing.Geometry = streamGeo;

                    drawingGroup.Children.Add(geoDrawing);

                    index++;
                }

                // draw the needle
                RectangleGeometry needle = new RectangleGeometry(
                    new Rect(BeatGraph.graphRadius - 1.5, 0, 3, BeatGraph.graphRadius)
                    );
                needleRotation = new RotateTransform(0, BeatGraph.graphRadius, BeatGraph.graphRadius);
                needle.Transform = needleRotation;
                var needleDrawing = new GeometryDrawing(Brushes.Aqua, new Pen(), needle);
                drawingGroup.Children.Add(needleDrawing);

                // Animate

                if (Metronome.GetInstance().PlayState != Metronome.State.Stopped)
                {
                    // set the initial position (if beat was playing before graph opened)
                    if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
                    {
                        Metronome.GetInstance().UpdateElapsedQuarters();
                    }

                    double portion = Metronome.GetInstance().ElapsedQuarters / BeatGraph.cycleLength;

                    needleRotation.Angle = 360 * portion;
                    // sync blinkers
                    foreach (BlinkElement el in blinkElems)
                    {
                        el.Sync(Metronome.GetInstance().ElapsedQuarters);
                    }
                }

                Timer = new AnimationTimer();
                CompositionTarget.Rendering += GraphAnimationFrame;

                GraphIsDrawn = true;

            }
            catch (TimeoutException e)
            {
                DrawAsymError();
            }
        }

        protected void DrawAsymError()
        {
            timeoutError.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Draw an animation frame. Rotates needle and triggers blink effects.
        /// </summary>
        private void GraphAnimationFrame(object sender, EventArgs e)
        {
            var playstate = Metronome.GetInstance().PlayState;

            if (playstate == Metronome.State.Playing)
            {
                isStopped = false;

                double interval = Timer.GetElapsedTime();

                double quarterNotes = Metronome.GetInstance().Tempo * (interval / 60);
                double portion = quarterNotes / BeatGraph.cycleLength;

                needleRotation.Angle += 360 * portion;

                // do blinking animations
                foreach (BlinkElement el in blinkElems)
                {
                    el.nextBeat -= quarterNotes;
                    
                    if (el.nextBeat <= 0)
                    {
                        if (BlinkingIsEnabled) el.Blink();
                        el.progressBeat();
                    }
                }
            }
            else if (playstate == Metronome.State.Paused)
            {
                Timer.Reset();
            }
            else if (playstate == Metronome.State.Stopped)
            {
                Timer.Reset();
                if (!isStopped)
                {
                    needleRotation.Angle = 0;
                    ResetBlinks();
                }
                isStopped = true;
            }
        }

        ///// <summary>
        ///// Get a color based on the layer index.
        ///// </summary>
        ///// <param name="index">Index of the layer</param>
        //protected Color GetRgb(int index)
        //{
        //    float i = 3f / 8f * (index % 8f);
        //    i += 3f * (float)(rgbSeed / 100);
        //    if (i > 3f) i -= 3f;
        //
        //    Color color = new Color() { ScA = 1f };
        //
        //    // find RGB based on layer index
        //    if (i >= 2.5f)
        //    {
        //        color.ScG = .5f + (i - 2.5f);
        //        color.ScR = i - 2.5f;
        //
        //    }
        //    else if (i >= 1.5f)
        //    {
        //        color.ScB = .5f + (i - 1.5f);
        //        color.ScG = i - 1.5f;
        //    }
        //    else if (i >= .5f)
        //    {
        //        color.ScR = .5f + (i - .5f);
        //        color.ScB = i - .5f;
        //    }
        //    else
        //    {
        //        color.ScR = .5f + i;
        //        color.ScG = .5f - i;
        //    }
        //
        //    return color;
        //}

        /// <summary>
        /// Creates a gradient brush for the concentric layer elements
        /// </summary>
        /// <param name="center">Center point</param>
        /// <param name="innerRadius">Inner radius</param>
        /// <param name="outerRadius">Outer radius</param>
        /// <param name="color">Base color</param>
        /// <param name="alpha">Alpha value to fade to</param>
        protected RadialGradientBrush MakeGradient(Point center, double innerRadius, double outerRadius, Color color, float alpha = .2f)
        {
            Color transColor = new Color()
            {
                ScA = .2f,
                ScB = color.ScB,
                ScG = color.ScG,
                ScR = color.ScR
            };

            var grad = new RadialGradientBrush();
            grad.ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation;
            grad.Center = center;
            grad.GradientOrigin = center;
            grad.MappingMode = BrushMappingMode.Absolute;
            grad.RadiusX = grad.RadiusY = outerRadius;
            grad.GradientStops = new GradientStopCollection(new GradientStop[]
            {
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                    new GradientStop(Color.FromArgb(0, 0, 0, 0), innerRadius / outerRadius),
                    new GradientStop(transColor, innerRadius / outerRadius),
                    new GradientStop(color, 1),
            });

            return grad;
        }

        /// <summary>
        /// Reset the blink element's internal values
        /// </summary>
        public void ResetBlinks()
        {
            foreach (BlinkElement el in blinkElems)
            {
                el.Reset();
            }
        }

        public bool KeepOpen = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (KeepOpen)
            {
                Hide();
                e.Cancel = true;
            }

            GraphIsDrawn = false;
            CompositionTarget.Rendering -= GraphAnimationFrame;
        }

        protected class BlinkElement
        {
            static DoubleAnimation animation = new DoubleAnimation()
            {
                From = .8,
                To = 0,
                Duration = TimeSpan.FromSeconds(.15)
            };

            protected Layer layer;
            public GradientBrush Brush;
            public double nextBeat;
            protected int beatIndex = 0;

            public BlinkElement(GradientBrush brush, int layerIndex)
            {
                layer = Metronome.GetInstance().Layers[layerIndex];
                Brush = brush;
                nextBeat = layer.Offset;

                // account for silent beats
                addSilent();

                trim();
            }

            public void progressBeat()
            {
                addNext();

                // account for silent beats
                addSilent();

                trim();
            }

            public void Blink()
            {
                Brush.BeginAnimation(GradientBrush.OpacityProperty, animation);
            }

            public void Sync(double bpm)
            {
                while (nextBeat < bpm)
                {
                    addNext();

                    addSilent();
                }

                nextBeat -= bpm;

                trim();
            }

            protected void addNext()
            {
                nextBeat += layer.Beat[beatIndex].Bpm;
                beatIndex++;

                if (beatIndex >= layer.Beat.Count)
                {
                    beatIndex -= layer.Beat.Count;
                }
            }

            protected void addSilent()
            {
                while (layer.Beat[beatIndex].SoundSource?.Uri == WavFileStream.SilentSourceName)
                {
                    addNext();
                }
            }

            protected void trim()
            {
                if (nextBeat >= BeatGraph.cycleLength)
                {
                    nextBeat -= BeatGraph.cycleLength * (int)(nextBeat / BeatGraph.cycleLength);
                }
            }

            public void Reset()
            {
                beatIndex = 0;
                nextBeat = layer.Offset;
                addSilent();
                trim();
            }
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
