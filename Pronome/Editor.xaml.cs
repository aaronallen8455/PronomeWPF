using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Text.RegularExpressions;
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
    /// Interaction logic for Editor.xaml
    /// </summary>
    public partial class Editor : Window
    {
        public static Editor Instance;

        List<Editor.Row> Rows = new List<Editor.Row>();

        /// <summary>
        /// The scale of the spacing in the UI
        /// </summary>
        public double Scale = 1;

        public Editor()
        {
            InitializeComponent();

            Instance = this;
        }

        public void BuildUI()
        {
            // remove old UI
            layerPanel.Children.Clear();
            Rows.Clear();
            foreach (Layer layer in Metronome.GetInstance().Layers)
            {
                var row = new Editor.Row(layer);
                layerPanel.Children.Add(row.Canvas);
                Rows.Add(row);
            }
        }
    }
}
