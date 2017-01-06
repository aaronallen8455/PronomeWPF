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
using System.ComponentModel;
using System.IO.IsolatedStorage;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static StackPanel LayerStack;

        public MainWindow()
        {
            InitializeComponent();

            LayerStack = layerStack;

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
        }

        /**<summary>Play the beat.</summary>*/
        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            if (Metronome.GetInstance().Play())
            {
                (Application.Current.Resources["disableDuringPlay"] as Button).IsEnabled = false;
                playButton.IsEnabled = false;
                pauseButton.IsEnabled = true;
            }
        }

        /**<summary>Pause the beat.</summary>*/
        private void pauseButton_Click(object sender, RoutedEventArgs e)
        {
            Metronome.GetInstance().Pause();

            playButton.IsEnabled = true;
            pauseButton.IsEnabled = false;
        }

        /**<summary>Stop the beat.</summary>*/
        private void stopButton_Click(object sender, RoutedEventArgs e)
        {
            Metronome.GetInstance().Stop();

            playButton.IsEnabled = true;
            pauseButton.IsEnabled = false;
            (Application.Current.Resources["disableDuringPlay"] as Button).IsEnabled = true;
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
                double width = position.X + resizeOffset.X;
                double height = position.Y + resizeOffset.Y;
                Width = width > 540 ? width : 540; // limit the dimensions
                Height = height > 150 ? height : 150;
            }
        }

        public Dictionary<string, double> Settings = new Dictionary<string, double>();

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // close the options window
            OptionsWindow options = Resources["optionsWindow"] as OptionsWindow;
            options.KeepOpen = false;
            options.Close();

            // save user settings
            if (Settings.ContainsKey("winWidth")) Settings["winWidth"] = Width;
            else Settings.Add("winWidth", Width);
            if (Settings.ContainsKey("winHeight")) Settings["winHeight"] = Height;
            else Settings.Add("winHeight", Height);
            if (Settings.ContainsKey("winX")) Settings["winX"] = Left;
            else Settings.Add("winX", Left);
            if (Settings.ContainsKey("winY")) Settings["winY"] = Top;
            else Settings.Add("winY", Top);
            if (Settings.ContainsKey("beatFontSize")) Settings["beatFontSize"] = (double)Application.Current.Resources["textBoxFontSize"];
            else Settings.Add("beatFontSize", (double)Application.Current.Resources["textBoxFontSize"]);

            // Write window size and position to storage
            IsolatedStorageFile f = IsolatedStorageFile.GetUserStoreForAssembly();
            using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream("pronomeSettings", FileMode.Create, f))
                using (StreamWriter writer = new StreamWriter(stream))
            {
                foreach (KeyValuePair<string, double> pair in Settings)
                {
                    writer.WriteLine("{0}={1}", pair.Key, pair.Value);
                }
            }

            f.Dispose();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // Read each setting when application is initialized
            IsolatedStorageFile f = IsolatedStorageFile.GetUserStoreForAssembly();
            using (IsolatedStorageFileStream stream = new IsolatedStorageFileStream("pronomeSettings", FileMode.OpenOrCreate, f))
                using (StreamReader reader = new StreamReader(stream))
            {
                string line = reader.ReadLine();
                while (line != null)
                {
                    string[] setting = line.Split('=');
                    try
                    {
                        Settings.Add(setting[0], double.Parse(setting[1]));
                    }
                    catch (Exception err) { }

                    line = reader.ReadLine();
                }

            }
            // apply settings
            if (Settings.ContainsKey("winX")) Left = Settings["winX"];
            if (Settings.ContainsKey("winY")) Top = Settings["winY"];
            if (Settings.ContainsKey("winWidth")) Width = Settings["winWidth"];
            if (Settings.ContainsKey("winHeight")) Height = Settings["winHeight"];
            if (Settings.ContainsKey("beatFontSize")) Application.Current.Resources["textBoxFontSize"] = Settings["beatFontSize"];

            f.Dispose();
        }

        private void openOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            Window pop = Resources["optionsWindow"] as Window;
            pop.Show();
            pop.Activate();
        }
    }

    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBooleanConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        #endregion
    }
}
