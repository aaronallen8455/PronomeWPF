﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pronome.Editor
{
    public class Cell : IComparable
    {
        public Row Row;
        public Rectangle Rectangle;

        /// <summary>
        /// Currently selected cells
        /// </summary>
        public static Selection SelectedCells = new Selection();

        protected double _duration;
        /// <summary>
        /// The cell's value in BPM
        /// </summary>
        public double Duration
        {
            get => _duration;
            set {
                //HashSet<RepeatGroup> touchedRepGroups = new HashSet<RepeatGroup>();
                //HashSet<MultGroup> touchedMultGroups = new HashSet<MultGroup>();
                HashSet<Group> touchedGroups = new HashSet<Group>();
                double diff = value - _duration;
                _duration = value;
                // resize groups of which this cell is a part
                foreach (RepeatGroup rg in RepeatGroups)
                {
                    touchedGroups.Add(rg);
                    rg.Duration += diff;
                }
                foreach (MultGroup mg in MultGroups)
                {
                    touchedGroups.Add(mg);
                    mg.Duration += diff;
                }
                // reposition all subsequent cells and groups and references
                foreach (Cell cell in Row.Cells.SkipWhile(x => x != this).Skip(1))
                {
                    cell.Position += diff;
                    // reposition reference rect
                    if (cell.ReferenceRectangle != null)
                    {
                        double cur = Canvas.GetLeft(cell.ReferenceRectangle);
                        Canvas.SetLeft(cell.ReferenceRectangle, cur + diff);
                    }
                }
                // reposition groups
                foreach (RepeatGroup rg in Row.RepeatGroups.Where(x => !touchedGroups.Contains(x) && x.Position > Position))
                {
                    rg.Position += diff;
                }
                foreach (MultGroup mg in Row.MultGroups.Where(x => !touchedGroups.Contains(x) && x.Position > Position))
                {
                    mg.Position += diff;
                }
                // resize sizer
                Row.ChangeSizerWidthByAmount(diff);
            }
        }

        protected double _actualDuration = -1;
        /// <summary>
        /// Get the duration of the cell with multiplication groups applied
        /// </summary>
        public double ActualDuration
        {
            get => Duration;
        }

        protected string _value;
        /// <summary>
        /// The string representation of the duration. ie 1+2/3
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                // update the duration UI input if this is the only selected cell
                if (IsSelected && SelectedCells.Cells.Count == 1)
                {
                    EditorWindow.Instance.durationInput.Text = value;
                }

                _value = value;
            }
        }

        protected double _position;
        /// <summary>
        /// The horizontal position of the cell in BPM. Changes actual position when set.
        /// </summary>
        public double Position
        {
            get => _position;
            set
            {
                _position = value;
                Canvas.SetLeft(Rectangle, value * EditorWindow.Scale * EditorWindow.BaseFactor);

                //// if this is first cell in row, adjust the row offset
                //if (Row.Cells.Any() && Row.Cells.First.Value == this)
                //{
                //    Row.Offset = value;
                //}
            }
        }
        public bool IsSelected = false;

        /// <summary>
        /// Whether cell is a break point for a loop - |
        /// </summary>
        public bool IsBreak = false;

        /// <summary>
        /// Aggregation of the mult factors for this cell from mult groups it's a member of
        /// </summary>
        public string MultFactor = "1";

        protected string MultipliedValue;

        /// <summary>
        /// The group actions that occur at this cell. First part of tuple is true if group was begun, false if ended.
        /// </summary>
        public LinkedList<(bool, Group)> GroupActions = new LinkedList<(bool, Group)>();

        /// <summary>
        /// The index of the layer that this cell is a reference for. Null if it's a regular cell.
        /// </summary>
        public string Reference;
        //public CellRepeat Repeat;
        /// <summary>
        /// The audio source for this cell.
        /// </summary>
        public ISoundSource Source = null;

        /// <summary>
        /// Multiplication groups that this cell is a part of. Outermost group is first
        /// </summary>
        public LinkedList<MultGroup> MultGroups = new LinkedList<MultGroup>();

        /// <summary>
        /// Repeat groups that this cell is part of. Outermost group is first
        /// </summary>
        public LinkedList<RepeatGroup> RepeatGroups = new LinkedList<RepeatGroup>();

        /// <summary>
        /// Is this cell part of a reference. Should not be manipulable if so
        /// </summary>
        public bool IsReference = false;

        /// <summary>
        /// Drawn in place of cell rect if this is a reference. Denotes a referenced block.
        /// </summary>
        public Rectangle ReferenceRectangle;// = EditorWindow.Instance.Resources["referenceRectangle"] as Rectangle;

        public Cell(Row row)
        {
            Row = row;
            Rectangle = EditorWindow.Instance != null ? EditorWindow.Instance.Resources["cellRectangle"] as Rectangle : new Rectangle();
            //Rectangle.Height = (double)EditorWindow.Instance.Resources["cellHeight"];

            // the ref rect
            ReferenceRectangle = EditorWindow.Instance != null ? EditorWindow.Instance.Resources["referenceRectangle"] as Rectangle : new Rectangle();
            // set Canvas.Top
            double top = EditorWindow.Instance == null ? 0 : (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - (double)EditorWindow.Instance.Resources["cellHeight"] / 2;
            Canvas.SetTop(Rectangle, top);
            Canvas.SetTop(ReferenceRectangle, top);

            Panel.SetZIndex(Rectangle, 10);
            Rectangle.MouseLeftButtonDown += Rectangle_MouseDown;
            Rectangle.MouseLeftButtonUp += Rectangle_MouseLeftButtonUp;
            ReferenceRectangle.MouseLeftButtonDown += Rectangle_MouseDown;
            ReferenceRectangle.MouseLeftButtonUp += Rectangle_MouseLeftButtonUp;
            Rectangle.MouseLeave += Rectangle_MouseLeave;
            ReferenceRectangle.MouseMove += Rectangle_MouseLeave;
        }

        /// <summary>
        /// Begin the dragging action
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rectangle_MouseLeave(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed && !Row.IsDraggingCell)
            {
                Row.BeginDraggingCell(e.GetPosition(Row.BaseElement).X / EditorWindow.Scale / EditorWindow.BaseFactor);
            }
        }

        /// <summary>
        /// Select the cell
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rectangle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!Row.IsDraggingCell)
            {
                if (!IsReference) // ref cells should not be manipulable
                {
                    ToggleSelect();

                    EditorWindow.Instance.UpdateUiForSelectedCell();
                }

                e.Handled = true;
            }
        }

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        public void ToggleSelect(bool Clicked = true)
        {
            IsSelected = !IsSelected;
            // set selection color
            Rectangle.Stroke = IsSelected ? System.Windows.Media.Brushes.DeepPink : System.Windows.Media.Brushes.Black;
            // select the reference rect if this cell is a ref
            if (!string.IsNullOrEmpty(Reference))
            {
                ReferenceRectangle.Opacity += .4 * (IsSelected ? 1 : -1);
            }
            // if not a multi select, deselect currently selected cell(s)
            if (IsSelected)
            {
                if (Clicked)
                {
                    // multiSelect
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        int start = -1;
                        int end = -1;
                        for (int i = 0; i < Row.Cells.Count; i++)
                        {
                            if (start == -1 && Row.Cells[i].IsSelected)
                            {
                                start = i;
                                end = i;
                            }
                            else if (Row.Cells[i].IsSelected || Row.Cells[i] == this)
                            {
                                end = i;
                            }
                        }
                        // have to set this otherwise it will get flipped by SelectRange.
                        IsSelected = false;

                        SelectedCells.SelectRange(start, end, Row, false);

                        return;
                    }
                    else
                    { // single select : deselect others
                        foreach (Cell c in SelectedCells.Cells.ToArray())
                        {
                            c.ToggleSelect(false);
                        }
                    }
                }

                SelectedCells.Cells.Add(this);
                EditorWindow.Instance.SetCellSelected(true);
            }
            else if (!IsSelected)
            { // deselect if not a cell in a multi-select being clicked
                if (SelectedCells.Cells.Count > 1 && Clicked)
                {
                    //ToggleSelect(false);
                    foreach (Cell c in SelectedCells.Cells.ToArray())
                    {
                        c.ToggleSelect(false);
                    }
                }

                SelectedCells.Cells.Remove(this);
                if (!SelectedCells.Cells.Any())
                {
                    // no cells selected
                    EditorWindow.Instance.SetCellSelected(false);
                }
            }
        }

        /// <summary>
        /// Assign a new duration with altering the UI
        /// </summary>
        /// <param name="duration"></param>
        public void SetDurationDirectly(double duration)
        {
            _duration = duration;
            //_actualDuration = -1; // reevaluate actual duration
        }

        public int CompareTo(object obj)
        {
            if (obj is Cell cell)
            {
                return Position > cell.Position ? 1 : -1;
            }
            return 0;
        }

        /// <summary>
        /// Gets the string value with mult factors applied.
        /// </summary>
        /// <returns>The value with mult factors.</returns>
        public string GetValueWithMultFactors(bool ignoreScaleSetting = false)
        {
            // don't operate if scaling is disabled
            if (string.IsNullOrEmpty(MultFactor) || (!ignoreScaleSetting && !UserSettings.GetSettings().DrawMultToScale))
            {
                return Value;
            }

            if (string.IsNullOrEmpty(MultipliedValue))
            {
                MultipliedValue = BeatCell.MultiplyTerms(Value, MultFactor);
            }

            return MultipliedValue;
        }

        /// <summary>
        /// Divides the given string value by the factors of all nested mult groups.
        /// This is used to convert a "to scale" value to the actual value.
        /// </summary>
        /// <returns>The value divided by mult factors.</returns>
        /// <param name="value">Value.</param>
        public string GetValueDividedByMultFactors(string value, bool ignoreScaleSetting = false)
        {
            // don't operate if scaling is disabled
            if (string.IsNullOrEmpty(MultFactor) || (!ignoreScaleSetting && !UserSettings.GetSettings().DrawMultToScale))
            {
                return value;
            }

            return BeatCell.DivideTerms(value, MultFactor);
        }

        /// <summary>
        /// Resets the multiplied value so that it will be recalculated against a new cell value.
        /// </summary>
        public void ResetMultipliedValue()
        {
            MultipliedValue = string.Empty;
        }

        static public LinkedList<Cell> DeepCopyCells(IEnumerable<Cell> cells, Group noCopyGroup = null)
        {
            var copiedCells = new LinkedList<Cell>();
            var oldToNew = new Dictionary<Group, Group>();

            foreach (Cell ce in cells)
            {
                Cell copy = new Cell(null)
                {
                    Duration = ce.Duration,
                    Source = ce.Source,
                    IsBreak = ce.IsBreak,
                    Value = ce.Value
                };

                foreach (var kp in oldToNew)
                {
                    // add cell to each containing group
                    kp.Value.ExclusiveCells.AddLast(copy);

                    // add the group to the cell
                    if (kp.Key is RepeatGroup)
                    {
                        copy.RepeatGroups.AddLast((RepeatGroup)kp.Value);
                    }
                    else if (kp.Key is MultGroup)
                    {
                        copy.MultGroups.AddLast((MultGroup)kp.Value);
                    }
                }

                // copy the groups
                foreach ((bool isStart, Group oldGroup) in ce.GroupActions)
                {
                    if (oldGroup == noCopyGroup) continue;

                    if (isStart)
                    {
                        // start of group
                        Group newGroup = null;

                        if (oldGroup is RepeatGroup)
                        {
                            newGroup = new RepeatGroup()
                            {
                                Times = ((RepeatGroup)oldGroup).Times,
                                Duration = oldGroup.Duration,
                                LastTermModifier = ((RepeatGroup)oldGroup).LastTermModifier,
                                Position = oldGroup.Position,
                                MultFactor = ((RepeatGroup)oldGroup).MultFactor
                            };

                            copy.RepeatGroups.AddLast((RepeatGroup)newGroup);
                        }
                        else if (oldGroup is MultGroup)
                        {
                            newGroup = new MultGroup()
                            {
                                Duration = oldGroup.Duration,
                                Factor = (oldGroup as MultGroup).Factor,
                                FactorValue = ((MultGroup)oldGroup).FactorValue,
                                Position = oldGroup.Position
                            };

                            copy.MultGroups.AddLast((MultGroup)newGroup);
                        }
                        newGroup.ExclusiveCells.AddLast(copy);

                        copy.GroupActions.AddLast((true, newGroup));

                        oldToNew.Add(oldGroup, newGroup);
                    }
                    else
                    {
                        // end of group
                        copy.GroupActions.AddLast((false, oldToNew[oldGroup]));

                        if (oldGroup is RepeatGroup)
                        {
                            copy.RepeatGroups.AddLast((RepeatGroup)oldToNew[oldGroup]);
                        }
                        else if (oldGroup is MultGroup)
                        {
                            copy.MultGroups.AddLast((MultGroup)oldToNew[oldGroup]);
                        }

                        oldToNew.Remove(oldGroup);
                    }
                }

                copiedCells.AddLast(copy);
            }

            return copiedCells;
        }

        public class Selection
        {
            /// <summary>
            /// Cells currently contained by the selection
            /// </summary>
            public List<Cell> Cells = new List<Cell>();

            /// <summary>
            /// First cell in the selection. Set when grid lines are drawn
            /// </summary>
            public Cell FirstCell;

            /// <summary>
            /// Last cell in the selection. Set when grid lines are drawn
            /// </summary>
            public Cell LastCell;

            /// <summary>
            /// Remove all cells from the selection
            /// </summary>
            public void Clear()
            {
                Cells.Clear();
                FirstCell = null;
                LastCell = null;
            }

            /// <summary>
            /// Deselect all curently selected cells
            /// </summary>
            public void DeselectAll(bool updateUi = true)
            {
                foreach (Cell c in Cells.ToArray())
                {
                    c.ToggleSelect(false);
                }

                Clear();

                if (updateUi)
                {
                    EditorWindow.Instance.UpdateUiForSelectedCell();
                }
            }

            /// <summary>
            /// Select all cells from start to end inclusive
            /// </summary>
            /// <param name="start"></param>
            /// <param name="end"></param>
            /// <param name="row"></param>
            public void SelectRange(int start, int end, Row row, bool updateUi = true)
            {
                DeselectAll(false);

                if (row.Cells.Count > start && row.Cells.Count > end && start <= end)
                {
                    for (int i = start; i <= end; i++)
                    {
                        row.Cells[i].ToggleSelect(false);
                    }
                }

                if (updateUi)
                {
                    EditorWindow.Instance.UpdateUiForSelectedCell();
                }
            }
        }
    }

    public class CellList : List<Cell>
    {
        public int InsertSorted(Cell item, int startIndex = 0, int endIndex = -1)
        {
            int index;

            if (endIndex == -1) endIndex = Count - 1;

            if (item.Position < this[startIndex].Position)
            {
                index = startIndex;
            }
            else if (item.Position > this[endIndex].Position)
            {
                Add(item);
                return Count - 1;
            }
            else
            {
                // place in order within the collection
                index = InsertSorted(startIndex, endIndex, item.Position);
            }

            if (index > -1)
            {
                Insert(index, item);
            }

            return index;
        }

        protected int InsertSorted(int start, int end, double position)
        {
            int offset = (end - start) / 2;
            double comp = this[start + offset].Position;
            if (position < comp)
            {
                if (offset == 0)
                {
                    return end;
                }
                return InsertSorted(start, start + offset, position);
            }
            else if (position > comp)
            {
                if (offset == 0)
                {
                    return end;
                }
                return InsertSorted(start + offset, end, position);
            }

            return -1;
        }

        public Cell FindCellBelowPosition(double position)
        {
            int index = InsertSorted(0, Count - 1, position);
            if (index != -1)
            {
                return this[index - 1];
            }

            return null;
        }
    }
}
