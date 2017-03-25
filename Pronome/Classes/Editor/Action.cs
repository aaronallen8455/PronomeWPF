using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Pronome.Editor
{
    /// <summary>
    /// Enum of all possible action types so we know which type to cast the action to
    /// </summary>
    public enum ActionType { AddCell, DeleteCell, ChangeCellPosition, ChangeCellDuration, ChangeCellSource };

    public interface IEditorAction
    {
        void Redo();
        void Undo();

        /// <summary>
        /// String describing the action
        /// </summary>
        string HeaderText { get; }
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

    /// <summary>
    /// Holds the methods used by the AddCell and RemoveCell actions
    /// </summary>
    public abstract class AddRemoveCell
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
            Row row = Cell.Row;
            // Add in the cell
            row.Cells.Insert(Index, Cell);
            
            // render the rectangle
            if (Cell.RepeatGroups.Any())
            {
                Cell.RepeatGroups.Last.Value.Canvas.Children.Add(Cell.Rectangle);
            }
            else
            {
                row.Canvas.Children.Add(Cell.Rectangle);
            }

            // modify the previous cell or the row's offset
            Cell below = null;

            if (Index == 0)
            {
                row.Duration += Cell.Duration;
                row.Offset -= Cell.Duration;
                row.OffsetValue = BeatCell.Subtract(row.OffsetValue, Cell.Value);
            }
            else
            {
                below = row.Cells[Index - 1];

                below.Value = PreviousCellAfterValue;
                below.SetDurationDirectly(BeatCell.Parse(below.Value));

                if (Index == row.Cells.Count - 1)
                {
                    // it's the last cell, resize sizer
                    double oldDur = row.Duration;
                    row.Duration = Cell.Position + Cell.Duration;
                    row.ChangeSizerWidthByAmount(row.Duration - oldDur);
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
        }

        public void Remove()
        {
            Row row = Cell.Row;

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
                row.Canvas.Children.Remove(Cell.Rectangle);
            }
            // modify previous cell or row's offset
            if (Index == 0)
            {
                row.Duration -= Cell.Duration;
                // need to set the row's offset
                row.Offset += Cell.Duration;
                row.OffsetValue = BeatCell.SimplifyValue(row.OffsetValue + "+0" + Cell.Value);
                // place above cell at front of sizer and shrink sizer width
                Cell above = row.Cells[1];
                above.Position = 0;
                //row.ChangeSizerWidthByAmount(-Cell.Duration);
            }
            else
            {
                Cell below = row.Cells[Index - 1];

                below.Value = PreviousCellBeforeValue;
                // if cell is the last cell, resize the below cell. Otherwise set duration directly
                if (row.Cells.Last() == Cell)
                {
                    // preserve cell's position
                    double oldPos = Cell.Position;
                    double oldOffset = Canvas.GetLeft(Cell.Rectangle);
                    below.Duration = BeatCell.Parse(below.Value);
                    // reset position
                    Cell.Position = oldPos;
                    Canvas.SetLeft(Cell.Rectangle, oldOffset);
                    // resize row
                    row.Duration = below.Position + below.Duration;
                    row.ChangeSizerWidthByAmount(-Cell.Duration);
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

            row.Cells.Remove(Cell);
        }
    }

    public class RemoveCells : IEditorAction
    {
        public string HeaderText { get => "Remove Cell(s)"; }

        protected Cell[] Cells;

        protected string PreviousCellValue;

        protected HashSet<RepeatGroup> RepGroups = new HashSet<RepeatGroup>();

        protected HashSet<MultGroup> MultGroups = new HashSet<MultGroup>();

        /// <summary>
        /// Total duration in BPM of the group
        /// </summary>
        protected double Duration = 0;

        /// <summary>
        /// Index of the first cell in the selection
        /// </summary>
        protected int Index;

        public RemoveCells(Cell[] cells, string previousCellValue)
        {
            Cells = cells;
            PreviousCellValue = previousCellValue;
            Index = cells[0].Row.Cells.IndexOf(cells[0]);

            // find all groups that are encompassed by the selection
            HashSet<Group> touchedGroups = new HashSet<Group>();
            foreach (Cell c in Cells)
            {
                Duration += c.Duration;

                // TODO: need to allow for removal from within a rep group. Group (and it's dupes) needs to be resized and all effected cells repositioned

                foreach (RepeatGroup rg in c.RepeatGroups)
                {
                    if (touchedGroups.Contains(rg)) continue;
                    touchedGroups.Add(rg);
                    if (
                        (Cells[0] == rg.Cells.First.Value || rg.Position >= Cells[0].Position) 
                        && (Cells[Cells.Length - 1] == rg.Cells.Last.Value || rg.Position + rg.Duration <= Cells[Cells.Length - 1].Position + Cells[Cells.Length - 1].Duration))
                    {
                        RepGroups.Add(rg);
                    }
                    // if a rep group ends on this cell, add it's dupes to the duration
                    if (rg.Cells.Last.Value == c)
                    {
                        Duration += rg.Duration * (rg.Times - 1);
                    }
                }
                foreach (MultGroup mg in c.MultGroups)
                {
                    if (touchedGroups.Contains(mg)) continue;
                    touchedGroups.Add(mg);
                    if (
                        (Cells[0] == mg.Cells.First.Value || mg.Position >= Cells[0].Position)
                        && (Cells[Cells.Length - 1] == mg.Cells.Last.Value || mg.Position + mg.Duration <= Cells[Cells.Length - 1].Position + Cells[Cells.Length - 1].Duration))
                    {
                        MultGroups.Add(mg);
                    }
                }
            }
        }

        public void Undo()
        {

        }

        public void Redo()
        {
            Row row = Cells[0].Row;

            // is the selection at the front, middle, or back of the row?
            if (Index == 0)
            {
                // can't delete all cells in row
                if (Cells.Length == row.Cells.Count)
                {
                    return;
                }
                // remove from front
                row.Cells.RemoveRange(Index, Cells.Length);
                foreach (Cell c in Cells)
                {
                    row.Cells.Remove(c);
                    // remove rectangle
                    if (!c.RepeatGroups.Any())
                    {
                        row.Canvas.Children.Remove(c.Rectangle);
                    }
                    else if (!RepGroups.Contains(c.RepeatGroups.Last.Value))
                    {
                        c.RepeatGroups.Last.Value.Canvas.Children.Remove(c.Rectangle);
                    }
                }
                // remove groups that are in selection
                foreach (RepeatGroup rg in RepGroups)
                {
                    row.Canvas.Children.Remove(rg.Rectangle);
                    rg.HostCanvas.Children.Remove(rg.Canvas);
                    foreach (Rectangle rect in rg.HostRects)
                    {
                        rg.HostCanvas.Children.Remove(rect);
                    }

                    row.RepeatGroups.Remove(rg);
                }
                foreach (MultGroup mg in MultGroups)
                {
                    row.Canvas.Children.Remove(mg.Rectangle);
                    row.MultGroups.Remove(mg);
                }
                // reposition other cells
                bool isFirst = true;
                double offset = 0;
                foreach (Cell c in row.Cells)
                {
                    if (isFirst)
                    {
                        offset = c.Position;
                        isFirst = false;
                    }

                    c.Position -= offset;
                }

                row.Duration -= offset;
                row.ChangeSizerWidthByAmount(-offset);
            }
            else if (Cells.Last() != row.Cells.Last())
            {
                // remove from middle
            }
            else
            {
                // remove from end
            }
        }
    }

    /// <summary>
    /// Adds extra functionality to a stack to implement Undo/Redo
    /// </summary>
    public class ActionStack : Stack<IEditorAction>
    {
        private MenuItem MenuItem;

        private string Prefix;

        public ActionStack(MenuItem menuItem, int size) : base(size)
        {
            MenuItem = menuItem;
            Prefix = menuItem.Header.ToString();
        }

        new public void Push(IEditorAction action)
        {
            // append the header text
            MenuItem.Header = Prefix + " " + action.HeaderText;

            base.Push(action);
        }

        new public IEditorAction Pop()
        {
            // return to default if empty
            IEditorAction action = base.Pop();
            if (Count == 0)
            {
                MenuItem.Header = Prefix;
            }
            return action;
        }

        new public void Clear()
        {
            base.Clear();
            MenuItem.Header = Prefix;
        }
    }
}
