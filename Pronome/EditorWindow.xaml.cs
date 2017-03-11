using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Pronome.Editor;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for EditorWindow.xaml
    /// </summary>
    public partial class EditorWindow : Window
    {
        public static EditorWindow Instance;

        List<Row> Rows = new List<Row>();

        /// <summary>
        /// The scale of the spacing in the UI
        /// </summary>
        public static double Scale = 1;

        public const float BaseFactor = 40f;

        public EditorWindow()
        {
            InitializeComponent();

            Instance = this;

            // add items to source selector
            List<string> sources = WavFileStream.FileNameIndex.Cast<string>()
                .Where((n, i) => i % 2 == 1) // get the pretty names from the odd numbered indexes
                .Select((x, i) => (i.ToString() + ".").PadRight(4) + x).ToList(); // add index numbers
            sources[0] = "Pitch"; // replace Silentbeat with Pitch
            sourceSelector.ItemsSource = sources;
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
                layerPanel.Children.Add(row.Background);
                Rows.Add(row);
            }
        }

        public void UpdateUiForSelectedCell()
        {
            if (Cell.SelectedCells.Count == 1)
            {
                Cell cell = Cell.SelectedCells[0];

                durationInput.Text = cell.Value;

                string source = string.IsNullOrEmpty(cell.Source) ? cell.Row.Layer.BaseSourceName : cell.Source;
                // is a pitch or wav?
                if (source.Contains(".wav"))
                {
                    pitchInputPanel.Visibility = Visibility.Collapsed;
                    //string newSource = WavFileStream.GetFileByName((sourceSelector.SelectedItem as string).Substring(4));
                    string name = WavFileStream.GetSelectorNameByFile(source);
                    sourceSelector.SelectedItem = name;
                }
                else
                {
                    // pitch
                    pitchInputPanel.Visibility = Visibility.Visible;
                    pitchInput.Text = source;
                    sourceSelector.SelectedItem = "Pitch";
                }
                //pitchInput.Text = source;
            }
            else
            {
                durationInput.Text = string.Empty;
                sourceSelector.SelectedItem = null;
                pitchInput.Text = string.Empty;
                pitchInputPanel.Visibility = Visibility.Collapsed;
            }
        }

        public bool KeepOpen = true;

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            Cell.SelectedCells.Clear();
            SetCellSelected(false);
            UpdateUiForSelectedCell();

            if (KeepOpen)
            {
                Hide();
                e.Cancel = true;
            }
        }

        public void SetCellSelected(bool selected)
        {
            Resources["cellSelected"] = selected;
        }

        public void SetChangesApplied(bool applied)
        {
            Resources["changesApplied"] = !applied;
        }

        private void applyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            SetChangesApplied(true);
        }

        private void durationInput_LostFocus(object sender, RoutedEventArgs e)
        {
            string value = ((TextBox)sender).Text;
            // validate
            if (Regex.IsMatch(value, @"(\d+\.?\d*[\-+*/xX]?)*\d+\.?\d*$"))
            {
                double duration = BeatCell.Parse(value);

                foreach(Cell cell in Cell.SelectedCells)
                {
                    cell.Duration = duration;
                    cell.Value = value;
                }

                SetChangesApplied(false);
            }
        }

        private void sourceSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }

        private void pitchInput_LostFocus(object sender, RoutedEventArgs e)
        {

        }
    }
}
