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
using System.Windows.Shapes;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for ExportWavWindow.xaml
    /// </summary>
    public partial class ExportWavWindow : Window
    {
        public double numerOfSeconds;

        protected double secondsInCycle;

        public ExportWavWindow()
        {
            InitializeComponent();

            // determine if beat is assymetrical
            double qNotes = Metronome.GetInstance().GetQuartersForCompleteCycle();

            double seconds = qNotes / (Metronome.GetInstance().Tempo / 60);
            if (seconds > 60 * 60 * 5) useCycle.IsEnabled = false;
            else secondsInCycle = seconds;
        }

        protected double GetFileSize(double seconds)
        {
            double kB = seconds * 126;
            int sub = (int)seconds;
            kB -= (seconds - 1);

            return kB;
        }

        private void useCycle_Checked(object sender, RoutedEventArgs e)
        {
            seconds.IsEnabled = false;
            seconds.Text = secondsInCycle.ToString();

            // insert the file size estimate
            double secs;

            if (double.TryParse(seconds.Text, out secs))
            {
                fileSize.Text = String.Format("{0:n0}", GetFileSize(secs));
            }
        }

        private void useCycle_Unchecked(object sender, RoutedEventArgs e)
        {
            seconds.IsEnabled = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            // validate input
            double secs;
            if (double.TryParse(seconds.Text, out secs))
            {
                numerOfSeconds = secs;
                DialogResult = true;
            }
        }

        private void seconds_TextChanged(object sender, TextChangedEventArgs e)
        {
            // insert the file size estimate
            double secs;

            if (double.TryParse(seconds.Text, out secs))
            {
                fileSize.Text = String.Format("{0:n0}", GetFileSize(secs));
            }
        }
    }
}
