using System.Linq;
using System.Windows.Controls;

namespace Pronome.Editor
{
    /// <summary>
    /// Holds the methods used by the AddCell and RemoveCell actions
    /// </summary>
    public abstract class AddRemoveCell : AbstractAction
    {
        protected Cell Cell;

        protected int Index;

        protected Cell PreviousCell;

        protected string PreviousCellBeforeValue;
        protected string PreviousCellAfterValue;

        /// <summary>
        /// The add cell action. Should be initialized after the new cell has been added into the row.
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="previousCell"></param>
        public AddRemoveCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null)
        {
            Cell = cell;
            Row = cell.Row;
            Index = Cell.Row.Cells.IndexOf(cell);

            PreviousCell = previousCell;

            if (previousCell != null)
            {
                PreviousCellAfterValue = previousCell.Value;
                PreviousCellBeforeValue = prevBeforeVal;
            }
        }

        public void Add()
        {
            // Add in the cell
            Row.Cells.Insert(Index, Cell);
            
            // render the rectangle
            if (Cell.RepeatGroups.Any())
            {
                Cell.RepeatGroups.Last.Value.Canvas.Children.Add(Cell.Rectangle);
            }
            else
            {
                Row.Canvas.Children.Add(Cell.Rectangle);
            }

            // modify the previous cell or the row's offset
            Cell below = null;

            if (Index == 0)
            {
                Row.Duration += Cell.Duration;
                Row.Offset -= Cell.Duration;
                Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, Cell.Value);
            }
            else
            {
                below = Row.Cells[Index - 1];

                below.Value = PreviousCellAfterValue;
                below.SetDurationDirectly(BeatCell.Parse(below.Value));

                if (Index == Row.Cells.Count - 1)
                {
                    // it's the last cell, resize sizer
                    double oldDur = Row.Duration;
                    Row.Duration = Cell.Position + Cell.Duration;
                    Row.ChangeSizerWidthByAmount(Row.Duration - oldDur);
                }
            }
            //TODO: check if need to adjust the LCM of the last repeat group if this is the last cell in row

            // add to groups
            foreach (RepeatGroup rg in Cell.RepeatGroups)
            {
                if (below != null && below.RepeatGroups.Contains(rg))
                {
                    rg.Cells.AddAfter(rg.Cells.Find(below), Cell);
                }
                else
                {
                    rg.Cells.AddFirst(Cell);
                }
            }
            foreach (MultGroup mg in Cell.MultGroups)
            {
                if (below != null && below.MultGroups.Contains(mg))
                {
                    mg.Cells.AddAfter(mg.Cells.Find(below), Cell);
                }
                else
                {
                    mg.Cells.AddFirst(Cell);
                }
            }

            Row.BeatCodeIsCurrent = false;

            RedrawReferencers();

            EditorWindow.Instance.SetChangesApplied(false);
        }

        public void Remove()
        {
            // deselect cell if selected
            if (Cell.IsSelected)
            {
                Cell.ToggleSelect();

                EditorWindow.Instance.UpdateUiForSelectedCell();
            }
            // remove the rect
            if (Cell.RepeatGroups.Any())
            {
                Cell.RepeatGroups.Last.Value.Canvas.Children.Remove(Cell.Rectangle);
            }
            else
            {
                Row.Canvas.Children.Remove(Cell.Rectangle);
            }
            // modify previous cell or row's offset
            if (Index == 0)
            {
                Row.Duration -= Cell.Duration;
                // need to set the row's offset
                Row.Offset += Cell.Duration;
                Row.OffsetValue = BeatCell.SimplifyValue(Row.OffsetValue + "+0" + Cell.Value);
                // place above cell at front of sizer and shrink sizer width
                Cell above = Row.Cells[1];
                above.Position = 0;
                //row.ChangeSizerWidthByAmount(-Cell.Duration);
            }
            else
            {
                Cell below = Row.Cells[Index - 1];

                below.Value = PreviousCellBeforeValue;
                // if cell is the last cell, resize the below cell. Otherwise set duration directly
                if (Row.Cells.Last() == Cell)
                {
                    // preserve cell's position
                    double oldPos = Cell.Position;
                    double oldOffset = Canvas.GetLeft(Cell.Rectangle);
                    below.Duration = BeatCell.Parse(below.Value);
                    // reset position
                    Cell.Position = oldPos;
                    Canvas.SetLeft(Cell.Rectangle, oldOffset);
                    // resize row
                    Row.Duration = below.Position + below.Duration;
                    Row.ChangeSizerWidthByAmount(-Cell.Duration);
                }
                else
                {
                    below.SetDurationDirectly(BeatCell.Parse(below.Value));
                }
            }
            // remove from groups
            foreach (RepeatGroup rg in Cell.RepeatGroups)
            {
                rg.Cells.Remove(Cell);
                rg.Canvas.Children.Remove(Cell.Rectangle);
                //// remove the group if no cells left
                //if (!rg.Cells.Any())
                //{
                //    rg.RemoveGroupFromRow();
                //}
            }
            foreach (MultGroup mg in Cell.MultGroups)
            {
                mg.Cells.Remove(Cell);
                //if (!mg.Cells.Any())
                //{
                //    mg.RemoveGroupFromRow();
                //}
            }

            Row.Cells.Remove(Cell);

            Row.BeatCodeIsCurrent = false;

            RedrawReferencers();

            EditorWindow.Instance.SetChangesApplied(false);
        }
    }

    public class AddCell : AddRemoveCell, IEditorAction
    {
        private string _headerText = "Add Cell";
        public string HeaderText { get => _headerText; }

        public AddCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null) : base(cell, previousCell, prevBeforeVal)
        {

        }

        public void Redo()
        {
            Add();
        }
        public void Undo()
        {
            Remove();
        }
    }

    public class RemoveCell : AddRemoveCell, IEditorAction
    {
        private string _headerText = "Remove Cell";
        public string HeaderText { get => _headerText; }

        public RemoveCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null) : base(cell, previousCell, prevBeforeVal)
        {

        }

        public void Redo()
        {
            Remove();
        }

        public void Undo()
        {
            Add();
        }
    }
}
