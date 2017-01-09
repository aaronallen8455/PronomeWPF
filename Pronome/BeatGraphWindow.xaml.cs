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
        

        public BeatGraphWindow()
        {
            InitializeComponent();
        }

        public void DrawGraph()
        {
            drawingGroup.Children.Clear();

            BeatGraphLayer[] graphLayers = BeatGraph.DrawGraph();

            // pick a color and draw the ticks for each layer
            foreach (BeatGraphLayer layer in graphLayers)
            {
                Color color = new Color();
                color.ScR = .5f;
                color.ScB = .5f;
                color.ScG = 0f;
                color.ScA = 1f;

                SolidColorBrush stroke = new SolidColorBrush(color);

                var geoDrawing = new GeometryDrawing();
                geoDrawing.Pen = new Pen(stroke, 2);

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

                // draw cirlce
                EllipseGeometry circle = new EllipseGeometry(
                    new Point(BeatGraph.graphRadius, BeatGraph.graphRadius), 
                    layer.Radius, layer.Radius
                );
                var circleGeo = new GeometryDrawing();
                circleGeo.Pen = new Pen(stroke, 3);
                circleGeo.Geometry = circle;
                drawingGroup.Children.Add(circleGeo);
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
        }
    }
}
