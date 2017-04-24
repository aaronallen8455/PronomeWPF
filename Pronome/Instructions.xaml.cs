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
    /// Interaction logic for Instructions.xaml
    /// </summary>
    public partial class Instructions : Window
    {
        public Instructions()
        {
            InitializeComponent();

        }

        private void tableOfContents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 1)
            {
                string input = (tableOfContents.SelectedItem as ListBoxItem).Content.ToString().ToLower();

                reader.Document = Resources[input] as FlowDocument;
            }
        }
    }
}
