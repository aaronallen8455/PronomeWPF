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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for BeatGraphWindow.xaml
    /// </summary>
    public partial class BeatGraphWindow : Window
    {
        protected RotateTransform needleRotation;

        protected AnimationTimer Timer;

        protected BlinkElement[] blinkElems;

        protected bool isStopped;

        public BeatGraphWindow()
        {
            InitializeComponent();
        }

        public void DrawGraph()
        {
            if (Metronome.GetInstance().Layers.Count == 0)
            {
                throw new Exception("No layers to graph.");
            }

            drawingGroup.Children.Clear();
            rgbSeed = Metronome.GetRandomNum();

            Point center = new Point(BeatGraph.graphRadius, BeatGraph.graphRadius);

            BeatGraphLayer[] graphLayers = BeatGraph.DrawGraph();

            blinkElems = new BlinkElement[graphLayers.Length];

            int index = 0;
            // pick a color and draw the ticks for each layer
            foreach (BeatGraphLayer layer in graphLayers)
            {
                EllipseGeometry halo = new EllipseGeometry(
                    center,
                    layer.Radius + BeatGraph.tickSize, layer.Radius + BeatGraph.tickSize);

                Color haloColor = GetRgb(index);

                // draw background 'blink' layer
                var blinkGeo = new GeometryDrawing();
                var blinkBrush = MakeGradient(center, layer.Radius, layer.Radius + BeatGraph.tickSize, haloColor, 1);
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
                Color color = new Color();
                color.ScR = .8f;
                color.ScB = 1f;
                color.ScG = .8f;
                color.ScA = 1f;

                SolidColorBrush stroke = new SolidColorBrush(color);

                var geoDrawing = new GeometryDrawing();
                geoDrawing.Pen = new Pen(
                    MakeGradient(center, layer.Radius, layer.Radius + BeatGraph.tickSize, color),
                    2
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

                //// draw center circle
                //EllipseGeometry circle = new EllipseGeometry(
                //    center, 
                //    layer.Radius, layer.Radius
                //);
                //var circleGeo = new GeometryDrawing();
                //circleGeo.Pen = new Pen(stroke, 3);
                //circleGeo.Geometry = circle;
                //drawingGroup.Children.Add(circleGeo);

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
        }

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
                    
                    if (el.nextBeat <= 0) el.Blink();
                    if (el.nextBeat <= 0)
                    {
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

        private double rgbSeed;

        protected Color GetRgb(int index)
        {
            float i = 3f / 8f * (index % 8f);
            i += 3f * (float)(rgbSeed / 100);
            if (i > 3f) i -= 3f;

            Color color = new Color() { ScA = 1f };

            // find RGB based on layer index
            if (i >= 2.5f)
            {
                color.ScG = .5f + (i - 2.5f);
                color.ScR = i - 2.5f;

            }
            else if (i >= 1.5f)
            {
                color.ScB = .5f + (i - 1.5f);
                color.ScG = i - 1.5f;
            }
            else if (i >= .5f)
            {
                color.ScR = .5f + (i - .5f);
                color.ScB = i - .5f;
            }
            else
            {
                color.ScR = .5f + i;
                color.ScG = .5f - i;
            }

            return color;
        }

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
                while (layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
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

    }
}
