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

        /// <summary>
        /// Determines how close a mouse click needs to be to a grid line to count as that line. It's a factor of the increment size.
        /// </summary>
        public const float GridProx = .15f;

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
            if (Cell.SelectedCells.Cells.Any() && Row == Cell.SelectedCells.FirstCell.Row)
            {
                // find the grid line within 10% of increment value of the click
                double increment = BeatCell.Parse(EditorWindow.CurrentIncrement);

                if (position > Cell.SelectedCells.LastCell.Position)
                {
                    // check if position is within the ghosted area of a repeat
                    bool outsideRepeat = true;
                    foreach (RepeatGroup rg in Row.RepeatGroups)
                    {
                        if (position > rg.Position + rg.Duration - increment * GridProx && position < rg.Position + rg.FullDuration)
                        {
                            outsideRepeat = false;
                            break;
                        }
                    }
                    if (outsideRepeat)
                    {
                        // if the last selected cell is a reference, make the cell after the ref the selections last cell.
                        // this corrects the placement of cells being made above the selection.
                        if (!string.IsNullOrEmpty(Cell.SelectedCells.LastCell.Reference) 
                            && Row.Cells.Reverse<Cell>().SkipWhile(x => x.IsReference).First() != Cell.SelectedCells.LastCell)
                        {
                            Cell.SelectedCells.LastCell = Row.Cells
                                .SkipWhile(x => x != Cell.SelectedCells.LastCell)
                                .Skip(1)
                                .SkipWhile(x => x.IsReference)
                                .First();
                        }

                        // if the new cell will be above the current row. Above all cells and above all repeat group LTMs
                        if (position > Row.Cells.Last().Position + Row.Cells.Last().ActualDuration - increment * GridProx
                            && (!Row.RepeatGroups.Any() ||
                            position > Row.RepeatGroups.Last.Value.Position 
                            + Row.RepeatGroups.Last.Value.FullDuration
                            + BeatCell.Parse(Row.RepeatGroups.Last.Value.LastTermModifier) - increment * GridProx))
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
                    if (position < -increment * GridProx)//Cell.SelectedCells.FirstCell.Position - ((int)(Cell.SelectedCells.FirstCell.Position / increment) * increment - increment * GridProx))
                    {
                        AddCellBelowRow(position, increment);
                    }
                    else if (position >= Cell.SelectedCells.FirstCell.Position - ((int)(Cell.SelectedCells.FirstCell.Position / increment) * increment) - increment * GridProx)
                    {
                        // insert withinin row, below selection
                        // check if it's within a repeat's ghosted zone
                        bool outsideRepeat = true;
                        foreach (RepeatGroup rg in Row.RepeatGroups)
                        {
                            if (position > rg.Position + rg.Duration - increment * GridProx && position < rg.Position + rg.FullDuration)
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
         * In order to have mult group resizing:
         * 1) Adding to a mult group from flat within that group,
         *    - the interval will need to be multiplied by the group factor
         *    - use the cell values that have said group's factor applied
         * 
         * 2) adding from inside a mult group to outside that gorup
         * 
         * 3) adding from before a mult group to after that group
         * 
         * 4) adding from outside the group, into the group
         *    - like normal, use the actualValue inside of mult group. Multiply result by group factor to get
         *    base value.
         */

        /**
         * Add above row works like this:
         *    - if placed above all other cells, and above all rep group's LTMs,
         *    increase the previous last cell's or rep group LTM's duration
         *    * get the BPM value of the increment multiplied by how many ticks from last selected cell
         *    * to the new cell
         *    * Then subtract the accumulated value of all cells including rep groups from the total value.
         *    make new cell duration the increment value
         * 
         * Add above selection, within row works like this:
         *    - Get the value of increment times # of ticks between last selected cell and new cell position
         *    - Subtract the accumulated values of all cells including rep groups to get the new value
         *    of the preceding cell OR a rep group's LTM if we are placing the cell inside of the LTM
         *    - The cells value is then the preceding cell's previous value minus it's new value.
         * 
         * Add below row works like this:
         *    - Get the value of increment times # of ticks between first selected cell and new cell position
         *    - subtract the accumulated values of all cells and groups between selected cell and new cell
         *    to get the value of the new cell.
         *    - Subtract new cell value from row's offset to get the new offset
         * 
         * add below section, within row works like this:
         *    - Get the increment * # of ticks value between the first selected cell and new cell postion
         *    - subtract the accumulated values of all cells and groups between selected cell and new cell
         *    to get the value of the new cell.
         *    - subtract new cell value from preceding cell / group LTM's old value to get value
         * 
         */ 

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
            double lower = increment * div + GridProx * increment;
            double upper = lower + increment - GridProx * 2 * increment;
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
                //val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));

                // running tally of times to multiply an LTM
                int ltmFactor = Cell.SelectedCells.LastCell.RepeatGroups.Any() ?
                        Cell.SelectedCells.LastCell.RepeatGroups
                        .Reverse()
                        .Select(x => x.Times)
                        .Aggregate((x, y) => x * y) : 1;

                foreach (Cell c in Row.Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell).Where(x => string.IsNullOrEmpty(x.Reference)))
                {
                    AddCellValueToAccumulator(c, Cell.SelectedCells.LastCell, cell, val, ref ltmFactor);
                }

                val.Append('0').Append(BeatCell.MultiplyTerms(BeatCell.Invert(EditorWindow.CurrentIncrement), div));

                string valToAdd = BeatCell.Invert(BeatCell.SimplifyValue(val.ToString()));

                //string oldPrevCellValue = below.Value;
                // if last cell is in a rep group, we need to increase the LTM for that group
                if (below.RepeatGroups.Any())
                {
                    var rg = below.RepeatGroups.First.Value;

                    rg.LastTermModifier = BeatCell.SimplifyValue(rg.GetValueDividedByMultFactor(valToAdd));
                }
                else
                {
                    // add to last cell's duration
                    below.Value = BeatCell.Add(below.GetValueDividedByMultFactors(valToAdd), below.Value);
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
            // is lower, or upper in range?
            if (lower + GridProx * increment > diff)
            {
                cell = new Cell(Row);
                cell.Position = lastCellPosition + lower;
            }
            else if (upper - GridProx * increment < diff)
            {
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

                    Cell below = Row.Cells[index - 1];

                    Group.AddToGroups(cell, below);

                    // is new cell placed in the LTM zone of a rep group?
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * GridProx > below.Position + below.ActualDuration))
                    {
                        repWithLtmToMod = rg;
                    }

                    // determine new value for the below cell
                    StringBuilder val = new StringBuilder();
                    // take and the distance from the end of the selection
                    //val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));

                    // a running total of the LTM factor (start with innermost nested group)
                    var sequence = Cell.SelectedCells.LastCell.RepeatGroups
                        .Where(x => !cell.RepeatGroups.Contains(x) && x.Cells.First.Value != Cell.SelectedCells.LastCell)
                        .Reverse()
                        .Select(x => x.Times);
                    int ltmFactor = sequence.Any() ? sequence.Aggregate((x, y) => x * y) : 1;

                    foreach (Cell c in Row.Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell).TakeWhile(x => x != cell).Where(x => string.IsNullOrEmpty(x.Reference)))
                    {
                        AddCellValueToAccumulator(c, Cell.SelectedCells.LastCell, cell, val, ref ltmFactor);
                    }

                    val.Append("0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(EditorWindow.CurrentIncrement), div));

                    // get new cells value by subtracting old value of below cell by new value.
                    string newVal = BeatCell.SimplifyValue(val.ToString());
                    // placing a new cell on the beginning of a LTM is not illegal
                    if (repWithLtmToMod != null && newVal == string.Empty)
                    {
                        newVal = "0";
                    }

                    // assign the new cell's value
                    cell.Value = BeatCell.SimplifyValue(cell.GetValueDividedByMultFactors(newVal));
                    
                    // if placing cell on top of another cell, it's not valid.
                    if (cell.Value == string.Empty || newVal == string.Empty)
                    {
                        // remove the cell
                        Row.Cells.Remove(cell);
                        foreach (RepeatGroup rg in cell.RepeatGroups)
                        {
                            rg.Cells.Remove(cell);
                        }
                        foreach (MultGroup mg in cell.MultGroups)
                        {
                            mg.Cells.Remove(cell);
                        }
                        cell = null;
                        return;
                    }

                    IsValid = true;

                    if (repWithLtmToMod == null)
                    {
                        // changing a cell value
                        below.Value = below.GetValueDividedByMultFactors(
                            BeatCell.Subtract(below.Value, newVal));
                        below.Value = BeatCell.SimplifyValue(below.Value);

                        if (below.IsBreak)
                        {
                            below.IsBreak = false;
                            cell.IsBreak = true;
                        }
                    }
                    else
                    {
                        // changing a LTM value
                        repWithLtmToMod.LastTermModifier = BeatCell.SimplifyValue(
                            repWithLtmToMod.GetValueDividedByMultFactor(
                                BeatCell.Subtract(repWithLtmToMod.LastTermModifier, newVal)));
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
            if (diff % increment <= increment * GridProx)
            {
                // upper
                cell = new Cell(Row);
            }
            else if (diff % increment >= increment * GridProx)
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
                //val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));

                // a running tally of the factor for LTMs
                int ltmFactor = 1;

                HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                foreach (Cell c in Row.Cells.TakeWhile(x => x != Cell.SelectedCells.FirstCell).Where(x => string.IsNullOrEmpty(x.Reference)))
                {
                    AddCellValueToAccumulator(c, Row.Cells.First(), Cell.SelectedCells.FirstCell, val, ref ltmFactor);
                }

                val.Append('0').Append(BeatCell.MultiplyTerms(BeatCell.Invert(EditorWindow.CurrentIncrement), div));

                cell.Value = BeatCell.Invert(BeatCell.SimplifyValue(val.ToString()));
                
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
            if (diff % increment <= increment * GridProx)
            {
                // right
                cell = new Cell(Row);
            }
            else if (diff % increment >= increment * (1 - GridProx))
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

                    Group.AddToGroups(cell, below);
                    
                    // see if the cell is being added to a rep group's LTM zone
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * GridProx > below.Position + below.ActualDuration))
                    {
                        repWithLtmToMod = rg;
                    }
                    
                    // get new value string for below
                    StringBuilder val = new StringBuilder();

                    //int ltmFactor = below.RepeatGroups.Any() ?
                    //    below.RepeatGroups
                    //    .Where(x => !Cell.SelectedCells.FirstCell.RepeatGroups.Contains(x))
                    //    .Reverse()
                    //    .Select(x => x.Times)
                    //    .Aggregate((x, y) => x * y) : 1;

                    // a running total of the LTM factor (start with innermost nested group)
                    var sequence = cell.RepeatGroups
                        .Where(x => !Cell.SelectedCells.FirstCell.RepeatGroups.Contains(x) && x.Cells.First.Value != cell)
                        .Reverse()
                        .Select(x => x.Times);
                    int ltmFactor = sequence.Any() ? sequence.Aggregate((x, y) => x * y) : 1;

                    foreach (Cell c in Row.Cells.SkipWhile(x => x != below).Skip(1).TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                    {
                        AddCellValueToAccumulator(c, cell, Cell.SelectedCells.FirstCell, val, ref ltmFactor);
                    }
                    
                    val.Append('0').Append(BeatCell.MultiplyTerms(BeatCell.Invert(EditorWindow.CurrentIncrement), div));

                    cell.Value = BeatCell.SimplifyValue(cell.GetValueDividedByMultFactors(BeatCell.Invert(val.ToString())));

                    //cell.Value = BeatCell.Subtract(repWithLtmToMod == null ? below.GetValueWithMultFactors() : repWithLtmToMod.GetLtmWithMultFactor(), val.ToString());
                    //cell.Value = cell.GetValueDividedByMultFactors(cell.Value);
                    //cell.Value = BeatCell.SimplifyValue(cell.Value);

                    string newValue = BeatCell.SimplifyValue(val.ToString());

                    // check for cell being doubled
                    if (cell.Value == string.Empty)// || (newValue == string.Empty && repWithLtmToMod == null))
                    {
                        IsValid = false;
                        Row.Cells.Remove(cell);
                        foreach (RepeatGroup rg in cell.RepeatGroups)
                        {
                            rg.Cells.Remove(cell);
                        }
                        foreach (MultGroup mg in cell.MultGroups)
                        {
                            mg.Cells.Remove(cell);
                        }
                        cell = null;
                        return;
                    }
                    
                    if (repWithLtmToMod == null)
                    {
                        below.Value = below.GetValueDividedByMultFactors(BeatCell.Add(below.Value, newValue));
                        //below.Value = below.GetValueDividedByMultFactors(newValue);
                        below.Value = BeatCell.SimplifyValue(below.Value);
                    }
                    else
                    {
                        repWithLtmToMod.LastTermModifier = 
                            repWithLtmToMod.GetValueDividedByMultFactor(
                                BeatCell.Subtract(repWithLtmToMod.LastTermModifier, newValue));
                        //repWithLtmToMod.LastTermModifier = repWithLtmToMod.GetValueDividedByMultFactor(newValue);
                        repWithLtmToMod.LastTermModifier = BeatCell.SimplifyValue(repWithLtmToMod.LastTermModifier);
                    }
                }
            }
        }

        /// <summary>
        /// Add a cell's value to the accumulator the correct number of times
        /// </summary>
        /// <param name="target"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="accumulator"></param>
        /// <param name="ltmFactor"></param>
        /// <param name="recursing"></param>
        protected void AddCellValueToAccumulator(Cell target, Cell start, Cell end, StringBuilder accumulator, ref int ltmFactor, bool recursing = false)
        {
            // subtract each value from the total
            if (!target.RepeatGroups.Any())
            {
                accumulator.Append(target.GetValueWithMultFactors()).Append('+');
                return;
            }

            int timesDiff = 1;
            int ltmTimesDiff = 1;
            bool isBehind = target.RepeatGroups.First.Value.Position + target.RepeatGroups.First.Value.FullDuration < start.Position;
            bool contains = !isBehind;
            int times = 1;
            foreach (RepeatGroup rg in target.RepeatGroups.TakeWhile(x => !end.RepeatGroups.Contains(x))) // iterate from innermost group
            {
                if (recursing)
                {
                    if (contains && rg.ExclusiveCells.Contains(start))
                    {
                        // this is the times to subtract because they occur before the starting point.
                        timesDiff = times;
                    }
                    else if (isBehind && rg.Cells.Contains(start))
                    {
                        // subtract a full cycle if this rep group exists all behind the target
                        ltmTimesDiff = timesDiff = times;
                        ltmTimesDiff /= target.RepeatGroups.First().Times;
                        isBehind = false;
                    }
                }

                // break cell(s) may decrease the factor
                if (rg.BreakCell != null)
                {
                    times *= rg.Times - (target == rg.BreakCell || target.Position < rg.BreakCell.Position ? 0 : 1);
                }
                else
                {
                    times *= rg.Times;
                }

                if (contains && recursing && rg.ExclusiveCells.Contains(start))
                {
                    ltmTimesDiff = times;
                    ltmTimesDiff /= target.RepeatGroups.First().Times;
                }
            }

            // handle LTMs
            foreach ((bool opens, Group rg) in target.GroupActions.Where(x => x.Item2 is RepeatGroup && !end.RepeatGroups.Contains(x.Item2)))
            {
                if (!opens)
                {
                    ltmFactor /= ((RepeatGroup)rg).Times;

                    // subtract out the LTM (if group doesn't contain the end point)
                    if (!string.IsNullOrEmpty((rg as RepeatGroup).LastTermModifier))
                    {
                        accumulator.Append(
                            BeatCell.MultiplyTerms(
                                ((RepeatGroup)rg).GetLtmWithMultFactor(), ltmFactor - (recursing ? ltmTimesDiff : 0))).Append('+');
                    }
                }
                else if (!end.RepeatGroups.Contains(rg))
                {
                    ltmFactor *= ((RepeatGroup)rg).Times;
                }
            }

            // account for preceding cells if we are starting mid-way through a rep group
            if (recursing) times -= timesDiff;
            else if (target == start)
            {
                // find outermost rep group that doesn't contain the new cell
                RepeatGroup rg = target.RepeatGroups.Reverse().SkipWhile(x => end.RepeatGroups.Contains(x)).FirstOrDefault();

                if (rg != null)
                {
                    int ltmFactorR = 1;//rg.Cells.First.Value == target ? 1 : rg.Times;
                    foreach (Cell c in rg.Cells.TakeWhile(x => x.Position < target.Position))
                    {
                        AddCellValueToAccumulator(c, start, end, accumulator, ref ltmFactorR, true);
                    }
                }
            }

            if (!string.IsNullOrEmpty(target.Value))
            {
                accumulator.Append(BeatCell.MultiplyTerms(target.GetValueWithMultFactors(), times)).Append('+');
            }
        }
    }
}
