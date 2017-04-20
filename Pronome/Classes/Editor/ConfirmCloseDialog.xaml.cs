using System.Windows;

namespace Pronome.Classes.Editor
{
    /// <summary>
    /// Interaction logic for ConfirmCloseDialog.xaml
    /// </summary>
    public partial class ConfirmCloseDialog : Window
    {
        public bool Discard;

        public ConfirmCloseDialog()
        {
            InitializeComponent();
        }

        private void applyButton_Click(object sender, RoutedEventArgs e)
        {
            Discard = false;
            DialogResult = true;
        }

        private void discardButton_Click(object sender, RoutedEventArgs e)
        {
            Discard = true;
            DialogResult = true;
        }
    }
}
