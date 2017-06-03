using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pronome.Editor
{
    public class RemoveCells : AbstractBeatCodeAction
    {
        protected Cell[] Cells;

        //protected string PreviousCellValue;

        protected HashSet<RepeatGroup> RepGroups = new HashSet<RepeatGroup>();

        protected HashSet<MultGroup> MultGroups = new HashSet<MultGroup>();

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

        public RemoveCells(Cell[] cells) : base(cells[0].Row, cells.Length > 1 ? "Remove Cells" : "Remove Cell")
        {
            Cells = cells;
            Row = cells[0].Row;
            //PreviousCellValue = previousCellValue;
            Index = cells[0].Row.Cells.IndexOf(cells[0]);

            StringBuilder duration = new StringBuilder();
            // find all groups that are encompassed by the selection
            HashSet<Group> touchedGroups = new HashSet<Group>();
            RepeatGroup groupBeingAppendedTo = null; // a group who's LTM is actively being augemented
            Queue<RepeatGroup> rgToAppendTo = new Queue<RepeatGroup>(); // RGs that may need to have their LTM added to
            foreach (Cell c in Cells.Where(x => string.IsNullOrEmpty(x.Reference)))
            {
                // add to the LTM of groups with a previous cell in the selection but not this cell
                if (rgToAppendTo.Any() && !c.RepeatGroups.Contains(rgToAppendTo.Peek()))
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
                // add cell's repeat durations if this cell is in the same scope as the first cell.
                if ((!c.RepeatGroups.Any() && !Cell.SelectedCells.FirstCell.RepeatGroups.Any()) ||
                    c.RepeatGroups.Last?.Value == Cell.SelectedCells.FirstCell.RepeatGroups?.Last.Value)
                {
                    duration.Append("+0").Append(BeatCell.MultiplyTerms(c.Value, times));
                }
                // add any LTM's from repeat groups
                foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                {
                    duration.Append("+0").Append(BeatCell.MultiplyTerms(kv.Key.LastTermModifier, kv.Value));
                    Duration += BeatCell.Parse(kv.Key.LastTermModifier) * kv.Value;
                }
            }

            BeatCodeDuration = BeatCell.SimplifyValue(duration.ToString());
        }

        public override void Undo()
        {
            base.Undo();

            if (ChangeOffset)
            {
                Row.Offset -= Duration;
                Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, BeatCodeDuration);
            }
        }

        protected override void Transformation()
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
                if (!firstCell.RepeatGroups.Any() || !RepGroups.Contains(firstCell.RepeatGroups.First.Value))
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
                    if (!firstCell.RepeatGroups.Contains(rg))
                    {
                        groupToAddTo = rg;
                    }
                    else break;
                }

                if (groupToAddTo != null)
                {
                    groupToAddTo.LastTermModifier = BeatCell.Add(groupToAddTo.LastTermModifier, BeatCodeDuration);
                }
                else if (!firstCell.RepeatGroups.Any() || prevCell.RepeatGroups.Contains(firstCell.RepeatGroups.Last.Value))
                {
                    // otherwise, increase the prev cell's duration
                    // but only if it is not the cell prior to a repgroup for which first cell of select is first cell of the rep group.
                    prevCell.Value = BeatCell.Add(prevCell.Value, BeatCodeDuration);
                }

            }

            // no longer need these
            RepGroups = null;
            MultGroups = null;
            Cells = null;
        }

        public override void Redo()
        {
            base.Redo();

            if (ChangeOffset)
            {
                Row.Offset += Duration;
                Row.OffsetValue = BeatCell.Add(Row.OffsetValue, BeatCodeDuration);
            }
        }
    }
}
