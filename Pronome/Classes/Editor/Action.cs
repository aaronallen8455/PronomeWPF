using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Pronome.Editor
{
    /// <summary>
    /// Enum of all possible action types so we know which type to cast the action to
    /// </summary>
    public enum ActionType { AddCell, DeleteCell, ChangeCellPosition, ChangeCellDuration, ChangeCellSource };

    public interface IEditorAction
    {
        void Redo();
        void Undo();

        /// <summary>
        /// String describing the action
        /// </summary>
        string HeaderText { get; }
    }

    public abstract class AbstractAction
    {
        protected Row Row;

        /// <summary>
        /// Redraw the rows which reference the row being modified
        /// </summary>
        public void RedrawReferencers()
        {
            if (Row.ReferenceMap.ContainsKey(Row.Index))
            {
                if (!Row.BeatCodeIsCurrent)
                {
                    Row.UpdateBeatCode();
                }
                foreach (int rowIndex in Row.ReferenceMap[Row.Index])
                {
                    Row r = EditorWindow.Instance.Rows[rowIndex];

                    r.Redraw();
                }
            }
        }
    }

    public class AddCell : AddRemoveCell, IEditorAction
    {
        private string _headerText = "Add Cell";
        public string HeaderText { get => _headerText; }

        public AddCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null) : base(cell, previousCell, prevBeforeVal)
        {

        }

        public void Redo()
        {
            Add();
        }
        public void Undo()
        {
            Remove();
        }
    }

    public class RemoveCell : AddRemoveCell, IEditorAction
    {
        private string _headerText = "Remove Cell";
        public string HeaderText { get => _headerText; }

        public RemoveCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null) : base(cell, previousCell, prevBeforeVal)
        {

        }

        public void Redo()
        {
            Remove();
        }

        public void Undo()
        {
            Add();
        }
    }

    /// <summary>
    /// Holds the methods used by the AddCell and RemoveCell actions
    /// </summary>
    public abstract class AddRemoveCell : AbstractAction
    {
        protected Cell Cell;

        protected int Index;

        protected Cell PreviousCell;

        protected string PreviousCellBeforeValue;
        protected string PreviousCellAfterValue;

        /// <summary>
        /// The add cell action. Should be initialized after the new cell has been added into the row.
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="previousCell"></param>
        public AddRemoveCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null)
        {
            Cell = cell;
            Row = cell.Row;
            Index = Cell.Row.Cells.IndexOf(cell);

            PreviousCell = previousCell;

            if (previousCell != null)
            {
                PreviousCellAfterValue = previousCell.Value;
                PreviousCellBeforeValue = prevBeforeVal;
            }
        }

        public void Add()
        {
            // Add in the cell
            Row.Cells.Insert(Index, Cell);
            
            // render the rectangle
            if (Cell.RepeatGroups.Any())
            {
                Cell.RepeatGroups.Last.Value.Canvas.Children.Add(Cell.Rectangle);
            }
            else
            {
                Row.Canvas.Children.Add(Cell.Rectangle);
            }

            // modify the previous cell or the row's offset
            Cell below = null;

            if (Index == 0)
            {
                Row.Duration += Cell.Duration;
                Row.Offset -= Cell.Duration;
                Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, Cell.Value);
            }
            else
            {
                below = Row.Cells[Index - 1];

                below.Value = PreviousCellAfterValue;
                below.SetDurationDirectly(BeatCell.Parse(below.Value));

                if (Index == Row.Cells.Count - 1)
                {
                    // it's the last cell, resize sizer
                    double oldDur = Row.Duration;
                    Row.Duration = Cell.Position + Cell.Duration;
                    Row.ChangeSizerWidthByAmount(Row.Duration - oldDur);
                }
            }
            //TODO: check if need to adjust the LCM of the last repeat group if this is the last cell in row

            // add to groups
            foreach (RepeatGroup rg in Cell.RepeatGroups)
            {
                if (below != null && below.RepeatGroups.Contains(rg))
                {
                    rg.Cells.AddAfter(rg.Cells.Find(below), Cell);
                }
                else
                {
                    rg.Cells.AddFirst(Cell);
                }
            }
            foreach (MultGroup mg in Cell.MultGroups)
            {
                if (below != null && below.MultGroups.Contains(mg))
                {
                    mg.Cells.AddAfter(mg.Cells.Find(below), Cell);
                }
                else
                {
                    mg.Cells.AddFirst(Cell);
                }
            }

            Row.BeatCodeIsCurrent = false;

            RedrawReferencers();
        }

        public void Remove()
        {
            // deselect cell if selected
            if (Cell.IsSelected)
            {
                Cell.ToggleSelect();

                EditorWindow.Instance.UpdateUiForSelectedCell();
            }
            // remove the rect
            if (Cell.RepeatGroups.Any())
            {
                Cell.RepeatGroups.Last.Value.Canvas.Children.Remove(Cell.Rectangle);
            }
            else
            {
                Row.Canvas.Children.Remove(Cell.Rectangle);
            }
            // modify previous cell or row's offset
            if (Index == 0)
            {
                Row.Duration -= Cell.Duration;
                // need to set the row's offset
                Row.Offset += Cell.Duration;
                Row.OffsetValue = BeatCell.SimplifyValue(Row.OffsetValue + "+0" + Cell.Value);
                // place above cell at front of sizer and shrink sizer width
                Cell above = Row.Cells[1];
                above.Position = 0;
                //row.ChangeSizerWidthByAmount(-Cell.Duration);
            }
            else
            {
                Cell below = Row.Cells[Index - 1];

                below.Value = PreviousCellBeforeValue;
                // if cell is the last cell, resize the below cell. Otherwise set duration directly
                if (Row.Cells.Last() == Cell)
                {
                    // preserve cell's position
                    double oldPos = Cell.Position;
                    double oldOffset = Canvas.GetLeft(Cell.Rectangle);
                    below.Duration = BeatCell.Parse(below.Value);
                    // reset position
                    Cell.Position = oldPos;
                    Canvas.SetLeft(Cell.Rectangle, oldOffset);
                    // resize row
                    Row.Duration = below.Position + below.Duration;
                    Row.ChangeSizerWidthByAmount(-Cell.Duration);
                }
                else
                {
                    below.SetDurationDirectly(BeatCell.Parse(below.Value));
                }
            }
            // remove from groups
            foreach (RepeatGroup rg in Cell.RepeatGroups)
            {
                rg.Cells.Remove(Cell);
                rg.Canvas.Children.Remove(Cell.Rectangle);
                //// remove the group if no cells left
                //if (!rg.Cells.Any())
                //{
                //    rg.RemoveGroupFromRow();
                //}
            }
            foreach (MultGroup mg in Cell.MultGroups)
            {
                mg.Cells.Remove(Cell);
                //if (!mg.Cells.Any())
                //{
                //    mg.RemoveGroupFromRow();
                //}
            }

            Row.Cells.Remove(Cell);

            Row.BeatCodeIsCurrent = false;

            RedrawReferencers();
        }
    }

    public class RemoveCells : AbstractAction, IEditorAction
    {
        public string HeaderText { get => "Remove Cell(s)"; }

        protected Cell[] Cells;

        //protected string PreviousCellValue;

        protected HashSet<RepeatGroup> RepGroups = new HashSet<RepeatGroup>();

        protected HashSet<MultGroup> MultGroups = new HashSet<MultGroup>();

        protected string BeatCodeBefore;

        protected string BeatCodeAfter;

        /// <summary>
        /// Total duration in BPM of the group
        /// </summary>
        protected double Duration = 0;

        protected string BeatCodeDuration;

        protected bool ChangeOffset = false;

        /// <summary>
        /// Index of the first cell in the selection
        /// </summary>
        protected int Index;

        public RemoveCells(Cell[] cells)//, string previousCellValue)
        {
            Cells = cells;
            Row = cells[0].Row;
            //PreviousCellValue = previousCellValue;
            Index = cells[0].Row.Cells.IndexOf(cells[0]);

            BeatCodeBefore = cells[0].Row.Stringify();

            StringBuilder duration = new StringBuilder();
            // find all groups that are encompassed by the selection
            HashSet<Group> touchedGroups = new HashSet<Group>();
            RepeatGroup groupBeingAppendedTo = null; // a group who's LTM is actively being augemented
            Queue<RepeatGroup> rgToAppendTo = new Queue<RepeatGroup>(); // RGs that may need to have their LTM added to
            foreach (Cell c in Cells)
            {
                // add to the LTM of groups with a previous cell in the selection but not this cell
                if (rgToAppendTo.Peek() != null && !c.RepeatGroups.Contains(rgToAppendTo.Peek()))
                {
                    groupBeingAppendedTo = rgToAppendTo.Dequeue();
                }
                if (groupBeingAppendedTo != null)
                {
                    groupBeingAppendedTo.LastTermModifier = BeatCell.Add(groupBeingAppendedTo.LastTermModifier, c.Value);
                }

                int times = 1; // times this cell gets repeated
                // track the times that each RG's LTM gets repeated
                Dictionary<RepeatGroup, int> lcmTimes = new Dictionary<RepeatGroup, int>();

                foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                {
                    // remove cell from group
                    rg.Cells.Remove(c);
                    if (touchedGroups.Contains(rg)) continue;

                    rgToAppendTo.Enqueue(rg);
                    touchedGroups.Add(rg);

                    if (
                        (Cells[0] == rg.Cells.First.Value || rg.Position >= Cells[0].Position) 
                        && (Cells[Cells.Length - 1] == rg.Cells.Last.Value || rg.Position + rg.Duration <= Cells[Cells.Length - 1].Position))
                    {
                        RepGroups.Add(rg);

                        times *= rg.Times;
                        // multiply all nested rgs' LTMs by this groups repeat times.
                        foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                        {
                            lcmTimes[kv.Key] *= rg.Times;
                        }
                        lcmTimes.Add(rg, 1);
                        touchedGroups.Add(rg);
                    }
                    // if a rep group ends on this cell, add it's dupes to the duration
                    //if (rg.Cells.Last.Value == c)
                    //{
                    //    Duration += rg.Duration * (rg.Times - 1);
                    //}
                }
                foreach (MultGroup mg in c.MultGroups)
                {
                    // remove cell from group
                    mg.Cells.Remove(c);
                    if (touchedGroups.Contains(mg)) continue;
                    touchedGroups.Add(mg);
                    if (
                        (Cells[0] == mg.Cells.First.Value || mg.Position >= Cells[0].Position)
                        && (Cells[Cells.Length - 1] == mg.Cells.Last.Value || mg.Position + mg.Duration <= Cells[Cells.Length - 1].Position + Cells[Cells.Length - 1].Duration))
                    {
                        MultGroups.Add(mg);
                    }
                }

                // get the double version of duration
                Duration += c.Duration * times;

                // get the string version of duration
                // add cell's repeat durations.
                duration.Append("+0").Append(BeatCell.MultiplyTerms(c.Value, times));
                // add any LTM's from repeat groups
                foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                {
                    duration.Append("+0").Append(BeatCell.MultiplyTerms(kv.Key.LastTermModifier, kv.Value));
                    Duration += BeatCell.Parse(kv.Key.LastTermModifier) * kv.Value;
                }
                

                //foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                //{
                //    // only concerned with the rep groups that are encompassed by the selection
                //    if (!RepGroups.Contains(rg)) continue;
                //
                //    times *= rg.Times;
                //
                //    if (!touchedGroups.Contains(rg))
                //    {
                //        foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                //        {
                //            lcmTimes[kv.Key] *= rg.Times;
                //        }
                //        lcmTimes.Add(rg, 1);
                //        touchedGroups.Add(rg);
                //    }
                //}

            }

            BeatCodeDuration = BeatCell.SimplifyValue(duration.ToString());
        }

        public void Undo()
        {
            // unselect
            Cell.SelectedCells.DeselectAll();

            EditorWindow.Instance.UpdateUiForSelectedCell();

            Row.Reset();

            Row.FillFromBeatCode(BeatCodeBefore);
            Row.BeatCode = BeatCodeBefore;
            Row.BeatCodeIsCurrent = true;

            if (ChangeOffset)
            {
                Row.Offset -= Duration;
                Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, BeatCodeDuration);
            }

            RedrawReferencers();
        }

        public void Redo()
        {
            // check if afterBeatCode has already been generated, if not, make it
            if (string.IsNullOrEmpty(BeatCodeAfter))
            {
                Cell firstCell = Row.Cells[Index];
                // remove cells
                Row.Cells.RemoveRange(Index, Cells.Length);
                // remove groups
                foreach (RepeatGroup rg in RepGroups)
                {
                    Row.RepeatGroups.Remove(rg);
                }
                foreach (MultGroup mg in MultGroups)
                {
                    Row.MultGroups.Remove(mg);
                }

                // check if first cell of selection is not row's first cell
                if (Index == 0)
                {
                    // will be increasing the row offset, but only if
                    // selection is not part of a rep group that is not
                    // encompassed by the selection
                    if (RepGroups.Contains(firstCell.RepeatGroups.First.Value))
                    {
                        // augment the row's offset
                        ChangeOffset = true;
                    }
                }
                else
                {
                    Cell prevCell = Row.Cells[Index - 1];
                    // if previous cell is the last cell of a rep group, increase rep groups offset
                    
                    // TODO: In case of a selection starting inside a rep group and ending outside it, the LTM needs to increase

                    RepeatGroup groupToAddTo = null;
                    foreach (RepeatGroup rg in prevCell.RepeatGroups.Reverse())
                    {
                        if (!rg.Cells.Contains(firstCell))
                        {
                            groupToAddTo = rg;
                        }
                        else break;
                    }

                    if (groupToAddTo != null)
                    {
                        groupToAddTo.LastTermModifier = BeatCell.Add(groupToAddTo.LastTermModifier, BeatCodeDuration);
                    }
                    else if (!firstCell.RepeatGroups.Any() || firstCell.RepeatGroups.Last.Value.Cells.Contains(prevCell))
                    {
                        // otherwise, increase the prev cell's duration
                        // but only if it is not the cell prior to a repgroup for which first cell of select is first cell of the rep group.
                        prevCell.Value = BeatCell.Add(prevCell.Value, BeatCodeDuration);
                    }
                    
                }
                // the string to parse whenever redo action occurs
                BeatCodeAfter = Row.Stringify();

                // no longer need these
                RepGroups.Clear();
                MultGroups.Clear();
                Cells = null;
            }

            // unselect
            Cell.SelectedCells.DeselectAll();

            //EditorWindow.Instance.UpdateUiForSelectedCell();

            Row.Reset();

            Row.FillFromBeatCode(BeatCodeAfter);
            Row.BeatCode = BeatCodeAfter;
            Row.BeatCodeIsCurrent = true;

            if (ChangeOffset)
            {
                Row.Offset += Duration;
                Row.OffsetValue = BeatCell.Add(Row.OffsetValue, BeatCodeDuration);
            }

            RedrawReferencers();

            // TODO: removing cells from the middle of a row shouldn't shrink the row.

            //// If a cell is deleted from the front of a rep group, then the duration of that group should change
            //// otherwise, the duration should stay the same.
            //
            //
            //// is the selection at the front, middle, or back of the row?
            //if (Index == 0)
            //{
            //    // can't delete all cells in row
            //    if (Cells.Length == Row.Cells.Count)
            //    {
            //        return;
            //    }
            //    //StringBuilder offsetVal = new StringBuilder(Row.OffsetValue);
            //    StringBuilder removedDuration = new StringBuilder();
            //    HashSet<RepeatGroup> touchedRepGroups = new HashSet<RepeatGroup>(); // use to accumulate the LCMs of nested rep groups
            //    // remove from front
            //    Row.Cells.RemoveRange(Index, Cells.Length);
            //    foreach (Cell c in Cells)
            //    {
            //        Row.Offset += c.Duration;
            //
            //        Row.Cells.Remove(c);
            //        // remove rectangle
            //        if (!c.RepeatGroups.Any())
            //        {
            //            removedDuration.Append("+0").Append(c.Value);
            //            Row.Canvas.Children.Remove(c.Rectangle);
            //        }
            //        else
            //        {
            //            // only need to worry about removing cell rects from rep groups that won't be totally removed
            //            if (!RepGroups.Contains(c.RepeatGroups.Last.Value))
            //            {
            //                c.RepeatGroups.Last.Value.Canvas.Children.Remove(c.Rectangle);
            //            }
            //            //int times = c.RepeatGroups.Select(x => x.Times).Aggregate((x, y) => x * y);
            //            //offsetVal.Append("+0").Append(BeatCell.MultiplyTerms(c.Value, times));
            //
            //            int times = 1;
            //            Dictionary<RepeatGroup, int> lcmTimes = new Dictionary<RepeatGroup, int>();
            //            foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
            //            {
            //                // only concerned with the rep groups that are encompassed by the selection
            //                if (!RepGroups.Contains(rg)) continue;
            //
            //                times *= rg.Times;
            //
            //                if (!touchedRepGroups.Contains(rg))
            //                {
            //                    foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
            //                    {
            //                        lcmTimes[kv.Key] *= rg.Times;
            //                    }
            //                    lcmTimes.Add(rg, 1);
            //                    touchedRepGroups.Add(rg);
            //                }
            //            }
            //
            //            // add cell's repeat durations.
            //            removedDuration.Append("+0").Append(BeatCell.MultiplyTerms(c.Value, times));
            //            // add any LTM's from repeat groups
            //            foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
            //            {
            //                removedDuration.Append("+0").Append(BeatCell.MultiplyTerms(kv.Key.LastTermModifier, kv.Value));
            //            }
            //        }
            //    }
            //    // remove groups that are in selection
            //    foreach (RepeatGroup rg in RepGroups)
            //    {
            //        // need to accumulate the LTM's
            //        Row.Canvas.Children.Remove(rg.Rectangle);
            //        rg.HostCanvas.Children.Remove(rg.Canvas);
            //        foreach (Rectangle rect in rg.HostRects)
            //        {
            //            rg.HostCanvas.Children.Remove(rect);
            //        }
            //
            //        Row.RepeatGroups.Remove(rg);
            //    }
            //    foreach (MultGroup mg in MultGroups)
            //    {
            //        Row.Canvas.Children.Remove(mg.Rectangle);
            //        Row.MultGroups.Remove(mg);
            //    }
            //    // reposition other cells
            //    bool isFirst = true;
            //    double offset = 0;
            //    foreach (Cell c in Row.Cells)
            //    {
            //        if (isFirst)
            //        {
            //            offset = c.Position;
            //            isFirst = false;
            //        }
            //
            //        c.Position -= offset;
            //    }
            //
            //    Row.Duration -= offset;
            //    Row.ChangeSizerWidthByAmount(-offset);
            //}
            //else if (Cells.Last() != Row.Cells.Last())
            //{
            //    // remove from middle
            //}
            //else
            //{
            //    // remove from end
            //}
        }
    }

    /// <summary>
    /// Adds extra functionality to a stack to implement Undo/Redo
    /// </summary>
    public class ActionStack : Stack<IEditorAction>
    {
        private MenuItem MenuItem;

        private string Prefix;

        public ActionStack(MenuItem menuItem, int size) : base(size)
        {
            MenuItem = menuItem;
            Prefix = menuItem.Header.ToString();
        }

        new public void Push(IEditorAction action)
        {
            // append the header text
            MenuItem.Header = Prefix + " " + action.HeaderText;

            base.Push(action);
        }

        new public IEditorAction Pop()
        {
            // return to default if empty
            IEditorAction action = base.Pop();
            if (Count == 0)
            {
                MenuItem.Header = Prefix;
            }
            return action;
        }

        new public void Clear()
        {
            base.Clear();
            MenuItem.Header = Prefix;
        }
    }
}
