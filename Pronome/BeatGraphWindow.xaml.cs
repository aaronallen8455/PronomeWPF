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
    /// Interaction logic for BeatGraphWindow.xaml
    /// </summary>
    public partial class BeatGraphWindow : Window
    {
        protected RotateTransform needleRotation;

        protected AnimationTimer Timer;

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

            int index = 0;
            // pick a color and draw the ticks for each layer
            foreach (BeatGraphLayer layer in graphLayers)
            {

                // draw halo circle
                EllipseGeometry halo = new EllipseGeometry(
                    center,
                    layer.Radius + BeatGraph.tickSize, layer.Radius + BeatGraph.tickSize);
                var haloGeo = new GeometryDrawing();
                //var haloColor1 = GetRgb(index);
                //haloColor1.ScA = .2f;
                var haloColor = GetRgb(index);
                var grad = MakeGradient(center, layer.Radius, layer.Radius + BeatGraph.tickSize, haloColor);
                //grad.ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation;
                //grad.Center = center;
                //grad.GradientOrigin = center;
                //grad.MappingMode = BrushMappingMode.Absolute;
                //grad.RadiusX = grad.RadiusY = layer.Radius + BeatGraph.tickSize;
                //grad.GradientStops = new GradientStopCollection(new GradientStop[]
                //{
                //    new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
                //    new GradientStop(Color.FromArgb(0, 0, 0, 0), (layer.Radius - BeatGraph.tickSize) / (layer.Radius + BeatGraph.tickSize)),
                //    new GradientStop(haloColor1, (layer.Radius - BeatGraph.tickSize) / (layer.Radius + BeatGraph.tickSize)),
                //    new GradientStop(haloColor2, 1),
                //});
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
            //Metronome.GetInstance().UpdateTime();
            Timer = new AnimationTimer();
            CompositionTarget.Rendering += GraphAnimationFrame;
        }

        private void GraphAnimationFrame(object sender, EventArgs e)
        {
            var playstate = Metronome.GetInstance().PlayState;

            if (playstate == Metronome.State.Playing)
            {
                //try
                //{
                    //double curTime = Metronome.GetInstance().ElapsedTime.TotalSeconds;
                    //double timeDiff = curTime - lastTime;

                double interval = Timer.GetElapsedTime();

                double quarterNotes = Metronome.GetInstance().Tempo * (interval / 60);
                double portion = quarterNotes / BeatGraph.cycleLength;
                double angle = 360 * portion;

                needleRotation.Angle += angle;

                    //// rotate needle
                    //if (needleRotation.Angle == angle)
                    //{
                    //    needleRotation.Angle += Metronome.GetInstance().Tempo / 10 / BeatGraph.cycleLength;
                    //}
                    //else
                    //{
                    //    //lastAngle = Math.Abs(needleRotation.Angle - angle);
                    //    needleRotation.Angle = angle;
                    //}
                    //needleRotation.Angle += Metronome.GetInstance().Tempo / 10 / BeatGraph.cycleLength;
                    //lastTime = curTime;
                //}
                //catch (Exception err) { }
            }
            else if (playstate == Metronome.State.Paused)
            {
                Timer.Reset();
            }
            else if (playstate == Metronome.State.Stopped)
            {
                needleRotation.Angle = 0;
                Timer.Reset();
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

        protected RadialGradientBrush MakeGradient(Point center, double innerRadius, double outerRadius, Color color)
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

    }
}
