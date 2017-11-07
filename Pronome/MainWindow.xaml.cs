using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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
        public static StackPanel LayerStack;

        public static MainWindow Instance;

        protected SaveFileHelper SaveFileHelper;

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

            // if a previous session's beat is persisted we will open that
            if (UserSettings.PersistSessionStatic && Metronome.GetInstance().Layers.Any())
            {
                foreach (Layer layer in Metronome.GetInstance().Layers)
                {
                    new LayerUI(LayerStack, layer);
                }

                masterVolume.Value = Metronome.GetInstance().Volume;
            }
            else
            {
                // load default starting beat
                Metronome.GetInstance().Tempo = 120f;

                new LayerUI(layerStack);
            }
            tempoInput.Text = Metronome.GetInstance().Tempo.ToString();

            SaveFileHelper = new SaveFileHelper(Resources["recentlyOpenedFiles"] as RecentlyOpenedFiles);

            Instance = this;
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
            //Metronome.GetInstance().Dispose();

            Close();
        }

        /**<summary>Add a new layer</summary>*/
        private void addLayerButton_Click(object sender, RoutedEventArgs e)
        {
            LayerUI layerUI = new LayerUI(layerStack);

            // redraw the beat graph
            if (Metronome.GetInstance().PlayState == Metronome.State.Stopped)
            {
                Metronome.GetInstance().TriggerAfterBeatParsed();
            }
        }

        /**<summary>Play the beat.</summary>*/
        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            if (Metronome.GetInstance().Play())
            {
                (Application.Current.Resources["disableDuringPlay"] as Button).IsEnabled = false;
                playButton.IsEnabled = false;
                pauseButton.IsEnabled = true;

                //// disable the apply changes button in editor if not already off
                //if (EditorWindow.Instance != null && !EditorWindow.Instance.ChangesApplied)
                //{
                //    EditorWindow.Instance.Resources["changesApplied"] = false;
                //}
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

            //// enable the apply changes button in the editor if some changes are pending
            //if (EditorWindow.Instance != null && !EditorWindow.Instance.ChangesApplied)
            //{
            //    EditorWindow.Instance.Resources["changesApplied"] = true;
            //}
        }

        private List<int> tempoHistory = new List<int>();
        /**<summary>Tempo tap handler</summary>*/
        private void tempoTap_Click(object sender, RoutedEventArgs e)
        {
            // exit if a tempo change is currently queued.
            if (Metronome.GetInstance().TempoChangeCued) return;

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
            // exit if a tempo change is currently queued.
            if (Metronome.GetInstance().TempoChangeCued) return;

            float newTempo = 0f;
            float.TryParse(tempoInput.Text, out newTempo);

            if (newTempo > 0)
            {
                Metronome.GetInstance().ChangeTempo(newTempo);
            }
        }

        private void tempoInput_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // exit if a tempo change is currently queued.
            //if (Metronome.GetInstance().TempoChangeCued) return;

            var input = sender as TextBox;
            float tempo = Metronome.GetInstance().Tempo;
            tempo += e.Delta / 120;

            if (tempo > 1)
            {
                input.Text = tempo.ToString();
                Metronome.GetInstance().ChangeTempo(tempo);
            }
        }

        private void tempoUp_Click(object sender, RoutedEventArgs e)
        {
            // exit if a tempo change is currently queued.
            //if (Metronome.GetInstance().TempoChangeCued) return;

            float current = Metronome.GetInstance().Tempo;
            current++;
            tempoInput.Text = current.ToString();
            Metronome.GetInstance().ChangeTempo(current);
        }

        private void tempoDown_Click(object sender, RoutedEventArgs e)
        {
            // exit if a tempo change is currently queued.
            //if (Metronome.GetInstance().TempoChangeCued) return;

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

        private void masterVolume_Initialized(object sender, EventArgs e)
        {
            (sender as Slider).Value = Metronome.GetInstance().Volume;
        }

        /// <summary>
        /// Control the volume with mouse wheel
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void masterVolume_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var slider = sender as Slider;
            double vol = Metronome.GetInstance().Volume;
            double change = (double)e.Delta / 4800;
            vol += change;

            if (vol >= 0 && vol <= 1)
            {
                slider.Value = vol;
            }
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
                Height = height > 200 ? height : 200;
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

            BeatGraphWindow graph = Resources["graphWindow"] as BeatGraphWindow;
            graph.KeepOpen = false;
            graph.Close();

            BounceWindow bounce = Resources["bounceWindow"] as BounceWindow;
            bounce.KeepOpen = false;
            bounce.Close();

            EditorWindow editor = Resources["editorWindow"] as EditorWindow;
            editor.KeepOpen = false;
            editor.Close();

            Instructions help = Resources["helpWindow"] as Instructions;
            help.KeepOpen = false;
            help.Close();

            // save the settings to storage
            UserSettings.GetSettings().SaveToStorage();

            // dispose the metronome
            Metronome.GetInstance().Dispose();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);

            // apply settings from storage
            UserSettings.GetSettingsFromStorage()?.ApplySettings();
        }

        private void minimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CommandPlayStop_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // check if playing or not
            if (Metronome.GetInstance().PlayState == Metronome.State.Playing)
            {
                stopButton_Click(null, null);
            }
            else
            {
                playButton_Click(null, null);
            }
        }

        private void CommandPlayStop_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            IInputElement focus = Keyboard.FocusedElement;
            // check if focused element is a text editor or a button
            if (focus is ICSharpCode.AvalonEdit.Editing.TextArea) e.CanExecute = false;
            else e.CanExecute = true;
            //e.CanExecute = focus == null;
        }

        /// <summary>
        /// When button gets focus, lose it so it doesn't interfere with the shift+space shortcut
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_GotFocus(object sender, RoutedEventArgs e)
        {
            scrollViewer.Focus();
        }

        private void helpButton_Click(object sender, RoutedEventArgs e)
        {
            var helpWindow = Resources["helpWindow"] as Instructions;
            helpWindow.Show();
            helpWindow.Activate();
        }

        private void OpenFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileHelper.LoadFile();
        }

        private void OpenFileCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Metronome.GetInstance().PlayState == Metronome.State.Stopped;
        }

        private void OpenRecentCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var item = e.OriginalSource as MenuItem;

            // the file URI is in the tooltip
            if (item?.ToolTip != null)
            {
                SaveFileHelper.LoadFileUri(item.ToolTip.ToString());
            }
        }

        private void OpenBounceCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var bounceWindow = Resources["bounceWindow"] as BounceWindow;
            bounceWindow.Show();
            //bounceWindow.Activate();
            if (!bounceWindow.SceneDrawn)
            {
                if (BeatGraphWindow.Instance == null || !BeatGraphWindow.Instance.IsVisible)
                {
                    ColorHelper.ResetRgbSeed();
                }

                bounceWindow.DrawScene();
            }
        }

        private void OpenBounceCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Metronome.GetInstance().Layers.Count > 0;
        }

        private void OpenGraphCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var graph = Resources["graphWindow"] as BeatGraphWindow;
            graph.Show();
            graph.Activate();
            if (!graph.GraphIsDrawn)
            {
                if (BounceWindow.Instance == null || !BounceWindow.Instance.IsVisible)
                {
                    ColorHelper.ResetRgbSeed();
                }

                graph.DrawGraph();
            }
        }

        private void OpenOptionsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Window pop = Resources["optionsWindow"] as Window;
            pop.Show();
            pop.Activate();
        }

        private void OpenEditorCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var editorWindow = Resources["editorWindow"] as EditorWindow;
            if (!editorWindow.IsVisible)
            {
                editorWindow.BuildUI();
            }
            editorWindow.Show();
            editorWindow.Activate();
        }

        private void OpenTapCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {

        }

        private void SaveFileCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (SaveFileHelper.CurrentFile == null)
            {
                SaveFileHelper.SaveFileAs();
            }
            else
            {
                SaveFileHelper.SaveFile(SaveFileHelper.CurrentFile.Uri);
            }
        }

        private void SaveAsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveFileHelper.SaveFileAs();
        }

        private void RevertToSaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (SaveFileHelper.CurrentFile != null)
            {
                SaveFileHelper.LoadFileUri(SaveFileHelper.CurrentFile.Uri);
            }
        }

        private void OpenRecentItem_Click(object sender, RoutedEventArgs e)
        {
            var item = sender as MenuItem;

            // the file URI is in the tooltip
            if (item?.ToolTip != null)
            {
                SaveFileHelper.LoadFileUri(item.ToolTip.ToString());
            }
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
