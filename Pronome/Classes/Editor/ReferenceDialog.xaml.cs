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

namespace Pronome.Classes.Editor
{
    /// <summary>
    /// Interaction logic for ReferenceDialog.xaml
    /// </summary>
    public partial class ReferenceDialog : Window
    {
        public int ReferenceIndex = 1;

        public ReferenceDialog()
        {
            InitializeComponent();
        }

        private void refInput_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ReferenceIndex = (sender as ComboBox).SelectedIndex + 1;
        }

        private void refInput_Loaded(object sender, RoutedEventArgs e)
        {
            ComboBox cb = sender as ComboBox;

            for (int i = 1; i <= Metronome.GetInstance().Layers.Count; i++)
            {
                cb.Items.Add($"Layer {i}");
            }

            cb.SelectedIndex = ReferenceIndex - 1;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
