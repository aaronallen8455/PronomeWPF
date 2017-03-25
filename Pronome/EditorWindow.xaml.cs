using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Media;
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
        /// The last row to have contained a selection
        /// </summary>
        Row LastSelectedRow;

        /// <summary>
        /// Actions that can be undone
        /// </summary>
        public ActionStack UndoStack;

        /// <summary>
        /// Actions that can be redone
        /// </summary>
        public ActionStack RedoStack;

        /// <summary>
        /// Sizes the grid cells
        /// </summary>
        public Rectangle GridSizer;
        /// <summary>
        /// Tick mark for each grid cell
        /// </summary>
        public Rectangle GridTick;
        /// <summary>
        /// Left portion of the grid is displayed in here
        /// </summary>
        public Rectangle GridLeft;
        /// <summary>
        /// Right portion of the grid is diplayed in here
        /// </summary>
        public Rectangle GridRight;
        public Canvas GridCanvas;
        public VisualBrush GridBrush;

        public StackPanel LayerPanel;

        /// <summary>
        /// The scale of the spacing in the UI
        /// </summary>
        public static double Scale = 1;

        /// <summary>
        /// Determines overall scale of UI
        /// </summary>
        public const float BaseFactor = 40f;

        public EditorWindow()
        {
            InitializeComponent();

            Instance = this;

            LayerPanel = layerPanel;

            // add items to source selector
            List<string> sources = WavFileStream.FileNameIndex.Cast<string>()
                .Where((n, i) => i % 2 == 1) // get the pretty names from the odd numbered indexes
                .Select((x, i) => (i.ToString() + ".").PadRight(4) + x).ToList(); // add index numbers
            sources[0] = "Pitch"; // replace Silentbeat with Pitch
            sourceSelector.ItemsSource = sources;

            // init grid UI elements
            GridSizer = Resources["gridSizer"] as Rectangle;
            GridTick = Resources["gridTick"] as Rectangle;
            GridLeft = Resources["gridLeft"] as Rectangle;
            Panel.SetZIndex(GridLeft, 5);
            GridRight = Resources["gridRight"] as Rectangle;
            Panel.SetZIndex(GridRight, 5);
            GridCanvas = new Canvas();
            GridBrush = new VisualBrush(GridCanvas);
            GridBrush.ViewportUnits = BrushMappingMode.Absolute;
            GridBrush.TileMode = TileMode.Tile;
            GridLeft.Fill = GridBrush;
            GridRight.Fill = GridBrush;
            GridCanvas.Children.Add(GridSizer);
            GridCanvas.Children.Add(GridTick);

            UndoStack = new ActionStack(undoMenuItem, 50);
            RedoStack = new ActionStack(redoMenuItem, 50);
        }

        public void BuildUI()
        {
            // remove old UI
            layerPanel.Children.Clear();
            Rows.Clear();
            foreach (Layer layer in Metronome.GetInstance().Layers)
            {
                var row = new Row(layer);
                layerPanel.Children.Add(row.BaseElement);
                //layerPanel.Children.Add(row.Canvas);
                //layerPanel.Children.Add(row.Background);
                Rows.Add(row);
            }
        }

        /// <summary>
        /// Will alter the UI to reflect a change in cell selection. Includes grid lines and inputs.
        /// </summary>
        public void UpdateUiForSelectedCell()
        {
            if (Cell.SelectedCells.Cells.Any())
            {
                if (Cell.SelectedCells.Cells.Count == 1)
                {
                    Cell cell = Cell.SelectedCells.Cells[0];

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
                }
                else
                {
                    durationInput.Text = string.Empty;
                    sourceSelector.SelectedItem = null;
                    pitchInput.Text = string.Empty;
                    pitchInputPanel.Visibility = Visibility.Collapsed;
                }

                // draw grid based on increment input
                string intervalCode = incrementInput.Text;
                // validate input

                // free up the grid
                RemoveGridLines();
                // draw the grid on the current row
                LastSelectedRow = Cell.SelectedCells.Cells.First().Row;
                LastSelectedRow.DrawGridLines(intervalCode);
            }
            else
            {
                // empty the fields and remove grid
                durationInput.Text = string.Empty;
                sourceSelector.SelectedItem = null;
                pitchInput.Text = string.Empty;
                pitchInputPanel.Visibility = Visibility.Collapsed;

                RemoveGridLines();
            }
        }

        public void RemoveGridLines()
        {
            if (LastSelectedRow != null)
            {
                LastSelectedRow.BaseElement.Children.Remove(GridLeft);
                LastSelectedRow.BaseElement.Children.Remove(GridRight);
            }
        }

        /// <summary>
        /// Push a new action onto the undo stack and clear the redo stack
        /// </summary>
        /// <param name="action"></param>
        public void AddUndoAction(IEditorAction action)
        {
            RedoStack.Clear();
            UndoStack.Push(action);
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
            double duration;
            if (BeatCell.TryParse(value, out duration))
            {
                //double duration = BeatCell.Parse(value);

                foreach(Cell cell in Cell.SelectedCells.Cells)
                {
                    cell.Duration = duration;
                    cell.Value = value;
                }

                SetChangesApplied(false);
            }
        }

        private void sourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void pitchInput_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        public static string CurrentIncrement = "1";
        /// <summary>
        /// Set a new increment amount
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void incrementInput_LostFocus(object sender, RoutedEventArgs e)
        {
            string input = ((TextBox)sender).Text;

            if (BeatCell.TryParse(input, out double incr))
            {
                if (Cell.SelectedCells.Cells.Any())
                {
                    RemoveGridLines();
                    // draw new grid
                    Cell.SelectedCells.Cells.First().Row.DrawGridLines(input);

                    CurrentIncrement = input;
                }
            }
        }

        /// <summary>
        /// Shows the mouse position in BPM
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollViewer_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // convert mouse position to BPMs
            mousePositionText.Text = (e.GetPosition((StackPanel)sender).X / Scale / BaseFactor).ToString("0.00");
        }

        private void layerPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            // remove mouse location info when mouse leaves the work area
            mousePositionText.Text = string.Empty;
        }

        /** COMMANDS **/

        private void Undo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = UndoStack.Any();
        }

        private void Undo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            // undo the last action
            IEditorAction action = UndoStack.Pop();
            action.Undo();
            RedoStack.Push(action);
        }

        private void Redo_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = RedoStack.Any();
        }

        private void Redo_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            // redo the last undone action
            IEditorAction action = RedoStack.Pop();
            action.Redo();
            UndoStack.Push(action);
        }

        private void CreateRepeatGroup_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Cell.SelectedCells.Cells.Any();
        }

        private void CreateRepeatGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {

        }

        private void RemoveRepeatGroup_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            Cell.Selection selection = Cell.SelectedCells;

            if (selection.Cells.Any())
            {
                // check if any repeat group is represented by the selection
                foreach (RepeatGroup rg in selection.FirstCell.RepeatGroups)
                {
                    if (rg.Cells.First.Value == selection.FirstCell && rg.Cells.Last.Value == selection.LastCell)
                    {
                        e.CanExecute = true;
                        return;
                    }
                }
            }

            e.CanExecute = false;
        }

        private void RemoveRepeatGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {

        }

    }

    public static class Commands
    {
        public static readonly RoutedUICommand CreateRepeatGroup = new RoutedUICommand(
            "Create Repeat Group",
            "Create Repeat Group", 
            typeof(Commands));

        public static readonly RoutedUICommand RemoveRepeatGroup = new RoutedUICommand(
            "Remove Repeat Group",
            "Remove Repeat Group",
            typeof(Commands));

        public static readonly RoutedUICommand CreateMultGroup = new RoutedUICommand(
            "Create Multiply Group",
            "Create Multiply Group",
            typeof(Commands));

        public static readonly RoutedUICommand RemoveMultGroup = new RoutedUICommand(
            "Remove Multiply Group",
            "Remove Multiply Group",
            typeof(Commands));

        public static readonly RoutedUICommand DeleteSelection = new RoutedUICommand(
            "Delete Selection",
            "Delete Selection",
            typeof(Commands));
    }
}
