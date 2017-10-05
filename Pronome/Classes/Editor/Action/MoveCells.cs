using System;
using System.Linq;

namespace Pronome.Editor
{
    public class MoveCells : AbstractBeatCodeAction
    {
        protected Cell[] Cells;

        /// <summary>
        /// True if cells are being shifted to the right, otherwise shift left.
        /// </summary>
        protected bool ShiftingRight;

        protected string Increment;

        protected int Times;

        public MoveCells(Cell[] cells, string increment, int times) : base(cells[0].Row, cells.Length > 1 ? "Move Cells" : "Move Cell")
        {
            Cells = cells;
            ShiftingRight = times > 0;
            Increment = increment;
            Times = Math.Abs(times);
        }

        protected override void Transformation()
        {
            string value = BeatCell.MultiplyTerms(Increment, Math.Abs(Times));

            Cell last = Cells[Cells.Length - 1];

            if (Row.Cells[0] == Cells[0])
            {
                // selection is at start of row, offset will be changed
                if (ShiftingRight)
                {
                    // add to offset
                    if (string.IsNullOrEmpty(Row.OffsetValue))
                    {
                        Row.OffsetValue = "0";
                    }
                    Row.OffsetValue = BeatCell.Add(Row.OffsetValue, value);
                    // subtract from last cell's value if not last cell of row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        last.Value = BeatCell.Subtract(last.Value, last.GetValueDividedByMultFactors(value));
                    }
                }
                else
                {
                    // subtract from offset
                    Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, value);
                    // zero becomes an empty string, make it zero.
                    if (string.IsNullOrEmpty(Row.OffsetValue))
                    {
                        Row.OffsetValue = "0";
                    }
                    // add to last cell's value if not last cell of row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        last.Value = BeatCell.Add(last.Value, last.GetValueDividedByMultFactors(value));
                    }
                }
            }
            else
            {
                Cell below = Row.Cells[Row.Cells.IndexOf(Cells[0]) - 1];
                // if below is last cell of a repeat group, we instead operate on that group's LTM
                RepeatGroup leftGroup = below.RepeatGroups.Where(x => x.Cells.Last.Value == below).FirstOrDefault();
                bool useLeftGroup = leftGroup != default(RepeatGroup);
                // if last cell in selection is last of a repeat group, operate on it's LTM
                RepeatGroup rightGroup = last.RepeatGroups.Where(x => x.Cells.Last.Value == last).FirstOrDefault();
                bool useRightGroup = rightGroup != default(RepeatGroup);

                if (ShiftingRight)
                {
                    if (useLeftGroup)
                    {
                        // add to LTM
                        leftGroup.LastTermModifier = BeatCell.Add(leftGroup.LastTermModifier, leftGroup.GetValueDividedByMultFactor(value));
                    }
                    else
                    {
                        // add to below cell's value
                        below.Value = BeatCell.Add(below.Value, below.GetValueDividedByMultFactors(value));
                    }
                    // subtract from last cell's value if not last of row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        if (useRightGroup)
                        {
                            // subtract from LTM
                            rightGroup.LastTermModifier = BeatCell.Subtract(rightGroup.LastTermModifier, rightGroup.GetValueDividedByMultFactor(value));
                        }
                        else
                        {
                            last.Value = BeatCell.Subtract(last.Value, last.GetValueDividedByMultFactors(value));
                        }
                    }
                }
                else
                {
                    if (useLeftGroup)
                    {
                        // subtract from LTM
                        leftGroup.LastTermModifier = BeatCell.Subtract(leftGroup.LastTermModifier, leftGroup.GetValueDividedByMultFactor(value));
                    }
                    else
                    {
                        // subtract from below cell's value
                        below.Value = BeatCell.Subtract(below.Value, below.GetValueDividedByMultFactors(value));
                    }
                    // add to last cell's value if not last in row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        if (useRightGroup)
                        {
                            rightGroup.LastTermModifier = BeatCell.Add(rightGroup.LastTermModifier, rightGroup.GetValueDividedByMultFactor(value));
                        }
                        else
                        {
                            last.Value = BeatCell.Add(last.Value, last.GetValueDividedByMultFactors(value));
                        }
                    }
                }
            }

            Cells = null;
        }

        /// <summary>
        /// Check if the move cell action can be performed. (no cell overlap, etc)
        /// </summary>
        /// <returns></returns>
        public static bool CanPerformLeftMove()
        {
            if (Cell.SelectedCells.Cells.Any())
            {
                double move = BeatCell.Parse(EditorWindow.CurrentIncrement);// + .0001;
                Cell first = Cell.SelectedCells.FirstCell;
                // if selection at start of row, check against the offset
                if (first == first.Row.Cells[0])
                {
                    if (first.Row.Offset >= move)
                    {
                        return true;
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
                            belowGroup = below.RepeatGroups.Where(x => x.Cells.Last.Value == below).LastOrDefault();
                        }

                        // if above rep group, check against the LTM
                        if (belowGroup != null)
                        {
                            if (BeatCell.Parse(belowGroup.LastTermModifier) >= move)
                            {
                                return true;
                            }
                        }
                        else
                        {
                            // check against below cell's value
                            if (below.Duration > move)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        public static bool CanPerformRightMove()
        {
            if (Cell.SelectedCells.Cells.Any())
            {
                Cell last = Cell.SelectedCells.LastCell;
                // if last is last of row, then we can execute
                if (last == last.Row.Cells.Last())
                {
                    return true;
                }
                else
                {
                    // check that last's value is greater than the move amount.
                    double move = BeatCell.Parse(EditorWindow.CurrentIncrement);
                    if (last.Duration > move)// + .0001)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
