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
        }

        /**<summary>Make top of window draggable</summary>*/
        private void window_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
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
            this.Height += layerUI.basePanel.ActualHeight;
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

        /**<summary>Tempo tap handler</summary>*/
        private void tempoTap_Click(object sender, RoutedEventArgs e)
        {
            // calculate new tempo based on tapping
        }

        private void tempoInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            float newTempo = 0f;
            float.TryParse(tempoInput.Text, out newTempo);

            if (newTempo > 0)
            {
                Metronome.GetInstance().ChangeTempo(newTempo);
            }
        }
    }
}
