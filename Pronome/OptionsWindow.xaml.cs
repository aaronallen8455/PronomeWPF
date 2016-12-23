using System;
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

        }

        private void loadButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void exportWavButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void recordWavButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
