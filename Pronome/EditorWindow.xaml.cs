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

        public List<Row> Rows = new List<Row>();

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

        ///// <summary>
        ///// Facilitates getting the CanExecute data for the repeat group UICommands
        ///// </summary>
        //private class RepeatGroupCommandHelper
        //{
        //    static protected RepeatGroupCommandHelper Result;
        //
        //    static protected int TimesAccessed = 0;
        //
        //    /// <summary>
        //    /// The number of commands that can access the result before clearing the cached result.
        //    /// </summary>
        //    const int MaxAccessTimes = 3;
        //
        //    public bool CanAdd;
        //
        //    public bool CanRemoveOrEdit;
        //
        //    public RepeatGroup GroupToRemoveOrEdit;
        //
        //    static public RepeatGroup GetGroupToRemoveOrEdit()
        //    {
        //        if (Result != null)
        //        {
        //            return Result.GroupToRemoveOrEdit;
        //        }
        //
        //        return null;
        //    }
        //
        //    static public RepeatGroupCommandHelper GetResult()
        //    {
        //        if (Result != null && TimesAccessed++ < MaxAccessTimes)
        //        {
        //            return Result;
        //        }
        //
        //        Result = new RepeatGroupCommandHelper();
        //        TimesAccessed = 1;
        //        return Result;
        //    }
        //
        //    public RepeatGroupCommandHelper()
        //    {
        //        if (Cell.SelectedCells.Cells.Count == 1)
        //        {
        //            // if a single cell selected, no further validation
        //            if (Cell.SelectedCells.FirstCell.RepeatGroups.Any() &&
        //                Cell.SelectedCells.Cells[0].RepeatGroups.Last.Value.Cells.First == Cell.SelectedCells.Cells[0].RepeatGroups.Last.Value.Cells.Last)
        //            {
        //                // not if a single cell repeat already exists over this cell.
        //                //e.CanExecute = false;
        //                CanAdd = false;
        //                CanRemoveOrEdit = true;
        //                GroupToRemoveOrEdit = Cell.SelectedCells.FirstCell.RepeatGroups.Last.Value;
        //                return;
        //            }
        //            else
        //            {
        //                CanAdd = true;
        //                CanRemoveOrEdit = false;
        //                GroupToRemoveOrEdit = null;
        //                return;
        //            }
        //        }
        //
        //        // Ensure that the selected cells share grouping scope
        //        LinkedListNode<RepeatGroup> first = Cell.SelectedCells.FirstCell.RepeatGroups.First;
        //        LinkedListNode<RepeatGroup> last = Cell.SelectedCells.LastCell.RepeatGroups.First;
        //        while (true)
        //        {
        //            // both cells share this group, go to nested group
        //            if (first != null && last != null)
        //            {
        //                if (first.Value == last.Value)
        //                {
        //                    // don't allow a repeat group to be made right on top of another RG
        //                    if (first.Value.Cells.First.Value != Cell.SelectedCells.FirstCell
        //                        && first.Value.Cells.Last.Value != Cell.SelectedCells.LastCell)
        //                    {
        //                        first = first.Next;
        //                        last = last.Next;
        //                    }
        //                    else
        //                    {
        //                        CanAdd = false;
        //                        CanRemoveOrEdit = true;
        //                        GroupToRemoveOrEdit = first.Value;
        //                        break;
        //                    }
        //                }
        //                else if (first.Value.Cells.First.Value == Cell.SelectedCells.FirstCell &&
        //                        last.Value.Cells.Last.Value == Cell.SelectedCells.LastCell)
        //                {
        //                    // both ends of select are in different groups but those groups are not being cut
        //                    CanAdd = true;
        //                    CanRemoveOrEdit = false;
        //                    GroupToRemoveOrEdit = null;
        //                    break;
        //                }
        //                else
        //                {
        //                    CanAdd = false;
        //                    CanRemoveOrEdit = false;
        //                    GroupToRemoveOrEdit = null;
        //                    break;
        //                }
        //            }
        //            // is last cell in nested repeat group where it is the last cell?
        //            else if (first == null && last != null)
        //            {
        //                if (last.Value.Cells.Last.Value == Cell.SelectedCells.LastCell)
        //                {
        //                    CanAdd = true;
        //                    CanRemoveOrEdit = false;
        //                    GroupToRemoveOrEdit = null;
        //                    break;
        //                }
        //                else
        //                {
        //                    CanAdd = false;
        //                    CanRemoveOrEdit = false;
        //                    GroupToRemoveOrEdit = null;
        //                    break;
        //                }
        //            }
        //            // is first cell in nested rep group and is the first cell of that group?
        //            else if (first != null && last == null)
        //            {
        //                if (first.Value.Cells.First.Value == Cell.SelectedCells.FirstCell)
        //                {
        //                    CanAdd = true;
        //                    CanRemoveOrEdit = false;
        //                    GroupToRemoveOrEdit = null;
        //                    break;
        //                }
        //                else
        //                {
        //                    CanAdd = false;
        //                    CanRemoveOrEdit = false;
        //                    GroupToRemoveOrEdit = null;
        //                    break;
        //                }
        //            }
        //
        //            // reached the end
        //            if (first == null && last == null)
        //            {
        //                CanAdd = true;
        //                CanRemoveOrEdit = false;
        //                GroupToRemoveOrEdit = null;
        //                break;
        //            }
        //        }
        //    }
        //}

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

        /// <summary>
        /// Set the boolean that controls whether changes have been applied to Row's Layer.
        /// </summary>
        /// <param name="applied"></param>
        public void SetChangesApplied(bool applied)
        {
            Resources["changesApplied"] = !applied;
        }

        private void applyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            bool changesMade = false;
            foreach (Row row in Rows)
            {
                // TODO: what if a layer was removed / created while editor is open?
                if (!row.BeatCodeIsCurrent) row.UpdateBeatCode();
                string beatCode = row.BeatCode;

                if (beatCode != row.Layer.ParsedString)
                {
                    changesMade = true;
                    row.Layer.UI.textEditor.Text = beatCode;
                    row.Layer.Parse(beatCode);
                    // redraw beat graph / bounce if necessary
                }
            }
            if (changesMade)
            {
                Metronome.GetInstance().TriggerAfterBeatParsed();
            }

            SetChangesApplied(true);
        }

        private void durationInput_LostFocus(object sender, RoutedEventArgs e)
        {
            string value = ((TextBox)sender).Text;
            // validate
            double duration;
            if (BeatCell.TryParse(value, out duration))
            {
                // the action to the undo stack
                CellDuration action = new CellDuration(Cell.SelectedCells.Cells.ToArray(), value);
                UndoStack.Push(action);

                action.Redo();
                //foreach(Cell cell in Cell.SelectedCells.Cells)
                //{
                //    cell.Duration = duration;
                //    cell.Value = value;
                //}

                //SetChangesApplied(false);
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

            if (BeatCell.ValidateExpression(input))
            {
                if (Cell.SelectedCells.Cells.Any())
                {
                    RemoveGridLines();
                    // draw new grid
                    Cell.SelectedCells.Cells.First().Row.DrawGridLines(input);
                }
                CurrentIncrement = input;
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
            //e.CanExecute = RepeatGroupCommandHelper.GetResult().CanAdd;
            e.CanExecute = GroupCommandHelper<RepeatGroupCommandHelper>.GetResult().CanAdd;
        }

        private void CreateRepeatGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new Classes.Editor.RepeatGroupDialog();

            if (dialog.ShowDialog() == true)
            {
                int times = dialog.Times;
                string lastTermModifier = dialog.LastTermModifier;

                AddRepeatGroup action = new AddRepeatGroup(Cell.SelectedCells.Cells.ToArray(), times, lastTermModifier);

                action.Redo();

                AddUndoAction(action);
            }
        }

        private void RemoveRepeatGroup_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            //e.CanExecute = RepeatGroupCommandHelper.GetResult().CanRemoveOrEdit;
            e.CanExecute = GroupCommandHelper<RepeatGroupCommandHelper>.GetResult().CanRemoveOrEdit;
        }

        private void RemoveRepeatGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            //RepeatGroup group = RepeatGroupCommandHelper.GetGroupToRemoveOrEdit();
            RepeatGroup group = GroupCommandHelper<RepeatGroupCommandHelper>.GetGroupToRemoveOrEdit() as RepeatGroup;

            RemoveRepeatGroup action = new RemoveRepeatGroup(group);

            action.Redo();

            AddUndoAction(action);
        }

        private void DeleteSelection_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Cell.SelectedCells.Cells.Any();
        }

        private void DeleteSelection_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Cell[] selection = Cell.SelectedCells.Cells.ToArray();

            // don't delete entire row
            if (selection.Length < selection[0].Row.Cells.Count)
            {
                RemoveCells removeAction = new RemoveCells(selection);
                // execute the action
                removeAction.Redo();
                // add to undo stack
                AddUndoAction(removeAction);

                SetChangesApplied(false);
            }
        }

        private void EditRepeatGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new Classes.Editor.RepeatGroupDialog();
            //RepeatGroup group = RepeatGroupCommandHelper.GetGroupToRemoveOrEdit();
            RepeatGroup group = GroupCommandHelper<RepeatGroupCommandHelper>.GetGroupToRemoveOrEdit() as RepeatGroup;
            dialog.Times = group.Times;
            dialog.LastTermModifier = group.LastTermModifier;

            if (dialog.ShowDialog() == true)
            {
                int times = dialog.Times;
                string lastTermModifier = dialog.LastTermModifier;

                EditRepeatGroup action = new EditRepeatGroup(group, times, lastTermModifier);

                action.Redo();
                
                AddUndoAction(action);
            }
        }

        private void Deselect_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Cell.SelectedCells.Cells.Any();
        }

        private void Deselect_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Cell.SelectedCells.DeselectAll();
        }

        private void CreateMultGroup_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var result = GroupCommandHelper<MultGroupCommandHelper>.GetResult();
            e.CanExecute = result.CanAdd;
        }

        private void CreateMultGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = new Classes.Editor.MultGroupDialog();
            // open dialogue
            if (dialog.ShowDialog() == true)
            {
                var action = new AddMultGroup(Cell.SelectedCells.Cells.ToArray(), dialog.Factor);

                action.Redo();

                AddUndoAction(action);
            }
        }

        private void RemoveMultGroup_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var result = GroupCommandHelper<MultGroupCommandHelper>.GetResult();
            e.CanExecute = result.CanRemoveOrEdit;
        }

        private void RemoveMultGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MultGroup group = GroupCommandHelper<MultGroupCommandHelper>.GetGroupToRemoveOrEdit() as MultGroup;
            var action = new RemoveMultGroup(group);
            action.Redo();
            AddUndoAction(action);
        }

        private void EditMultGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            MultGroup group = GroupCommandHelper<MultGroupCommandHelper>.GetGroupToRemoveOrEdit() as MultGroup;
            var dialog = new Classes.Editor.MultGroupDialog();
            dialog.Factor = group.Factor;
            if (dialog.ShowDialog() == true)
            {
                var action = new EditMultGroup(group, dialog.Factor);
                action.Redo();
                AddUndoAction(action);
            }
        }

        private void MoveCellsLeft_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (Cell.SelectedCells.Cells.Any())
            {
                double move = BeatCell.Parse(CurrentIncrement) + .0001;
                Cell first = Cell.SelectedCells.FirstCell;
                // if selection at start of row, check against the offset
                if (first == first.Row.Cells[0])
                {
                    if (first.Row.Offset > move)
                    {
                        e.CanExecute = true;
                    }
                }
                else
                {
                    // check if selection is in front of a rep group or a cell
                    // if below cell is a reference, cancel
                    Cell below = first.Row.Cells[first.Row.Cells.IndexOf(first) - 1];
                    if (string.IsNullOrEmpty(below.Reference))
                    {
                        RepeatGroup belowGroup = null;
                        if (below.RepeatGroups.Any())
                        {
                            belowGroup = below.RepeatGroups.Where(x => x.Cells.Last.Value == below).Last();
                        }

                        // if above rep group, check against the LTM
                        if (belowGroup != null)
                        {
                            if (BeatCell.Parse(belowGroup.LastTermModifier) > move)
                            {
                                e.CanExecute = true;
                            }
                        }
                        else
                        {
                            // check against below cell's value
                            if (below.Duration > move)
                            {
                                e.CanExecute = true;
                            }
                        }
                    }
                }
            }
        }

        private void MoveCells_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int times = int.Parse(e.Parameter.ToString());
            var action = new MoveCells(Cell.SelectedCells.Cells.ToArray(), CurrentIncrement, times);

            action.Redo();

            AddUndoAction(action);
        }

        private void MoveCellsRight_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (Cell.SelectedCells.Cells.Any())
            {
                Cell last = Cell.SelectedCells.LastCell;
                // if last is last of row, then we can execute
                if (last == last.Row.Cells.Last())
                {
                    e.CanExecute = true;
                }
                else
                {
                    // check that last's value is greater than the move amount.
                    double move = BeatCell.Parse(CurrentIncrement);
                    if (last.Duration > move + .0001)
                    {
                        e.CanExecute = true;
                    }
                }
            }
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

        public static readonly RoutedUICommand EditRepeatGroup = new RoutedUICommand(
            "Edit Repeat Group",
            "Edit Repeat Group",
            typeof(Commands));

        public static readonly RoutedUICommand CreateMultGroup = new RoutedUICommand(
            "Create Multiply Group",
            "Create Multiply Group",
            typeof(Commands));

        public static readonly RoutedUICommand RemoveMultGroup = new RoutedUICommand(
            "Remove Multiply Group",
            "Remove Multiply Group",
            typeof(Commands));

        public static readonly RoutedUICommand EditMultGroup = new RoutedUICommand(
            "Edit Multiply Group",
            "Edit Multiply Group",
            typeof(Commands));

        public static readonly RoutedUICommand DeleteSelection = new RoutedUICommand(
            "Delete Selection",
            "Delete Selection",
            typeof(Commands));

        public static readonly RoutedCommand MoveCellsLeft = new RoutedCommand(
            "Move Cells Left",
            typeof(Commands));

        public static readonly RoutedUICommand MoveCellsRight = new RoutedUICommand(
            "Move Cells Right",
            "Move Cells Right",
            typeof(Commands));
    }
}
