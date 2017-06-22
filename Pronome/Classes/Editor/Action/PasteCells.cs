using System.Collections.Generic;

namespace Pronome.Editor
{
    public class PasteCells : AbstractBeatCodeAction
    {
        protected int Index;

        protected LinkedList<Cell> Cells;

        protected Cell[] SelectedCells;

        private int RepCount = 0;
        private int MultCount = 0;

        public PasteCells(int index, Row row, LinkedList<Cell> cells, Cell[] selectedCells, int rightIndex) : base(row, "Paste", rightIndex)
        {
            Index = index;
            Cells = cells;
            SelectedCells = selectedCells;
        }

        protected override void Transformation()
        {
            // add any applicable groups to the new cells
            foreach (RepeatGroup rg in SelectedCells[0].RepeatGroups)
            {
                if (rg.Cells.First.Value == SelectedCells[0]
                    && rg.Cells.Last.Value == SelectedCells[SelectedCells.Length - 1])
                {
                    Cells.First.Value.RepeatGroups.AddLast(rg);
                    Cells.Last.Value.RepeatGroups.AddLast(rg);
                }
            }

            foreach (MultGroup mg in SelectedCells[0].MultGroups)
            {
                if (mg.Cells.First.Value == SelectedCells[0]
                    && mg.Cells.Last.Value == SelectedCells[SelectedCells.Length - 1])
                {
                    Cells.First.Value.MultGroups.AddLast(mg);
                    Cells.Last.Value.MultGroups.AddLast(mg);
                }
            }

            // swap the cells
            Row.Cells.RemoveRange(Index, SelectedCells.Length);
            Row.Cells.InsertRange(Index, Cells);

            SelectedCells = null;
        }

        public override void Redo()
        {
            base.Redo();

            // remove the groups that were added to the selection from the pasted area
            for (int i = 0; i < RepCount; i++)
            {
                Cells.First.Value.RepeatGroups.RemoveLast();
                Cells.Last.Value.RepeatGroups.RemoveLast();
            }
            for (int i = 0; i < MultCount; i++)
            {
                Cells.First.Value.MultGroups.RemoveLast();
                Cells.Last.Value.MultGroups.RemoveLast();
            }

            Cells = null;
        }
    } 
}
