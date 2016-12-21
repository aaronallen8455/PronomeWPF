using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Reflection;
using System.IO;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // import the syntax definition
            Assembly myAssembly = Assembly.GetExecutingAssembly();
            using (Stream s = myAssembly.GetManifestResourceStream("Pronome.pronome.xshd"))
                using (XmlTextReader reader = new XmlTextReader(s))
                    HighlightingManager.Instance.RegisterHighlighting("Pronome", new[] { ".cs" }, 
                        HighlightingLoader.Load(reader, HighlightingManager.Instance));

            Metronome.GetInstance().Tempo = 120f;
            tempoInput.Text = Metronome.GetInstance().Tempo.ToString();

            new LayerUI(layerStack);
        }

        /**<summary>Make top of window draggable</summary>*/
        private void window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (ResizeMode != ResizeMode.NoResize)
            {
                ResizeMode = ResizeMode.NoResize;
                UpdateLayout();
            }

            DragMove();
        }

        /**<summary>Close the window.</summary>*/
        private void windowCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Metronome.GetInstance().Stop();
            Metronome.GetInstance().Dispose();
            Close();
        }

        /**<summary>Add a new layer</summary>*/
        private void addLayerButton_Click(object sender, RoutedEventArgs e)
        {
            LayerUI layerUI = new LayerUI(layerStack);
            // resize window
            //this.Height += layerUI.basePanel.ActualHeight;
        }

        /**<summary>Play the beat.</summary>*/
        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            Metronome.GetInstance().Play();
        }

        /**<summary>Pause the beat.</summary>*/
        private void pauseButton_Click(object sender, RoutedEventArgs e)
        {
            Metronome.GetInstance().Pause();
        }

        /**<summary>Stop the beat.</summary>*/
        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Metronome.GetInstance().Stop();
        }

        private List<int> tempoHistory = new List<int>();
        /**<summary>Tempo tap handler</summary>*/
        private void tempoTap_Click(object sender, RoutedEventArgs e)
        {
            // get total elapsed millisecs
            int current = (int)(DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);

            if (tempoHistory.Count > 0)
            {
                tempoHistory[tempoHistory.Count - 1] = current - tempoHistory.Last(); // convert to elapsed time
                // reset if greater than 2 secs
                if (tempoHistory.Last() <= 2000)
                {
                    // don't let list size get too big
                    if (tempoHistory.Count > 6) tempoHistory.RemoveAt(0);

                    float tempo = (float)tempoHistory.Average(); // length of time in ms
                    // convert to BPM
                    tempo = 60000 / tempo;
                    Metronome.GetInstance().ChangeTempo(tempo);
                    tempoInput.Text = tempo.ToString();
                }
                else tempoHistory.Clear();
            }
            tempoHistory.Add(current); // plop the full milisec count at end.
        }

        private void tempoInput_LostFocus(object sender, RoutedEventArgs e)
        {
            float newTempo = 0f;
            float.TryParse(tempoInput.Text, out newTempo);

            if (newTempo > 0)
            {
                Metronome.GetInstance().ChangeTempo(newTempo);
            }
        }

        private void tempoUp_Click(object sender, RoutedEventArgs e)
        {
            float current = Metronome.GetInstance().Tempo;
            current++;
            tempoInput.Text = current.ToString();
            Metronome.GetInstance().ChangeTempo(current);
        }

        private void tempoDown_Click(object sender, RoutedEventArgs e)
        {
            float tempo = Metronome.GetInstance().Tempo;
            if (tempo > 1)
            {
                tempo--;
                tempoInput.Text = tempo.ToString();
                Metronome.GetInstance().ChangeTempo(tempo);
            }
        }

        private void masterVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Metronome.GetInstance().Volume = masterVolume.Value;
        }

        Point resizeOffset;
        private void windowResizer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).CaptureMouse();
            Point position = e.GetPosition(this);
            resizeOffset = new Point();
            resizeOffset.X = Width - position.X;
            resizeOffset.Y = Height - position.Y;
        }

        private void windowResizer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((UIElement)sender).ReleaseMouseCapture();
        }

        private void windowResizer_MouseMove(object sender, MouseEventArgs e)
        {
            if (((UIElement)sender).IsMouseCaptured)
            {
                // Resize window
                Point position = e.GetPosition(this);
                Width = position.X + resizeOffset.X;
                Height = position.Y + resizeOffset.Y;
            }
        }
    }
}
