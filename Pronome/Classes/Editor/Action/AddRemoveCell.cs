using System;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Text;
using System.Collections.Generic;

namespace Pronome.Editor
{
    public class AddCell : AbstractBeatCodeAction
    {
        // TODO: Row stringify needs to account for the offset. Some add cell actions will change offset
        //protected Cell Cell;
        protected double ClickPosition;

        public int Index;

        public AddCell(double clickPosition, Row row) : base(row, "Add Cell")
        {
            ClickPosition = clickPosition;
            //Index = cell.Row.Cells.IndexOf(Cell);
        }

        protected override void Transformation()
        {
            // click position in BPM
            double position = ClickPosition / EditorWindow.Scale / EditorWindow.BaseFactor;
            position -= Row.Offset; // will be negative if inserting before the start

            // is it before or after the current selection?
            if (Cell.SelectedCells.Cells.Any())
            {
                // find the grid line within 10% of increment value of the click
                double increment = BeatCell.Parse(EditorWindow.CurrentIncrement);

                if (position > Cell.SelectedCells.LastCell.Position)
                {
                    // check if position is within the ghosted area of a repeat
                    bool outsideRepeat = true;
                    foreach (RepeatGroup rg in Row.RepeatGroups)
                    {
                        if (position > rg.Position + rg.Duration - increment * Row.GridProx && position < rg.Position + rg.Duration * rg.Times)
                        {
                            outsideRepeat = false;
                            break;
                        }
                    }
                    if (outsideRepeat)
                    {
                        // if the new cell will be above the current row
                        if (position > Row.Cells.Last().Position + Row.Cells.Last().Duration - increment * Row.GridProx) // should a cell placed within duration of prev cell maintain overall duration?
                        {
                            AddCellAboveRow(position, increment);
                        }
                        else
                        {
                            // cell will be above selection but within the row
                            AddCellToRowAboveSelection(position, increment);
                        }
                    }
                }
                else if (position < Cell.SelectedCells.FirstCell.Position)
                {
                    // New cell is below selection
                    // is new cell in the offset area, or is inside the row?
                    // check by seeing if the position is less than the least posible grid line position within the row from the selected cell.
                    if (position < Cell.SelectedCells.FirstCell.Position - ((int)(Cell.SelectedCells.FirstCell.Position / increment) * increment - increment * Row.GridProx))
                    {
                        AddCellBelowRow(position, increment);
                    }
                    else
                    {
                        // insert withinin row, below selection
                        // check if it's within a repeat's ghosted zone
                        bool outsideRepeat = true;
                        foreach (RepeatGroup rg in Row.RepeatGroups)
                        {
                            if (position > rg.Position + rg.Duration - increment * Row.GridProx && position < rg.Position + rg.Duration * rg.Times)
                            {
                                outsideRepeat = false;
                                break;
                            }
                        }
                        if (outsideRepeat)
                        {
                            AddCellToRowBelowSelection(position, increment);
                        }
                    }
                }
            }
        }

        /**
         * Test Cases:
         * 
         * 1) Above row
         * 2) Above row where last cell is in a repeat
         * 3) Above row and within the duration of the last cell
         * 4) Above selection and within row
         * 5) ^ Where selection is in a repeat and new cell is not
         * 6) ^ Where selection and new cell are in the same repeat
         * 7) ^ Where new cell is in a repeat group
         * 8) ^ Selection is in a repeat group that is nested
         * 9) ^ new cell is in a repeat group that is nested
         * 10) Below selection and within row
         * 11) ^ Where selection is in a repeat and new cell is not
         * 12) ^ Where selection and new cell are in the same repeat
         * 13) ^ Where new cell is in a repeat group
         * 14) ^ Selection is in a repeat group that is nested
         * 15) ^ new cell is in a repeat group that is nested
         * 16) Below the row, in offset area
         * 17) ^ selection is in a repeat group
         * 18) ^ there is a repeat group between the selection and the start
         */

        protected void AddCellAboveRow(double position, double increment)
        {
            double diff = position - Cell.SelectedCells.LastCell.Position;
            int div = (int)(diff / increment);
            double lower = increment * div + Row.GridProx * increment;
            double upper = lower + increment - Row.GridProx * 2 * increment;
            Cell cell = null;
            // use upper or lower grid line?
            if (diff <= lower && diff > 0)
            {
                // use left grid line
                // make new cell
                cell = new Cell(Row);
            }
            else if (diff >= upper)
            {
                // use right grid line
                div++;
                // make new cell
                cell = new Cell(Row);
            }

            if (cell != null)
            {
                cell.Value = BeatCell.SimplifyValue(EditorWindow.CurrentIncrement);
                cell.Position = Cell.SelectedCells.LastCell.Position + increment * div;
                // set new duration of previous cell
                Cell below = Row.Cells.Last();
                // add to groups and put rectangle in correct canvas
                //if (Group.AddToGroups(cell, below))
                //{
                //    cell.RepeatGroups.Last.Value.Canvas.Children.Add(cell.Rectangle);
                //}
                //else
                //{
                //    Row.Canvas.Children.Add(cell.Rectangle);
                //}

                // find the value string
                StringBuilder val = new StringBuilder();
                val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));

                HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                foreach (Cell c in Row.Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell))
                {
                    val.Append("+0").Append(BeatCell.Invert(c.Value));
                    // account for rep groups and their LTMs
                    Dictionary<RepeatGroup, int> ltmTimes = new Dictionary<RepeatGroup, int>();
                    foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                    {
                        if (repGroups.Contains(rg)) continue;

                        foreach (Cell ce in rg.Cells)
                        {
                            val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(ce.Value), rg.Times - 1));
                        }
                        foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                        {
                            ltmTimes[kv.Key] = kv.Value * rg.Times;
                        }

                        repGroups.Add(rg);
                        ltmTimes.Add(rg, 1);
                    }
                    foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                    {
                        val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(kv.Key.LastTermModifier), kv.Value));
                    }
                }

                string oldPrevCellValue = below.Value;
                // if last cell is in a rep group, we need to increase the LTM for that group
                if (below.RepeatGroups.Any())
                {
                    oldPrevCellValue = below.RepeatGroups.First.Value.LastTermModifier;
                    // add to the bottom repeat group's LTM
                    below.RepeatGroups.First.Value.LastTermModifier = BeatCell.SimplifyValue(val.ToString());
                }
                else
                {
                    // add to last cell's duration
                    //below.Duration = increment * div - (Row.Cells.Last().Position - Cell.SelectedCells.LastCell.Position);
                    val.Append("+0").Append(below.Value);
                    below.Value = BeatCell.SimplifyValue(val.ToString());
                }

                Row.Cells.Add(cell);
                //cell.Duration = increment;
                // set new duration of this row
                //Row.Duration = cell.Position + cell.Duration;
                //SetBackground(Duration);

                // create the action
                //AddCell action = new AddCell(cell, below, oldPrevCellValue);

            }

        }

        protected void AddCellToRowAboveSelection(double position, double increment)
        {
            // find nearest grid line
            double lastCellPosition = Cell.SelectedCells.LastCell.Position;
            double diff = position - lastCellPosition;
            int div = (int)(diff / increment);
            double lower = increment * div;// + .1 * increment;
            double upper = lower + increment;// - .2 * increment;

            Cell cell = null;
            //Cell below = null;
            // is lower, or upper in range?
            if (lower + Row.GridProx * increment > diff)
            {
                //below = Cells.TakeWhile(x => x.Position < lastCellPosition + lower).Last();
                cell = new Cell(Row);
                cell.Position = lastCellPosition + lower;
            }
            else if (upper - Row.GridProx * increment < diff)
            {
                //below = Cells.TakeWhile(x => x.Position < lastCellPosition + upper).Last();
                cell = new Cell(Row);
                cell.Position = lastCellPosition + upper;
                div++;
            }

            if (cell != null)
            {
                int index = Row.Cells.InsertSorted(cell);
                if (index > -1)
                {
                    Cell below = Row.Cells[index - 1];
                    
                    // is new cell placed in the LTM zone of a rep group?
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * Row.GridProx > below.Position + below.Duration))
                    {
                        repWithLtmToMod = rg;
                    }
                    
                    double duration;
                    
                    if (repWithLtmToMod == null)
                    {
                        duration = below.Position + below.Duration - cell.Position;
                        // set duration of preceding cell.
                        below.SetDurationDirectly(below.Duration - duration);
                    }
                    else
                    {
                        // get duration as a slice of the LTM of preceding group
                        duration = repWithLtmToMod.Position + repWithLtmToMod.Duration 
                            * repWithLtmToMod.Times + BeatCell.Parse(repWithLtmToMod.LastTermModifier) 
                            - cell.Position;
                    }
                    
                    cell.SetDurationDirectly(duration);
                    
                    //// add to groups and add it's rectangle to appropriate canvas
                    //if (Group.AddToGroups(cell, below))
                    //{
                    //    cell.RepeatGroups.Last.Value.Canvas.Children.Add(cell.Rectangle);
                    //}
                    //else
                    //{
                    //    Canvas.Children.Add(cell.Rectangle);
                    //}
                    
                    // determine new value for the below cell
                    StringBuilder val = new StringBuilder();
                    // take and the distance from the end of the selection
                    val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));
                    // subtract the values up to the previous cell
                    HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                    foreach (Cell c in Row.Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell).TakeWhile(x => x != below))
                    {
                        // subtract each value from the total
                        val.Append("+0").Append(BeatCell.Invert(c.Value));
                        // account for rep group repititions.
                        Dictionary<RepeatGroup, int> ltmTimes = new Dictionary<RepeatGroup, int>();
                        foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                        {
                            if (repGroups.Contains(rg)) continue;
                            // don't include a rep group if the end point is included in it.
                            if (cell.RepeatGroups.Contains(rg))
                            {
                                repGroups.Add(rg);
                                continue;
                            }
                    
                            foreach (Cell ce in rg.Cells)
                            {
                                val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(ce.Value), rg.Times - 1));
                            }
                            // get times to count LTMs for each rg
                            foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                            {
                                ltmTimes[kv.Key] = kv.Value * rg.Times;
                            }
                    
                            ltmTimes.Add(rg, 1);
                            repGroups.Add(rg);
                        }
                        // subtract the LTMs
                        foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                        {
                            val.Append("+0").Append(
                                BeatCell.MultiplyTerms(
                                    BeatCell.Invert(kv.Key.LastTermModifier), kv.Value));
                        }
                    }
                    
                    // get new cells value by subtracting old value of below cell by new value.
                    string newVal = BeatCell.SimplifyValue(val.ToString());
                    cell.Value = BeatCell.Subtract(below.Value, newVal);
                    string oldValue = below.Value;
                    
                    if (repWithLtmToMod == null)
                    {
                        // changing a cell value
                        below.Value = newVal;
                    }
                    else
                    {
                        // changing a LTM value
                        repWithLtmToMod.LastTermModifier = BeatCell.Subtract(repWithLtmToMod.LastTermModifier, newVal);
                    }
                    
                    // create the action
                    //AddCell action = new AddCell(cell, below, oldValue);
                    
                    
                }
            }

        }

        protected void AddCellBelowRow(double position, double increment)
        {
            // in the offset area
            // how many increments back from first cell selected
            double diff = (Cell.SelectedCells.FirstCell.Position + Row.Offset) - (position + Row.Offset);
            int div = (int)(diff / increment);
            // is it closer to lower of upper grid line?
            Cell cell = null;
            if (diff % increment <= increment * Row.GridProx)
            {
                // upper
                cell = new Cell(Row);
            }
            else if (diff % increment >= increment * Row.GridProx)
            {
                // lower
                cell = new Cell(Row);
                div++;
            }
            if (cell != null)
            {
                // get the value string
                StringBuilder val = new StringBuilder();
                // value of grid lines, the 
                val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));
                
                HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                foreach (Cell c in Row.Cells.TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                {
                    val.Append("+0").Append(BeatCell.Invert(c.Value));
                    // deal with repeat groups
                    Dictionary<RepeatGroup, int> lcmTimes = new Dictionary<RepeatGroup, int>();
                    foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                    {
                        if (repGroups.Contains(rg)) continue;
                        // if the selected cell is in this rep group, we don't want to include repetitions
                        if (Cell.SelectedCells.FirstCell.RepeatGroups.Contains(rg))
                        {
                            repGroups.Add(rg);
                            continue;
                        }
                        foreach (Cell ce in rg.Cells)
                        {
                            val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(ce.Value), rg.Times - 1));
                        }
                
                        foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                        {
                            lcmTimes[kv.Key] = kv.Value * rg.Times;
                        }
                        repGroups.Add(rg);
                        lcmTimes.Add(rg, 1);
                    }
                    // subtract the LCMs
                    foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                    {
                        val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(kv.Key.LastTermModifier), kv.Value));
                    }
                }
                cell.Value = BeatCell.SimplifyValue(val.ToString());
                
                Row.Cells.Insert(0, cell);
                //Cells.AddFirst(cell);
                //cell.Duration = (Cell.SelectedCells.FirstCell.Position - div * increment) * -1;
                cell.Position = 0;
                
                // set new duration of this row
                //Duration += cell.Duration;
                
                Row.Offset -= cell.Duration; //Cell.SelectedCells.FirstCell.Position - div * increment;
                Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, cell.Value);
                Row.Canvas.Children.Add(cell.Rectangle);
                
                // add undo action
            }

        }

        protected void AddCellToRowBelowSelection(double position, double increment)
        {
            double diff = Cell.SelectedCells.FirstCell.Position - position;
            int div = (int)(diff / increment);
            Cell cell = null;
            // is it in range of the left or right grid line?
            if (diff % increment <= increment * Row.GridProx)
            {
                // right
                cell = new Cell(Row);
            }
            else if (diff % increment >= increment * (1 - Row.GridProx))
            {
                // left
                cell = new Cell(Row);
                div++;
            }

            if (cell != null)
            {
                cell.Position = Cell.SelectedCells.FirstCell.Position - div * increment;
                int index = Row.Cells.InsertSorted(cell);
                if (index > -1)
                {
                    Cell below = Row.Cells[index - 1];
                    
                    // find new duration of below cell
                    //double newDur = Cells.SkipWhile(x => x != below)
                    //    .TakeWhile(x => x != Cell.SelectedCells.FirstCell)
                    //    .Select(x => x.Position)
                    //    .Sum() - div * increment;
                    
                    // see if the cell is being added to a rep group's LTM zone
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * Row.GridProx > below.Position + below.Duration))
                    {
                        repWithLtmToMod = rg;
                    }
                    
                    double duration;
                    
                    if (repWithLtmToMod == null)
                    {
                        duration = below.Position + below.Duration - cell.Position;
                        below.SetDurationDirectly(below.Duration - duration);
                        //newDur = cell.Position - below.Position;
                    }
                    else
                    {
                        // find slice of the LTM to use as duration
                        duration = repWithLtmToMod.Position + repWithLtmToMod.Duration 
                            * repWithLtmToMod.Times + BeatCell.Parse(repWithLtmToMod.LastTermModifier) 
                            - cell.Position;
                    }
                    
                    //cell.SetDurationDirectly(below.Duration - newDur);
                    //below.SetDurationDirectly(newDur);
                    cell.SetDurationDirectly(duration);
                    
                    //// add to groups and add rectangle to correct canvas
                    //if (Group.AddToGroups(cell, below))
                    //{
                    //    cell.RepeatGroups.Last.Value.Canvas.Children.Add(cell.Rectangle);
                    //}
                    //else
                    //{
                    //    Canvas.Children.Add(cell.Rectangle);
                    //}
                    
                    // get new value string for below
                    StringBuilder val = new StringBuilder();
                    
                    HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                    foreach (Cell c in Row.Cells.SkipWhile(x => x != below).TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                    {
                        if (c == cell) continue; // don't include the new cell
                        // add the cells value
                        val.Append(c.Value).Append('+');
                        // we need to track how many times to multiply each rep group's LTM
                        Dictionary<RepeatGroup, int> ltmFactors = new Dictionary<RepeatGroup, int>();
                        // if there's a rep group, add the repeated sections
                        // what order are rg's in? reverse
                        foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                        {
                            if (repGroups.Contains(rg)) continue;
                            // don't count reps for groups that contain the selection
                            if (Cell.SelectedCells.FirstCell.RepeatGroups.Contains(rg))
                            {
                                repGroups.Add(rg);
                                continue;
                            }
                            foreach (Cell ce in rg.Cells)
                            {
                                val.Append('0').Append(
                                    BeatCell.MultiplyTerms(ce.Value, rg.Times - 1))
                                    .Append('+');
                            }
                            // increase multiplier of LTMs
                            foreach (KeyValuePair<RepeatGroup, int> kv in ltmFactors)
                            {
                                ltmFactors[kv.Key] = kv.Value * rg.Times;
                            }
                            ltmFactors.Add(rg, 1);
                            // don't add ghost reps more than once
                            repGroups.Add(rg);
                        }
                        // add in all the LTMs from rep groups
                        foreach (KeyValuePair<RepeatGroup, int> kv in ltmFactors)
                        {
                            val.Append('0')
                                .Append(BeatCell.MultiplyTerms(kv.Key.LastTermModifier, kv.Value))
                                .Append('+');
                        }
                    }
                    
                    val.Append('0');
                    val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(EditorWindow.CurrentIncrement), div));
                    cell.Value = BeatCell.Subtract(below.Value, val.ToString());
                    //cell.Value = BeatCell.SimplifyValue(below.Value + '-' + val.ToString());
                    string oldValue;
                    
                    if (repWithLtmToMod == null)
                    {
                        oldValue = below.Value;
                        below.Value = BeatCell.SimplifyValue(val.ToString());
                    }
                    else
                    {
                        oldValue = repWithLtmToMod.LastTermModifier;
                        repWithLtmToMod.LastTermModifier = BeatCell.Subtract(
                            repWithLtmToMod.LastTermModifier, 
                            BeatCell.SimplifyValue(val.ToString()));
                    }
                    
                    // add undo action
                    //return new AddCell(cell, below, oldValue);
                }
            }

        }
    }

    ///// <summary>
    ///// Holds the methods used by the AddCell and RemoveCell actions
    ///// </summary>
    //public abstract class AddRemoveCell : AbstractAction
    //{
    //    protected Cell Cell;
    //
    //    protected int Index;
    //
    //    protected Cell PreviousCell;
    //
    //    protected string PreviousCellBeforeValue; // or LTM of a rep group
    //    protected string PreviousCellAfterValue; // or LTM of a rep group
    //
    //    /// <summary>
    //    /// The add cell action. Should be initialized after the new cell has been added into the row.
    //    /// </summary>
    //    /// <param name="cell"></param>
    //    /// <param name="previousCell"></param>
    //    public AddRemoveCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null, string prevAfterValue = null)
    //    {
    //        Cell = cell;
    //        Row = cell.Row;
    //        Index = Cell.Row.Cells.IndexOf(cell);
    //
    //        PreviousCell = previousCell;
    //
    //        if (previousCell != null)
    //        {
    //            PreviousCellAfterValue = prevAfterValue;
    //            PreviousCellBeforeValue = prevBeforeVal;
    //        }
    //    }
    //
    //    public void Add()
    //    {
    //        // Add in the cell
    //        Row.Cells.Insert(Index, Cell);
    //        
    //        // render the rectangle
    //        if (Cell.RepeatGroups.Any())
    //        {
    //            Cell.RepeatGroups.Last.Value.Canvas.Children.Add(Cell.Rectangle);
    //        }
    //        else
    //        {
    //            Row.Canvas.Children.Add(Cell.Rectangle);
    //        }
    //
    //        // modify the previous cell or the row's offset
    //        Cell below = null;
    //
    //        if (Index == 0)
    //        {
    //            Row.Duration += Cell.Duration;
    //            Row.Offset -= Cell.Duration;
    //            Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, Cell.Value);
    //        }
    //        else
    //        {
    //            below = Row.Cells[Index - 1];
    //            // check if need to adjust the LCM of the last repeat group if this is the last cell in row
    //            RepeatGroup ltmToMod = null;
    //            foreach (RepeatGroup rg in below.RepeatGroups.Where(x => x.Cells.Last.Value == below))
    //            {
    //                ltmToMod = rg;
    //            }
    //
    //            if (ltmToMod == null)
    //            {
    //                below.Value = PreviousCellAfterValue;
    //                below.SetDurationDirectly(BeatCell.Parse(below.Value));
    //            }
    //            else
    //            {
    //                ltmToMod.LastTermModifier = PreviousCellAfterValue;
    //            }
    //
    //            if (Index == Row.Cells.Count - 1 && !Cell.RepeatGroups.Any())
    //            {
    //                // it's the last cell, resize sizer
    //                // but not if it's in a group - size will stay the same.
    //                double oldDur = Row.Duration;
    //                Row.Duration = Cell.Position + Cell.Duration;
    //                Row.ChangeSizerWidthByAmount(Row.Duration - oldDur);
    //            }
    //        }
    //
    //        // add to groups
    //        foreach (RepeatGroup rg in Cell.RepeatGroups)
    //        {
    //            if (below != null && below.RepeatGroups.Contains(rg))
    //            {
    //                rg.Cells.AddAfter(rg.Cells.Find(below), Cell);
    //            }
    //            else
    //            {
    //                rg.Cells.AddFirst(Cell);
    //            }
    //
    //            // resize host rects if this is last cell in row
    //            if (rg.Cells.Last.Value == Cell)
    //            {
    //                foreach (Rectangle rect in rg.HostRects)
    //                {
    //                    rect.Width += below.Duration * EditorWindow.Scale * EditorWindow.BaseFactor;
    //                }
    //            }
    //        }
    //        foreach (MultGroup mg in Cell.MultGroups)
    //        {
    //            if (below != null && below.MultGroups.Contains(mg))
    //            {
    //                mg.Cells.AddAfter(mg.Cells.Find(below), Cell);
    //            }
    //            else
    //            {
    //                mg.Cells.AddFirst(Cell);
    //            }
    //        }
    //
    //        Row.BeatCodeIsCurrent = false;
    //
    //        RedrawReferencers();
    //
    //        EditorWindow.Instance.SetChangesApplied(false);
    //    }
    //
    //    public void Remove()
    //    {
    //        // deselect cell if selected
    //        if (Cell.IsSelected)
    //        {
    //            Cell.ToggleSelect();
    //
    //            EditorWindow.Instance.UpdateUiForSelectedCell();
    //        }
    //        // remove the rect
    //        if (Cell.RepeatGroups.Any())
    //        {
    //            Cell.RepeatGroups.Last.Value.Canvas.Children.Remove(Cell.Rectangle);
    //        }
    //        else
    //        {
    //            Row.Canvas.Children.Remove(Cell.Rectangle);
    //        }
    //        // modify previous cell or row's offset
    //        if (Index == 0)
    //        {
    //            Row.Duration -= Cell.Duration;
    //            // need to set the row's offset
    //            Row.Offset += Cell.Duration;
    //            Row.OffsetValue = BeatCell.SimplifyValue(Row.OffsetValue + "+0" + Cell.Value);
    //            // place above cell at front of sizer and shrink sizer width
    //            Cell above = Row.Cells[1];
    //            above.Position = 0;
    //            //row.ChangeSizerWidthByAmount(-Cell.Duration);
    //        }
    //        else
    //        {
    //            Cell below = Row.Cells[Index - 1];
    //
    //            // check if a LTM is being changed instead of cell value
    //            RepeatGroup ltmToMod = null;
    //            foreach (RepeatGroup rg in below.RepeatGroups.Where(x => x.Cells.Last.Value == below))
    //            {
    //                ltmToMod = rg;
    //            }
    //
    //            if (ltmToMod == null)
    //            {
    //                below.Value = PreviousCellBeforeValue;
    //            }
    //            else
    //            {
    //                ltmToMod.LastTermModifier = PreviousCellBeforeValue;
    //            }
    //
    //            // if cell is the last cell, resize the below cell. Otherwise set duration directly
    //            if (Row.Cells.Last() == Cell && !Cell.RepeatGroups.Any())
    //            {
    //                // preserve cell's position
    //                double oldPos = Cell.Position;
    //                double oldOffset = Canvas.GetLeft(Cell.Rectangle);
    //                if (ltmToMod == null)
    //                {
    //                    below.Duration = BeatCell.Parse(below.Value);
    //                    // reset position
    //                    Cell.Position = oldPos;
    //                }
    //                //Canvas.SetLeft(Cell.Rectangle, oldOffset);
    //                // resize row
    //                Row.Duration = below.Position + below.Duration;
    //                Row.ChangeSizerWidthByAmount(-Cell.Duration);
    //            }
    //            else if (ltmToMod == null)
    //            {
    //                below.SetDurationDirectly(BeatCell.Parse(below.Value));
    //            }
    //        }
    //        // remove from groups
    //        foreach (RepeatGroup rg in Cell.RepeatGroups)
    //        {
    //            // if this was the last cell in group, need to resize the host rects
    //            if (rg.Cells.Last.Value == Cell)
    //            {
    //                foreach (Rectangle rect in rg.HostRects)
    //                {
    //                    rect.Width -= (rg.Cells.Last.Previous.Value.Duration - Cell.Duration) * EditorWindow.Scale * EditorWindow.BaseFactor;
    //                }
    //            }
    //
    //            rg.Cells.Remove(Cell);
    //            rg.Canvas.Children.Remove(Cell.Rectangle);
    //        }
    //        foreach (MultGroup mg in Cell.MultGroups)
    //        {
    //            mg.Cells.Remove(Cell);
    //        }
    //
    //        Row.Cells.Remove(Cell);
    //
    //        Row.BeatCodeIsCurrent = false;
    //
    //        RedrawReferencers();
    //
    //        EditorWindow.Instance.SetChangesApplied(false);
    //    }
    //}

    //public class AddCell : AddRemoveCell, IEditorAction
    //{
    //    // TODO: need to use a beatcode action so that group linking stays in place
    //    private string _headerText = "Add Cell";
    //    public string HeaderText { get => _headerText; }
    //
    //    public AddCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null) : base(cell, previousCell, prevBeforeVal)
    //    {
    //
    //    }
    //
    //    public void Redo()
    //    {
    //        Add();
    //    }
    //    public void Undo()
    //    {
    //        Remove();
    //    }
    //}
    //
    //public class RemoveCell : AddRemoveCell, IEditorAction
    //{
    //    private string _headerText = "Remove Cell";
    //    public string HeaderText { get => _headerText; }
    //
    //    public RemoveCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null) : base(cell, previousCell, prevBeforeVal)
    //    {
    //
    //    }
    //
    //    public void Redo()
    //    {
    //        Remove();
    //    }
    //
    //    public void Undo()
    //    {
    //        Add();
    //    }
    //}
}
