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
        //protected Cell Cell;
        protected double ClickPosition;

        public int Index;

        /// <summary>
        /// True if this action does something
        /// </summary>
        public bool IsValid = false;

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
                    if (position < -increment * Row.GridProx)//Cell.SelectedCells.FirstCell.Position - ((int)(Cell.SelectedCells.FirstCell.Position / increment) * increment - increment * Row.GridProx))
                    {
                        AddCellBelowRow(position, increment);
                    }
                    else if (position >= Cell.SelectedCells.FirstCell.Position - ((int)(Cell.SelectedCells.FirstCell.Position / increment) * increment) - increment * Row.GridProx)
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

                // if add above a reference, just drop it in and exit.
                if (below.IsReference)
                {
                    Row.Cells.Add(cell);

                    return;
                }

                // find the value string
                StringBuilder val = new StringBuilder();
                val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));

                HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                foreach (Cell c in Row.Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell).Where(x => string.IsNullOrEmpty(x.Reference)))
                {
                    val.Append("+0").Append(BeatCell.Invert(c.Value));
                    // account for rep groups and their LTMs
                    Dictionary<RepeatGroup, int> ltmTimes = new Dictionary<RepeatGroup, int>();
                    foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                    {
                        if (repGroups.Contains(rg)) continue;

                        foreach (Cell ce in rg.Cells.Where(x => string.IsNullOrEmpty(x.Reference)))
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
                        if (!string.IsNullOrEmpty(kv.Key.LastTermModifier))
                        {
                            val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(kv.Key.LastTermModifier), kv.Value));
                        }
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
                IsValid = true;
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
                    RightIndexBoundOfTransform = index - 1;
                    IsValid = true;

                    Cell below = Row.Cells[index - 1];
                    
                    // is new cell placed in the LTM zone of a rep group?
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * Row.GridProx > below.Position + below.Duration))
                    {
                        repWithLtmToMod = rg;
                    }
                    
                    // determine new value for the below cell
                    StringBuilder val = new StringBuilder();
                    // take and the distance from the end of the selection
                    val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));
                    // subtract the values up to the previous cell
                    HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                    foreach (Cell c in Row.Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell).TakeWhile(x => x != below).Where(x => string.IsNullOrEmpty(x.Reference)))
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
                    
                            foreach (Cell ce in rg.Cells.Where(x => string.IsNullOrEmpty(x.Reference)))
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
                foreach (Cell c in Row.Cells.TakeWhile(x => x != Cell.SelectedCells.FirstCell).Where(x => string.IsNullOrEmpty(x.Reference)))
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
                        foreach (Cell ce in rg.Cells.Where(x => string.IsNullOrEmpty(x.Reference)))
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
                RightIndexBoundOfTransform = -1;
                IsValid = true;
                cell.Position = 0;
                Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, cell.Value);
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
                    RightIndexBoundOfTransform = index - 1;
                    IsValid = true;

                    Cell below = Row.Cells[index - 1];
                    
                    // see if the cell is being added to a rep group's LTM zone
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * Row.GridProx > below.Position + below.Duration))
                    {
                        repWithLtmToMod = rg;
                    }
                    
                    // get new value string for below
                    StringBuilder val = new StringBuilder();
                    
                    HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                    foreach (Cell c in Row.Cells.SkipWhile(x => x != below).TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                    {
                        if (c == cell || !string.IsNullOrEmpty(c.Reference)) continue; // don't include the new cell
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
                            foreach (Cell ce in rg.Cells.Where(x => string.IsNullOrEmpty(x.Reference)))
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
                }
            }
        }
    }
}
