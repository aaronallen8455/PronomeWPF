using System.Windows;
using System.Windows.Controls;


namespace Pronome.Classes.Editor
{
    /// <summary>
    /// Interaction logic for MultGroupDialog.xaml
    /// </summary>
    public partial class MultGroupDialog : Window
    {
        public string Factor = "1";

        public MultGroupDialog()
        {
            Owner = EditorWindow.Instance;

            InitializeComponent();
        }

        private void factorInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            string input = (sender as TextBox).Text;

            if (IsInitialized)
            {
                if (BeatCell.ValidateExpression(input))
                {
                    Factor = input;
                    (sender as TextBox).IsInactiveSelectionHighlightEnabled = false;
                    okButton.IsEnabled = true;
                }
                else
                {
                    (sender as TextBox).IsInactiveSelectionHighlightEnabled = true;
                    okButton.IsEnabled = false;
                }
            }
        }

        private void factorInput_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).Text = Factor;
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
