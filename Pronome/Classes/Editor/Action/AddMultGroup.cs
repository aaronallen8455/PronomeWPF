using System.Collections.Generic;

namespace Pronome.Editor
{
    public class AddMultGroup : AbstractBeatCodeAction
    {
        protected MultGroup Group;

        public AddMultGroup(Cell[] cells, string factor) : base(cells[0].Row, "Create Multiply Group")
        {
            Group = new MultGroup();
            Group.Row = cells[0].Row;
            Group.Cells = new LinkedList<Cell>(cells);
            Group.FactorValue = factor;
        }

        protected override void Transformation()
        {
            Group.Cells.First.Value.MultGroups.AddLast(Group);
            Group.Cells.Last.Value.MultGroups.AddLast(Group);

            Group = null;
        }
    }
}
