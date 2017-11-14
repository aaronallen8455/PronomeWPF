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
    /// Interaction logic for TappingWindow.xaml
    /// </summary>
    public partial class TappingWindow : Window
    {
        protected bool IsListening;

        public TappingWindow()
        {
            InitializeComponent();
        }

        private void StartCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (targetLayerComboBox == null)
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = targetLayerComboBox.SelectedItem != null &&
                           modeComboBox.SelectedItem != null &&
                           !IsListening;
        }

        private void StartCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IsListening = true;
            listeningMessage.Visibility = Visibility.Visible;

            // setup the countdown
            if (countOffCheckBox.IsChecked == true)
            {
                Metronome.GetInstance().SetupCountoff();
            }
        }

        private void DoneCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IsListening = false;

            this.Close();
        }

        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var layers = Metronome.GetInstance().Layers;

            (sender as ComboBox).ItemsSource = layers.Select(x => "Layer " + (layers.IndexOf(x) + 1).ToString());
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {

        }
    }
}
