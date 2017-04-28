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
            // TODO: cells should be able to move past one another
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
                        last.Value = BeatCell.Subtract(last.Value, value);
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
                        last.Value = BeatCell.Add(last.Value, value);
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
                        leftGroup.LastTermModifier = BeatCell.Add(leftGroup.LastTermModifier, value);
                    }
                    else
                    {
                        // add to below cell's value
                        below.Value = BeatCell.Add(below.Value, value);
                    }
                    // subtract from last cell's value if not last of row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        if (useRightGroup)
                        {
                            // subtract from LTM
                            rightGroup.LastTermModifier = BeatCell.Subtract(rightGroup.LastTermModifier, value);
                        }
                        else
                        {
                            last.Value = BeatCell.Subtract(last.Value, value);
                        }
                    }
                }
                else
                {
                    if (useLeftGroup)
                    {
                        // subtract from LTM
                        leftGroup.LastTermModifier = BeatCell.Subtract(leftGroup.LastTermModifier, value);
                    }
                    else
                    {
                        // subtract from below cell's value
                        below.Value = BeatCell.Subtract(below.Value, value);
                    }
                    // add to last cell's value if not last in row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        if (useRightGroup)
                        {
                            rightGroup.LastTermModifier = BeatCell.Add(rightGroup.LastTermModifier, value);
                        }
                        else
                        {
                            last.Value = BeatCell.Add(last.Value, value);
                        }
                    }
                }
            }

            Cells = null;
        }
    }
}
