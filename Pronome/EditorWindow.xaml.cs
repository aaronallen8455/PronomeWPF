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

        /// <summary>
        /// The meaure marking element
        /// </summary>
        protected Rectangle MeasureTick;
        /// <summary>
        /// Spaces the size the of the measures
        /// </summary>
        protected Rectangle MeasureSizer;
        /// <summary>
        /// The element from the measure brush is created
        /// </summary>
        protected Canvas MeasureTickCanvas;
        /// <summary>
        /// The background of the LayerPanel element is set to this brush.
        /// </summary>
        protected VisualBrush MeasureBrush;
        protected double MeasureWidth;

        /// <summary>
        /// Each row's various elements are added to this panel.
        /// </summary>
        public StackPanel LayerPanel;

        /// <summary>
        /// A copy of the cells and associated groups that are on the clipboard.
        /// </summary>
        public LinkedList<Cell> CopiedCells = new LinkedList<Cell>();

        /// <summary>
        /// The scale of the spacing in the UI
        /// </summary>
        public static double Scale = 1;

        /// <summary>
        /// Determines overall scale of UI
        /// </summary>
        public const float BaseFactor = 55f;

        public EditorWindow()
        {
            InitializeComponent();

            Instance = this;

            LayerPanel = layerPanel;

            // add items to source selector
            List<string> sources = WavFileStream.FileNameIndex.Cast<string>()
                .Where((n, i) => i % 2 == 1) // get the pretty names from the odd numbered indexes
                .Select((x, i) => (i.ToString() + ".").PadRight(4) + x).ToList(); // add index numbers
            sources[0] = "Silent";
            sources.Insert(0, "Pitch");
            sources.AddRange(UserSource.Library.OrderBy(x => x.Label).Select(x => x.ToString()));
            //sources[0] = "Pitch"; // replace Silentbeat with Pitch
            sourceSelector.ItemsSource = sources;

            // init grid UI elements
            GridSizer = Resources["gridSizer"] as Rectangle;
            GridTick = Resources["gridTick"] as Rectangle;
            GridLeft = Resources["gridLeft"] as Rectangle;
            GridLeft.Margin = new Thickness(-GridTick.Width, 0, 0, 0);
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

            // init measure tick elements
            MeasureWidth = BeatCell.Parse(measureSizeInput.Text);
            MeasureTick = Resources["measureTick"] as Rectangle;
            MeasureSizer = Resources["measureSizer"] as Rectangle;
            MeasureSizer.Width = MeasureWidth * BaseFactor * Scale;
            MeasureTickCanvas = new Canvas();
            MeasureTickCanvas.Children.Add(MeasureTick);
            MeasureTickCanvas.Children.Add(MeasureSizer);
            MeasureBrush = new VisualBrush(MeasureTickCanvas);
            MeasureBrush.ViewportUnits = BrushMappingMode.Absolute;
            MeasureBrush.TileMode = TileMode.Tile;
            MeasureBrush.Viewport = new Rect(0, 0, MeasureWidth * BaseFactor * Scale, 1);
            LayerPanel.Background = MeasureBrush;

            // init the undo and redo stacks
            UndoStack = new ActionStack(undoMenuItem, 50);
            RedoStack = new ActionStack(redoMenuItem, 50);
        }

        public void BuildUI(object sender = null, EventArgs e = null)
        {
            // remove old UI
            layerPanel.Children.Clear();
            Rows.Clear();
            foreach (Layer layer in Metronome.GetInstance().Layers)
            {
                var row = new Row(layer);
                layerPanel.Children.Add(row.BaseElement);
                Rows.Add(row);
            }

            Metronome.AfterBeatParsed -= BuildUI;
            Metronome.AfterBeatParsed += BuildUI;

            // remove any undo /redo actions from old state
            UndoStack.Clear();
            RedoStack.Clear();
        }

        /// <summary>
        /// Will alter the UI to reflect a change in cell selection. Includes grid lines and inputs.
        /// </summary>
        public void UpdateUiForSelectedCell()
        {
            // don't apply source changes
            ignoreSourceChange = true;

            if (Cell.SelectedCells.Cells.Any())
            {
                // if any references are selected, don't show inputs
                if (Cell.SelectedCells.Cells.Any(x => !string.IsNullOrEmpty(x.Reference)))
                {
                    durationInput.IsEnabled = false;
                    sourceSelector.IsEnabled = false;
                    pitchInputPanel.Visibility = Visibility.Collapsed;
                    durationInput.Text = string.Empty;
                    sourceSelector.SelectedItem = null;
                    pitchInput.Text = string.Empty;
                }
                else
                {
                    durationInput.IsEnabled = true;
                    sourceSelector.IsEnabled = true;
                    string source = null;

                    if (Cell.SelectedCells.Cells.Count == 1)
                    {
                        Cell cell = Cell.SelectedCells.Cells[0];

                        durationInput.Text = cell.Value;

                        source = string.IsNullOrEmpty(cell.Source) ? cell.Row.Layer.BaseSourceName : cell.Source;
                        
                    }
                    else
                    {
                        // check if all selected cells have the same source
                        if (Cell.SelectedCells.Cells.All(x => string.Equals(x.Source, Cell.SelectedCells.Cells[0].Source)))
                        {
                            source = string.IsNullOrEmpty(
                                Cell.SelectedCells.Cells[0].Source) 
                                ? Cell.SelectedCells.Cells[0].Row.Layer.BaseSourceName 
                                : Cell.SelectedCells.Cells[0].Source;
                        }
                        else
                        {
                            // sources are not the same
                            durationInput.Text = string.Empty;
                            sourceSelector.SelectedItem = null;
                            pitchInput.Text = string.Empty;

                            // if all cells are pitches, show a blank pitch input, otherwise hide it
                            if (Cell.SelectedCells.Cells.All(x =>
                                {
                                    if (string.IsNullOrEmpty(x.Source))
                                    {
                                        return x.Row.Layer.BaseSourceName.Contains(".wav");
                                    }
                                    else
                                    {
                                        return x.Source.Contains(".wav");
                                    }
                                })
                            )
                            {
                                pitchInputPanel.Visibility = Visibility.Collapsed;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(source))
                    {
                        // is the source a pitch or a wav?
                        if (source.Contains(".wav"))
                        {
                            pitchInputPanel.Visibility = Visibility.Collapsed;
                            string name = WavFileStream.GetSelectorNameByFile(source);
                            sourceSelector.SelectedItem = name;
                        }
                        else if (source == "0")
                        {
                            // silent
                            pitchInputPanel.Visibility = Visibility.Collapsed;
                            sourceSelector.SelectedItem = "Silent";
                        }
                        else
                        {
                            // pitch
                            pitchInputPanel.Visibility = Visibility.Visible;
                            pitchInput.Text = source.TrimStart('p');
                            sourceSelector.SelectedItem = "Pitch";
                        }
                    }
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
                durationInput.IsEnabled = false;
                sourceSelector.IsEnabled = false;
                // empty the fields and remove grid
                durationInput.Text = string.Empty;
                sourceSelector.SelectedItem = null;
                pitchInput.Text = string.Empty;
                pitchInputPanel.Visibility = Visibility.Collapsed;

                RemoveGridLines();
            }

            ignoreSourceChange = false;
        }

        /// <summary>
        /// Remove the cell placement guidelines
        /// </summary>
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

            if (KeepOpen)
            {
                // if changes are not applied, prompt to discard, apply, or cancel
                bool close = true;
                if (!GetChangesApplied())
                {
                    var dialog = new Pronome.Classes.Editor.ConfirmCloseDialog();
                    dialog.Owner = this;
                    bool? dialogResult = dialog.ShowDialog();

                    if (dialogResult == true && !dialog.Discard)
                    {
                        applyChangesButton_Click(null, null);
                    }
                    else if (dialogResult == false)
                    {
                        close = false;
                    }
                }

                if (close)
                {
                    Cell.SelectedCells.Clear();
                    SetCellSelected(false);
                    UpdateUiForSelectedCell();
                    SetChangesApplied(true);
                    Metronome.AfterBeatParsed -= BuildUI;
                    Hide();
                }
                e.Cancel = true;
            }
        }

        public void SetCellSelected(bool selected)
        {
            Resources["cellSelected"] = selected;
        }

        /// <summary>
        /// Whether the changes have been applied to the beat. Used by the beat stop button to enable the CA button if needed.
        /// </summary>
        public bool ChangesApplied = true;

        /// <summary>
        /// Set the boolean that controls whether changes have been applied to Row's Layer.
        /// </summary>
        /// <param name="applied"></param>
        public void SetChangesApplied(bool applied)
        {
            ChangesApplied = applied;
            // don't set the resource if beat is playing.
            if (applied || !applied && Metronome.GetInstance().PlayState == Metronome.State.Stopped)
            {
                Resources["changesApplied"] = !applied;
            }
        }

        public bool GetChangesApplied()
        {
            return ChangesApplied;//!(bool)Resources["changesApplied"];
        }

        private void applyChangesButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                bool changesMade = false;
                foreach (Row row in Rows)
                {
                    if (!row.BeatCodeIsCurrent) row.UpdateBeatCode();
                    string beatCode = row.BeatCode;

                    if (beatCode != row.Layer.ParsedString || row.Layer.ParsedOffset != row.OffsetValue)
                    {
                        changesMade = true;
                        row.Layer.UI.textEditor.Text = beatCode;
                        row.Layer.ParsedOffset = row.OffsetValue;
                        row.Layer.UI.SetOffsetValue(row.OffsetValue);
                        row.Layer.Offset = row.Offset;
                        row.Layer.Parse(beatCode);
                        // redraw beat graph / bounce if necessary
                    }
                }
                if (changesMade)
                {
                    Metronome.AfterBeatParsed -= BuildUI; // don't rebuild UI
                    Metronome.GetInstance().TriggerAfterBeatParsed();
                    Metronome.AfterBeatParsed += BuildUI;
                }
            }));

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
            }
        }

        private bool ignoreSourceChange = false;
        private void sourceSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //if ((sender as ComboBox).SelectedValue == null) return;

            if (e.AddedItems.Count == 1 && !ignoreSourceChange)
            {
                string value = (sender as ComboBox).SelectedValue.ToString();
                string source = "";

                if (value == "Pitch")
                {
                    string pitchValue = pitchInput.Text;

                    // validate pitch input
                    if (Regex.IsMatch(pitchValue, @"^[a-gA-G][#b]?\d+$|^\d+\.?\d*"))
                    {
                        source = pitchValue;
                        // add 'p' if it's a numeric pitch
                        if (char.IsNumber(pitchValue[0]))
                        {
                            source = 'p' + source;
                        }
                    }
                }
                else if (value == "Silent")
                {
                    source = "0";
                }
                else
                {
                    value = Regex.Replace(value, @"^\d+\.\s*", "");
                    // get wav source index
                    source = WavFileStream.GetFileByName(value);
                }

                bool wasChanged = false;
                // set new source on selected cells
                foreach (Cell c in Cell.SelectedCells.Cells)
                {
                    string old = c.Source == null ? c.Row.Layer.BaseSourceName : c.Source;
                    if (old != source) wasChanged = true;
                    c.Source = source;
                }

                if (Cell.SelectedCells.Cells.Any() && wasChanged)
                {
                    Cell.SelectedCells.Cells[0].Row.BeatCodeIsCurrent = false;
                    SetChangesApplied(false);
                    UpdateUiForSelectedCell();
                }
            }
        }

        private void pitchInput_LostFocus(object sender, RoutedEventArgs e)
        {
            string pitchValue = (sender as TextBox).Text;

            // validate pitch input
            if (Regex.IsMatch(pitchValue, @"^[a-gA-G][#b]?\d{0,2}$|^\d+\.?\d*"))
            {
                // add 'p' if it's a numeric pitch
                if (char.IsNumber(pitchValue[0]))
                {
                    pitchValue = 'p' + pitchValue;
                }
                else if (!char.IsDigit(pitchValue.Last()))
                {
                    pitchValue += '4'; // default octave is 4
                }

                // assign to cells
                foreach (Cell c in Cell.SelectedCells.Cells)
                {
                    c.Source = pitchValue;
                }

                if (Cell.SelectedCells.Cells.Any())
                {
                    Cell.SelectedCells.Cells[0].Row.BeatCodeIsCurrent = false;
                    SetChangesApplied(false);
                    //UpdateUiForSelectedCell();
                }
            }
        }

        /// <summary>
        /// The spacing between grid lines in BPM
        /// </summary>
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
            double bpmPosition = e.GetPosition((StackPanel)sender).X / Scale / BaseFactor;
            // start at measure 1
            int measure = (int)(bpmPosition / MeasureWidth) + 1;
            // the bpm into the current measure
            string beat = (bpmPosition - (measure - 1) * MeasureWidth).ToString("0.00");

            mousePositionText.Text = $"{measure.ToString()} : {beat}";
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
            e.CanExecute = GroupCommandHelper<RepeatGroupCommandHelper>.GetResult().CanRemoveOrEdit;
        }

        private void RemoveRepeatGroup_Executed(object sender, ExecutedRoutedEventArgs e)
        {
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
            dialog.Factor = group.FactorValue;
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
                double move = BeatCell.Parse(CurrentIncrement);// + .0001;
                Cell first = Cell.SelectedCells.FirstCell;
                // if selection at start of row, check against the offset
                if (first == first.Row.Cells[0])
                {
                    if (first.Row.Offset >= move)
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
                            if (BeatCell.Parse(belowGroup.LastTermModifier) >= move)
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
                    if (last.Duration >= move)// + .0001)
                    {
                        e.CanExecute = true;
                    }
                }
            }
        }

        private void CreateReference_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (Cell.SelectedCells.Cells.Count == 1 && 
                string.IsNullOrEmpty(Cell.SelectedCells.FirstCell.Reference) &&
                !(Rows.Count == 1 && Rows[0].Cells.Count == 1))
            {
                e.CanExecute = true;
            }
        }

        private void CreateReference_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Cell cell = Cell.SelectedCells.FirstCell;
            // open dialog to get ref index
            var dialog = new Classes.Editor.ReferenceDialog();
            AddReference action;
            // are we creating or editing?
            if (!string.IsNullOrEmpty(cell.Reference))
            {
                int r = cell.Reference.ToLower() == "s" ? cell.Row.Index + 1 : int.Parse(cell.Reference);
                dialog.ReferenceIndex = r;
                if (dialog.ShowDialog() == true)
                {
                    action = new EditReference(Cell.SelectedCells.FirstCell, dialog.ReferenceIndex);
                    action.Redo();
                    AddUndoAction(action);
                }
            }
            else if (dialog.ShowDialog() == true)
            {
                action = new AddReference(Cell.SelectedCells.FirstCell, dialog.ReferenceIndex);
                action.Redo();
                AddUndoAction(action);
            }
        }

        private void EditReference_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (Cell.SelectedCells.Cells.Count == 1 && !string.IsNullOrEmpty(Cell.SelectedCells.FirstCell.Reference))
            {
                e.CanExecute = true;
            }
        }          
                       
        private void RemoveReference_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RemoveReference action = new RemoveReference(Cell.SelectedCells.FirstCell);
            action.Redo();
            AddUndoAction(action);
        }

        private void CopyCells_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // empty clipboard
            CopiedCells.Clear();
            // copy the selected cells to the clipboard

            // original is key, copy is value
            Dictionary<RepeatGroup, RepeatGroup> CopiedRepeatGroups = new Dictionary<RepeatGroup, RepeatGroup>();
            Dictionary<MultGroup, MultGroup> CopiedMultGroups = new Dictionary<MultGroup, MultGroup>();
            foreach (Cell c in Cell.SelectedCells.Cells.Where(x => !x.IsReference))
            {
                Cell copy = new Cell(c.Row);
                copy.Value = c.Value;
                copy.Source = c.Source;
                copy.Reference = c.Reference;
                copy.IsBreak = c.IsBreak;

                // if this is the first cell and is the first cell of a rep group or mult group, copy those groups
                foreach (RepeatGroup rg in c.RepeatGroups)
                {
                    RepeatGroup rgCopy = null;
                    if (CopiedRepeatGroups.ContainsKey(rg))
                    {
                        rgCopy = CopiedRepeatGroups[rg];
                    }
                    else if (rg.Cells.First.Value == c
                        && (!Cell.SelectedCells.LastCell.RepeatGroups.Contains(rg)
                        || rg.Cells.Last.Value == Cell.SelectedCells.LastCell))
                    {
                        rgCopy = new RepeatGroup();
                        CopiedRepeatGroups.Add(rg, rgCopy);
                        rgCopy.Times = rg.Times;
                    }

                    if (rgCopy != null)
                    {
                        rgCopy.Cells.AddLast(copy);
                        copy.RepeatGroups.AddLast(rg);
                    }
                }

                foreach (MultGroup mg in c.MultGroups)
                {
                    MultGroup mgCopy = null;
                    if (CopiedMultGroups.ContainsKey(mg))
                    {
                        mgCopy = CopiedMultGroups[mg];
                    }
                    else if (mg.Cells.First.Value == c
                        && (!Cell.SelectedCells.LastCell.MultGroups.Contains(mg)
                        || mg.Cells.Last.Value == Cell.SelectedCells.LastCell))
                    {
                        mgCopy = new MultGroup();
                        mgCopy.FactorValue = mg.FactorValue;
                        CopiedMultGroups.Add(mg, mgCopy);
                    }

                    if (mgCopy != null)
                    {
                        mgCopy.Cells.AddLast(copy);
                        copy.MultGroups.AddLast(mg);
                    }
                }

                // add copy to clipboard
                CopiedCells.AddLast(copy);
            }
        }

        private void CutCells_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CopyCells_Executed(null, null);

            RemoveCells action = new RemoveCells(Cell.SelectedCells.Cells.ToArray());
            action.HeaderText = "Cut";
            action.Redo();
            AddUndoAction(action);
        }

        private void PasteCells_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = Cell.SelectedCells.Cells.Any() && CopiedCells.Any();
        }

        private void PasteCells_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // replace the selected cell(s) with the clipboarded cells
            Row row = Cell.SelectedCells.FirstCell.Row;
            int index = row.Cells.IndexOf(Cell.SelectedCells.FirstCell);
            int rightIndex = index + Cell.SelectedCells.Cells.Count;
            // get groups that need to transfered to the new cells
            PasteCells action = new PasteCells(index, row, CopiedCells, Cell.SelectedCells.Cells.ToArray(), rightIndex);
            action.Redo();
            AddUndoAction(action);
        }

        /// <summary>
        /// Set the measure size when input changes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void measureSizeInput_LostFocus(object sender, RoutedEventArgs e)
        {
            string input = (sender as TextBox).Text;
            // validate the measure size input
            if (BeatCell.TryParse(input, out double bpm) && bpm > 0)
            {
                LayerPanel.Background = MeasureBrush;

                MeasureWidth = bpm;// * BaseFactor * Scale;

                // resize the measure tick
                ResizeMeasures();
            }
            else if (bpm == 0)
            {
                LayerPanel.Background = Brushes.Transparent;
            }
        }

        protected void ResizeMeasures()
        {
            double width = MeasureWidth * Scale * BaseFactor;
            MeasureSizer.Width = width;
            MeasureBrush.Viewport = new Rect(0, 0, width, 1);
        }

        private void ToolbarSelector_Click(object sender, RoutedEventArgs e)
        {
            string newIncrement = (sender as Button).ToolTip.ToString();

            incrementInput.Text = newIncrement;
            CurrentIncrement = newIncrement;
            
            UpdateUiForSelectedCell();
        }

        private void zoomComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                string newValue = (e.AddedItems[0] as ComboBoxItem).Content?.ToString().TrimEnd('%');

                if (newValue != null && double.TryParse(newValue, out double percent))
                {
                    ChangeZoom(percent);
                }
            }
        }

        private void ChangeZoom(double percent)
        {
            // set the scale amount
            Scale = percent / 100;

            // redraw the UI for all Rows
            foreach (Row row in Rows)
            {
                // preserve selection
                int selectionStart = -1;
                int selectionEnd = -1;
                if (Cell.SelectedCells.Cells.Any() && Cell.SelectedCells.FirstCell.Row == row)
                {
                    selectionStart = row.Cells.IndexOf(Cell.SelectedCells.FirstCell);
                    selectionEnd = row.Cells.IndexOf(Cell.SelectedCells.LastCell);
                }

                row.Redraw();

                if (selectionStart > -1)
                {
                    Cell.SelectedCells.SelectRange(selectionStart, selectionEnd, row);
                }
            }

            ResizeMeasures();
        }

        private void zoomComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            string text = (sender as ComboBox).Text.TrimEnd('%');

            if (double.TryParse(text, out double percent) && percent > 0)
            {
                ChangeZoom(percent);

                zoomComboBox.Text = text + '%';
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

        public static readonly RoutedUICommand MoveCellsLeft = new RoutedUICommand(
            "Move Cells Left",
            "Move Cells Left",
            typeof(Commands));

        public static readonly RoutedUICommand MoveCellsRight = new RoutedUICommand(
            "Move Cells Right",
            "Move Cells Right",
            typeof(Commands));

        public static readonly RoutedUICommand CreateReference = new RoutedUICommand(
            "Create Reference",
            "Create Reference",
            typeof(Commands));

        public static readonly RoutedUICommand EditReference = new RoutedUICommand(
            "Edit Reference",
            "Edit Reference",
            typeof(Commands));

        public static readonly RoutedUICommand RemoveReference = new RoutedUICommand(
            "Remove Reference",
            "Remove Reference",
            typeof(Commands));

        public static readonly RoutedUICommand CopyCells = new RoutedUICommand(
            "Copy Cells",
            "Copy Cells",
            typeof(Commands));

        public static readonly RoutedUICommand CutCells = new RoutedUICommand(
            "Cut Cells",
            "Cut Cells",
            typeof(Commands));

        public static readonly RoutedUICommand PasteCells = new RoutedUICommand(
            "Paste Cells",
            "Paste Cells",
            typeof(Commands));
    }
}
