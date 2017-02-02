﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;
using System.Text.RegularExpressions;
using Microsoft.Win32;

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
                if (Regex.IsMatch(intervalAudibleInput.Text + intervalSilentInput.Text, pattern)) {
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
                Metronome.Save(saveFileDialog.FileName);
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
            double value = double.Parse(input.Text);
            if (value > 0 && value < 1000)
            {
                BounceWindow.Tick.EndPoint = value;
                if (BounceWindow.Instance != null && BounceWindow.Instance.SceneDrawn)
                {
                    BounceWindow.Instance.DrawScene();
                }
            }
        }

        private void queueSizeInput_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).Text = BounceWindow.Tick.EndPoint.ToString();
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


    }
}
