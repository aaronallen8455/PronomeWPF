using System.Linq;
using System.Windows.Controls;
using System.Windows.Shapes;

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

        protected string PreviousCellBeforeValue; // or LTM of a rep group
        protected string PreviousCellAfterValue; // or LTM of a rep group

        /// <summary>
        /// The add cell action. Should be initialized after the new cell has been added into the row.
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="previousCell"></param>
        public AddRemoveCell(Cell cell, Cell previousCell = null, string prevBeforeVal = null, string prevAfterValue = null)
        {
            Cell = cell;
            Row = cell.Row;
            Index = Cell.Row.Cells.IndexOf(cell);

            PreviousCell = previousCell;

            if (previousCell != null)
            {
                PreviousCellAfterValue = prevAfterValue;
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
                // check if need to adjust the LCM of the last repeat group if this is the last cell in row
                RepeatGroup ltmToMod = null;
                foreach (RepeatGroup rg in below.RepeatGroups.Where(x => x.Cells.Last.Value == below))
                {
                    ltmToMod = rg;
                }

                if (ltmToMod == null)
                {
                    below.Value = PreviousCellAfterValue;
                    below.SetDurationDirectly(BeatCell.Parse(below.Value));
                }
                else
                {
                    ltmToMod.LastTermModifier = PreviousCellAfterValue;
                }

                if (Index == Row.Cells.Count - 1 && !Cell.RepeatGroups.Any())
                {
                    // it's the last cell, resize sizer
                    // but not if it's in a group - size will stay the same.
                    double oldDur = Row.Duration;
                    Row.Duration = Cell.Position + Cell.Duration;
                    Row.ChangeSizerWidthByAmount(Row.Duration - oldDur);
                }
            }

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

                // resize host rects if this is last cell in row
                if (rg.Cells.Last.Value == Cell)
                {
                    foreach (Rectangle rect in rg.HostRects)
                    {
                        rect.Width += below.Duration * EditorWindow.Scale * EditorWindow.BaseFactor;
                    }
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

                // check if a LTM is being changed instead of cell value
                RepeatGroup ltmToMod = null;
                foreach (RepeatGroup rg in below.RepeatGroups.Where(x => x.Cells.Last.Value == below))
                {
                    ltmToMod = rg;
                }

                if (ltmToMod == null)
                {
                    below.Value = PreviousCellBeforeValue;
                }
                else
                {
                    ltmToMod.LastTermModifier = PreviousCellBeforeValue;
                }

                // if cell is the last cell, resize the below cell. Otherwise set duration directly
                if (Row.Cells.Last() == Cell && !Cell.RepeatGroups.Any())
                {
                    // preserve cell's position
                    double oldPos = Cell.Position;
                    double oldOffset = Canvas.GetLeft(Cell.Rectangle);
                    if (ltmToMod == null)
                    {
                        below.Duration = BeatCell.Parse(below.Value);
                        // reset position
                        Cell.Position = oldPos;
                    }
                    //Canvas.SetLeft(Cell.Rectangle, oldOffset);
                    // resize row
                    Row.Duration = below.Position + below.Duration;
                    Row.ChangeSizerWidthByAmount(-Cell.Duration);
                }
                else if (ltmToMod == null)
                {
                    below.SetDurationDirectly(BeatCell.Parse(below.Value));
                }
            }
            // remove from groups
            foreach (RepeatGroup rg in Cell.RepeatGroups)
            {
                // if this was the last cell in group, need to resize the host rects
                if (rg.Cells.Last.Value == Cell)
                {
                    foreach (Rectangle rect in rg.HostRects)
                    {
                        rect.Width -= (rg.Cells.Last.Previous.Value.Duration - Cell.Duration) * EditorWindow.Scale * EditorWindow.BaseFactor;
                    }
                }

                rg.Cells.Remove(Cell);
                rg.Canvas.Children.Remove(Cell.Rectangle);
            }
            foreach (MultGroup mg in Cell.MultGroups)
            {
                mg.Cells.Remove(Cell);
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
