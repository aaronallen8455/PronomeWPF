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
    /// Interaction logic for RepeatGroupDialog.xaml
    /// </summary>
    public partial class RepeatGroupDialog : Window
    {
        public int Times = 2;

        public string LastTermModifier = "";

        Brush DefaultTextBoxBorder;

        public RepeatGroupDialog()
        {
            InitializeComponent();
        }

        private void timesInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox ele = sender as TextBox;
            if (int.TryParse(ele.Text, out int input))
            {
                Times = input;
                // Hijack this property for validation
                ele.IsInactiveSelectionHighlightEnabled = false;
                // enable OK
                okButton.IsEnabled = true;
            }
            else
            {
                ele.IsInactiveSelectionHighlightEnabled = true;
                //ele.BorderBrush = Brushes.Red;
                // disable OK
                okButton.IsEnabled = false;
            }
        }

        private void timesInput_Loaded(object sender, RoutedEventArgs e)
        {
            DefaultTextBoxBorder = (sender as TextBox).BorderBrush;
            (sender as TextBox).Text = Times.ToString();
        }

        private void lastTermModifierInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox ele = sender as TextBox;
            string input = ele.Text;
            if (BeatCell.ValidateExpression(input))
            {
                LastTermModifier = input;
                ele.IsInactiveSelectionHighlightEnabled = false;
                // enable ok button
                okButton.IsEnabled = true;
            }
            else
            {
                ele.IsInactiveSelectionHighlightEnabled = true;
                // disable Ok button
                okButton.IsEnabled = false;
            }
        }

        private void lastTermModifierInput_Loaded(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).Text = LastTermModifier;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void okButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
