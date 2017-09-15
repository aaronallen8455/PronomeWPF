using System.Collections.Generic;

namespace Pronome.Editor
{
    public class PasteCells : AbstractBeatCodeAction
    {
        protected int Index;

        protected LinkedList<Cell> Cells;

        protected Cell[] SelectedCells;

        //private int RepCount = 0;
        //private int MultCount = 0;

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
                if (rg.Cells.First.Value.Position < SelectedCells[0].Position && rg.Cells.Last.Value.Position < SelectedCells[SelectedCells.Length - 1].Position)
                {
                    // fix bisected groups
                    var newLast = Row.Cells[Row.Cells.IndexOf(SelectedCells[0]) - 1];
                    rg.Cells.AddLast(newLast);
                }
                else if (rg.Cells.First.Value != SelectedCells[0] || rg.Cells.Last.Value != SelectedCells[SelectedCells.Length - 1])
                {
                    if (rg.Cells.First.Value == SelectedCells[0])
                    {
                        Cells.First.Value.RepeatGroups.AddLast(rg);
                        rg.Cells.AddFirst(Cells.First.Value);
                    }
                    if (Cells.First != Cells.Last && rg.Cells.Last.Value == SelectedCells[SelectedCells.Length - 1])
                    {
                        Cells.Last.Value.RepeatGroups.AddLast(rg);
                        rg.Cells.AddLast(Cells.Last.Value);
                    }
                }
            }
            // fix bisected groups
            foreach (RepeatGroup rg in SelectedCells[SelectedCells.Length - 1].RepeatGroups)
            {
                if (rg.Cells.First.Value.Position > SelectedCells[0].Position && rg.Cells.Last.Value.Position > SelectedCells[SelectedCells.Length - 1].Position)
                {
                    var newFirst = Row.Cells[Row.Cells.IndexOf(SelectedCells[SelectedCells.Length - 1]) + 1];
                    rg.Cells.AddFirst(newFirst);
                }
            }

            foreach (MultGroup mg in SelectedCells[0].MultGroups)
            {
                if (mg.Cells.First.Value.Position < SelectedCells[0].Position && mg.Cells.Last.Value.Position < SelectedCells[SelectedCells.Length - 1].Position)
                {
                    var newLast = Row.Cells[Row.Cells.IndexOf(SelectedCells[0]) - 1];
                    mg.Cells.AddLast(newLast);
                }
                else if (mg.Cells.First.Value != SelectedCells[0] || mg.Cells.Last.Value != SelectedCells[SelectedCells.Length - 1])
                {
                    if (mg.Cells.First.Value == SelectedCells[0])
                    {
                        Cells.First.Value.MultGroups.AddLast(mg);
                        mg.Cells.AddFirst(Cells.First.Value);
                    }
                    if (Cells.First != Cells.Last && mg.Cells.Last.Value == SelectedCells[SelectedCells.Length - 1])
                    {
                        Cells.Last.Value.MultGroups.AddLast(mg);
                        mg.Cells.AddLast(Cells.Last.Value);
                    }
                }
            }

            foreach (MultGroup mg in SelectedCells[SelectedCells.Length - 1].MultGroups)
            {
                if (mg.Cells.First.Value.Position > SelectedCells[0].Position && mg.Cells.Last.Value.Position > SelectedCells[SelectedCells.Length - 1].Position)
                {
                    var newFirst = Row.Cells[Row.Cells.IndexOf(SelectedCells[SelectedCells.Length - 1]) + 1];
                    mg.Cells.AddFirst(newFirst);
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
            //for (int i = 0; i < RepCount; i++)
            //{
            //    Cells.First.Value.RepeatGroups.RemoveLast();
            //    Cells.Last.Value.RepeatGroups.RemoveLast();
            //}
            //for (int i = 0; i < MultCount; i++)
            //{
            //    Cells.First.Value.MultGroups.RemoveLast();
            //    Cells.Last.Value.MultGroups.RemoveLast();
            //}

            Cells = null;
        }
    } 
}
