using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using System.Collections.Generic;
using NAudio.Wave;
using System.Runtime.InteropServices;

// TODO: pitch random muting doesn't occur on first note
namespace Pronome
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
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

        private void applyMuting(object sender, RoutedEventArgs e)
        {
            int percent;
            int seconds;

            if (randomMuteToggle.IsChecked == true)
            {
                if (int.TryParse(randomMuteInput.Text, out percent) && int.TryParse(randomMuteTimerInput.Text, out seconds))
                {
                    Metronome.GetInstance().SetRandomMute(percent, seconds);
                }
            }
            else // disabled
            {
                Metronome.GetInstance().SetRandomMute(0);
            }
        }

        private void applyIntervalMuting(object sender, RoutedEventArgs e)
        {
            double audible;
            double silent;

            if (intervalMuteToggle.IsChecked == true)
            {
                // validate the input
                string pattern = @"[\d.*/\-+xX]";
                if (Regex.IsMatch(intervalAudibleInput.Text + intervalSilentInput.Text, pattern))
                {
                    audible = BeatCell.Parse(intervalAudibleInput.Text);
                    silent = BeatCell.Parse(intervalSilentInput.Text);

                    Metronome.GetInstance().SetSilentInterval(audible, silent);
                }
            }
            else
            {
                Metronome.GetInstance().SetSilentInterval(0, 0);
            }
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.AddExtension = true;
            saveFileDialog.DefaultExt = "beat";
            saveFileDialog.ValidateNames = true;
            saveFileDialog.Title = "Save Beat As";
            saveFileDialog.Filter = "Beat file (*.beat)|*.beat";

            if (saveFileDialog.ShowDialog() == true)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Metronome.Save(saveFileDialog.FileName);
                }));
            }
        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Beat file (*.beat)|*.beat";
            openFileDialog.Title = "Open Beat";
            openFileDialog.DefaultExt = "beat";

            if (openFileDialog.ShowDialog() == true)
            {
                Metronome.Load(openFileDialog.FileName);
            }

            Metronome met = Metronome.GetInstance();

            // set muting inputs
            if (Metronome.GetInstance().IsRandomMute)
            {
                randomMuteToggle.IsChecked = true;
                randomMuteInput.Text = met.RandomMutePercent.ToString();
                randomMuteTimerInput.Text = met.RandomMuteSeconds.ToString();
            }
            else
            {
                randomMuteToggle.IsChecked = false;
            }
            if (Metronome.GetInstance().IsSilentInterval)
            {
                intervalMuteToggle.IsChecked = true;
                intervalAudibleInput.Text = met.AudibleInterval.ToString();
                intervalSilentInput.Text = met.SilentInterval.ToString();
            }
            else
            {
                intervalMuteToggle.IsChecked = false;
            }

            // set the UI inputs
            ((MainWindow)Application.Current.MainWindow).tempoInput.Text = met.Tempo.ToString();
            ((MainWindow)Application.Current.MainWindow).masterVolume.Value = met.Volume;
        }

        private void exportWavButton_Click(object sender, RoutedEventArgs e)
        {
            ExportWavWindow wavWindow = new ExportWavWindow();
            wavWindow.Owner = this;
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.AddExtension = true;
            saveFileDialog.DefaultExt = "wav";
            saveFileDialog.ValidateNames = true;
            saveFileDialog.Title = "Export to Wav File";
            saveFileDialog.Filter = "Wav file (*.wav)|*.wav";

            if (wavWindow.ShowDialog() == true)
            {
                if (saveFileDialog.ShowDialog() == true)
                {
                    Metronome.GetInstance().ExportAsWav(wavWindow.numerOfSeconds, saveFileDialog.FileName);
                }
            }
        }

        private void recordWavButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.AddExtension = true;
            saveFileDialog.DefaultExt = "wav";
            saveFileDialog.ValidateNames = true;
            saveFileDialog.Title = "Record to Wav File";
            saveFileDialog.Filter = "Wav file (*.wav)|*.wav";

            if (saveFileDialog.ShowDialog() == true)
            {
                Metronome.GetInstance().Record(saveFileDialog.FileName);
            }
        }

        private void beatFontSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            double size;
            if (double.TryParse(beatFontSizeTextBox.Text, out size))
            {
                Application.Current.Resources["textBoxFontSize"] = size;
            }
        }

        private void beatFontSizeTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            beatFontSizeTextBox.Text = Application.Current.Resources["textBoxFontSize"].ToString();
        }

        private void blinkToggle_Checked(object sender, RoutedEventArgs e)
        {
            BeatGraphWindow.BlinkingIsEnabled = true;
        }

        private void blinkToggle_Loaded(object sender, RoutedEventArgs e)
        {
            blinkToggle.IsChecked = BeatGraphWindow.BlinkingIsEnabled;
        }

        private void blinkToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            BeatGraphWindow.BlinkingIsEnabled = false;
        }

        private void bounceDivideSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            BounceWindow.divisionPoint = (sender as Slider).Value;
            if (BounceWindow.Instance != null && BounceWindow.Instance.SceneDrawn)
            {
                BounceWindow.Instance.DrawScene();
                //BounceWindow.Instance.ResetConsts();
            }
        }

        private void bounceDivideSlider_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as Slider).Value = BounceWindow.divisionPoint;
        }

        private void queueSizeInput_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox input = sender as TextBox;
            try
            {
                double value = double.Parse(input.Text);

                if (value > 0 && value < 1000)
                {
                    BounceWindow.Tick.QueueSize = value;
                    if (BounceWindow.Instance != null && BounceWindow.Instance.SceneDrawn)
                    {
                        BounceWindow.Instance.DrawScene();
                    }
                }
            }
            catch (FormatException ex)
            {
                input.Text = BounceWindow.Tick.QueueSize.ToString();
            }
        }

        private void queueSizeInput_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).Text = BounceWindow.Tick.QueueSize.ToString();
        }

        private void bounceLaneTaperSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            BounceWindow.widthPad = (sender as Slider).Value;
            if (BounceWindow.Instance != null && BounceWindow.Instance.SceneDrawn)
            {
                //Dispatcher.BeginInvoke(new Action(() => BounceWindow.Instance.DrawScene()));
                BounceWindow.Instance.DrawScene();
                //BounceWindow.Instance.ResetConsts();
            }
        }

        private void bounceLaneTaperSlider_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as Slider).Value = BounceWindow.widthPad;
        }

        private void pitchDecayLength_LostFocus(object sender, RoutedEventArgs e)
        {
            double newValue;
            if (double.TryParse((sender as TextBox).Text, out newValue) && newValue > 0)
            {
                PitchStream.DecayLength = newValue;

            }
        }

        private void pitchDecayLength_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).Text = PitchStream.DecayLength.ToString();
        }

        /// <summary>
        /// Add a new source to the user defined sound sources
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void customSoundNewButton_Click(object sender, RoutedEventArgs e)
        {
            string fileName = "";
            string safeFileName = "";

            // get the target wav file
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Audio File|*.wav;*.mp3;*.aiff";
            openFileDialog.Title = "Select Audio File";
            openFileDialog.DefaultExt = "wav";

            if (openFileDialog.ShowDialog() == true)
            {
                // check if file is in the correct format
                try
                {
                    using (WaveFileReader reader = new WaveFileReader(openFileDialog.FileName))
                    {
                        if (reader.WaveFormat.SampleRate != 16000)
                        {
                            throw new Exception(); // must be 16000hz
                        }

                        fileName = openFileDialog.FileName;
                        safeFileName = openFileDialog.SafeFileName;
                    }
                }
                catch (Exception)
                {
                    var result = new TaskDialogWrapper(this).Show(
                        "Incorrect Format",
                        $"The file, {openFileDialog.SafeFileName}, isn't in the correct format.",
                        "Do you want to save a converted version to use instead?",
                        TaskDialogWrapper.TaskDialogButtons.Yes | TaskDialogWrapper.TaskDialogButtons.No,
                        TaskDialogWrapper.TaskDialogIcon.Warning
                    );
                    
                    if (result == TaskDialogWrapper.TaskDialogResult.Yes)
                    {
                        // save file prompt
                        var saveFile = new SaveFileDialog();
                        saveFile.AddExtension = true;
                        saveFile.Filter = "Wav file (*.wav)|*.wav";
                        saveFile.FileName = openFileDialog.SafeFileName.Substring(0, openFileDialog.SafeFileName.LastIndexOf('.')) + ".wav";
                        if (saveFile.ShowDialog() == true)
                        {
                            // check if new file and source file have the same name
                            bool overwrite = false;
                            if (openFileDialog.FileName == saveFile.FileName)
                            {
                                System.IO.File.Move(openFileDialog.FileName, openFileDialog.FileName + "x");
                                openFileDialog.FileName += "x";
                                overwrite = true;
                            }
                            if (UserSource.ConvertToWave16(openFileDialog.FileName, saveFile.FileName))
                            {
                                // success
                                fileName = saveFile.FileName;
                                safeFileName = saveFile.SafeFileName;

                                // delete source file if overwriting
                                if (overwrite)
                                {
                                    System.IO.File.Delete(openFileDialog.FileName);
                                }
                            }
                            else
                            {
                                // change name back if there was an error
                                System.IO.File.Move(openFileDialog.FileName, openFileDialog.FileName.Substring(0, openFileDialog.FileName.Length - 1));

                                new TaskDialogWrapper(this)
                                    .Show(
                                    "Error", 
                                    "The file could not be converted, an error occured.", 
                                    "",
                                    TaskDialogWrapper.TaskDialogButtons.Ok,
                                    TaskDialogWrapper.TaskDialogIcon.Error);
                            }
                        }
                    }
                }

                if (fileName != string.Empty)
                {
                    new UserSource(fileName, safeFileName);
                }
            }
        }

        /// <summary>
        /// Remove the selected source
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommandBinding_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            // remove the selected items
            var listBox = customSoundListBox as ListBox;

            LinkedList<UserSource> toRemove = new LinkedList<UserSource>();
            foreach (UserSource source in listBox.SelectedItems)
            {
                toRemove.AddLast(source);
            }

            foreach (UserSource source in toRemove)
            {
                UserSource.Library.Remove(source);
            }
        }

        /// <summary>
        /// Is a source selected for removal?
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CommandBinding_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            // check if any sources are selected
            var listBox = customSoundListBox as ListBox;
            if (listBox != null)
            {
                e.CanExecute = listBox.SelectedItems.Count > 0;
            }
        }
    }

}
