using System;

namespace Pronome.Editor
{
    public class AddRepeatGroup : AbstractBeatCodeAction
    {
        protected RepeatGroup Group;

        protected Cell[] Cells;

        public AddRepeatGroup(Cell[] cells, int times, string ltm) : base(cells[0].Row, "Add Repeat Group")
        {
            Cells = cells;

            Group = new RepeatGroup()
            {
                Times = times,
                LastTermModifier = ltm
            };

            if (!Row.BeatCodeIsCurrent)
            {
                Row.UpdateBeatCode();
            }
            BeforeBeatCode = Row.BeatCode;

            // the UICommand is where we check that the selected cells can form a rep group.
        }

        protected override void Transformation()
        {
            // add cells to the group
            Cells[0].RepeatGroups.AddLast(Group);
            Group.Cells.AddFirst(Cells[0]);
            if (Cells.Length > 1)
            {
                Cells[Cells.Length - 1].RepeatGroups.AddLast(Group);
                Group.Cells.AddLast(Cells[Cells.Length - 1]);
            }

            Cells = null;
        }
    }
}
