using System;
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

                //// reposition all subsequent cells and groups
                //HashSet<RepeatGroup> touchedRepGroups = new HashSet<RepeatGroup>();
                //HashSet<MultGroup> touchedMultGroups = new HashSet<MultGroup>();
                //bool isFirst = true;
                //double diff = value - _duration;
                //foreach (Cell cell in Row.Cells.SkipWhile(x => x != this).Skip(1))
                //{
                //    cell.Position += diff;
                //    // reposition groups
                //    foreach (RepeatGroup rg in cell.RepeatGroups)
                //    {
                //        if (!touchedRepGroups.Contains(rg))
                //        {
                //            touchedRepGroups.Add(rg);
                //            if (isFirst && cell == this) // if positioning from within group, increase duration
                //            {
                //                rg.Duration += diff;
                //            }
                //            else
                //            {
                //                rg.Position += diff;
                //            }
                //        }
                //    }
                //    foreach (MultGroup mg in cell.MultGroups)
                //    {
                //        if (!touchedMultGroups.Contains(mg))
                //        {
                //            touchedMultGroups.Add(mg);
                //            if (isFirst && cell == this)
                //            {
                //                mg.Duration += diff;
                //            }
                //            else
                //            {
                //                mg.Position += diff;
                //            }
                //        }
                //    }
                //    // reposition reference rect
                //    if (cell.ReferenceRectangle != null)
                //    {
                //        double cur = Canvas.GetLeft(cell.ReferenceRectangle);
                //        Canvas.SetLeft(cell.ReferenceRectangle, cur + diff);
                //    }
                //
                //    isFirst = false;
                //}
                //
                //// resize sizer
                //Row.ChangeSizerWidthByAmount(diff);
                //
                //_duration = value;
            }
        }

        /// <summary>
        /// The string representation of the duration. ie 1+2/3
        /// </summary>
        public string Value;

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
        /// The index of the layer that this cell is a reference for. Null if it's a regular cell.
        /// </summary>
        public string Reference;
        //public CellRepeat Repeat;
        public string Source;
        public LinkedList<MultGroup> MultGroups = new LinkedList<MultGroup>();
        public LinkedList<RepeatGroup> RepeatGroups = new LinkedList<RepeatGroup>();
        //public MultGroup MultGroup;
        //public RepeatGroup RepeatGroup;
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
            Rectangle = EditorWindow.Instance.Resources["cellRectangle"] as Rectangle;
            Rectangle.Height = (double)EditorWindow.Instance.Resources["cellHeight"];
            // set Canvas.Top
            Canvas.SetTop(Rectangle,
                (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - (double)EditorWindow.Instance.Resources["cellHeight"] / 2);
            Panel.SetZIndex(Rectangle, 10);
            Rectangle.MouseDown += Rectangle_MouseDown; ;
        }

        /// <summary>
        /// Select the cell
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsReference) // ref cells should not be manipulable
            {
                ToggleSelect();

                EditorWindow.Instance.UpdateUiForSelectedCell();
            }

            e.Handled = true;
        }

        public void ToggleSelect(bool Clicked = true)
        {
            IsSelected = !IsSelected;
            Rectangle.Stroke = IsSelected ? System.Windows.Media.Brushes.DeepPink : System.Windows.Media.Brushes.Black;
            // if not a multi select, deselect currently selected cell(s)
            if (IsSelected)
            {
                if (Clicked)
                {
                    // multiSelect
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        foreach (Cell c in Row.Cells
                            .SkipWhile(x => !x.IsSelected)
                            .SkipWhile(x => x.IsSelected)
                            .TakeWhile(x => !x.IsSelected))
                        {
                            c.ToggleSelect(false);
                        }
                    }
                    else
                    { // single select : deselect others
                        while (SelectedCells.Cells.Any())
                        {
                            SelectedCells.Cells.First().ToggleSelect();
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

        public void AddValue(string val)
        {
            Value += $"+{val}";
            Duration = BeatCell.Parse(Value);
        }

        public void SubtractValue(string val)
        {
            Value += $"-{val}";
            Duration = BeatCell.Parse(Value);
        }

        /// <summary>
        /// Assign a new duration with altering the UI
        /// </summary>
        /// <param name="duration"></param>
        public void SetDurationDirectly(double duration)
        {
            _duration = duration;
        }

        public int CompareTo(object obj)
        {
            if (obj is Cell cell)
            {
                return Position > cell.Position ? 1 : -1;
            }
            return 0;
        }

        public class Selection
        {
            public List<Cell> Cells = new List<Cell>();
            public Cell FirstCell;
            public Cell LastCell;
            public double Duration;
            public double Position;

            public void Clear()
            {
                Cells.Clear();
                FirstCell = null;
                LastCell = null;
                Duration = 0;
                Position = 0;
            }
        }
    }

    public class CellList : List<Cell>
    {
        public int InsertSorted(Cell item, int startIndex = 0, int endIndex = -1)
        {
            int index = InsertSorted(startIndex, endIndex == -1 ? Count - 1 : endIndex, item.Position);
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
