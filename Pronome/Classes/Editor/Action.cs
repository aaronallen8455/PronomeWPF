using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Linq;

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

    public abstract class AbstractAction
    {
        protected Row Row;

        /// <summary>
        /// Redraw the rows which reference the row being modified
        /// </summary>
        public void RedrawReferencers()
        {
            if (Row.ReferenceMap.ContainsKey(Row.Index))
            {
                if (!Row.BeatCodeIsCurrent)
                {
                    Row.UpdateBeatCode();
                }
                foreach (int rowIndex in Row.ReferenceMap[Row.Index])
                {
                    Row r = EditorWindow.Instance.Rows[rowIndex];

                    // deselect if in row
                    if (Cell.SelectedCells.FirstCell.Row == r)
                    {
                        Cell.SelectedCells.DeselectAll();
                    }

                    r.Redraw();
                }
            }
        }
    }

    public class AddMultGroup : AbstractAction, IEditorAction
    {
        protected MultGroup Group;

        public string HeaderText { get => "Create Multiply Group"; }

        public AddMultGroup(Cell[] cells, string factor)
        {
            Row = cells[0].Row;

            Group = new MultGroup();
            Group.Factor = factor;
            Group.Cells = new LinkedList<Cell>(cells);
            Group.Position = cells[0].Position;
            Group.Duration = cells.Select(x => x.Duration).Sum();
        }

        public void Redo()
        {
            Row.MultGroups.AddLast(Group);
            // add to main canvas or a rep canvas
            if (Group.Cells.First.Value.RepeatGroups.Count > 0)
            {
                Group.Cells.First.Value.RepeatGroups.Last.Value.Canvas.Children.Add(Group.Rectangle);
                Group.HostCanvas = Group.Cells.First.Value.RepeatGroups.Last.Value.Canvas;
            }
            else
            {
                Row.Canvas.Children.Add(Group.Rectangle);
                Group.HostCanvas = Row.Canvas;
            }

            // add the group to all cell's lists in the correct order
            Cell cell = Group.Cells.First.Value;
            LinkedListNode<MultGroup> before = null;
            if (cell.MultGroups.Any())
            {
                before = cell.MultGroups.Find(cell.MultGroups.Where(x => x.Position < Group.Position).Last());

            }
            // add cells
            foreach (Cell c in Group.Cells)
            {
                if (before != null)
                {
                    c.MultGroups.AddAfter(before, Group);
                }
                else
                {
                    c.MultGroups.AddLast(Group);
                }
            }

            EditorWindow.Instance.SetChangesApplied(false);
            RedrawReferencers();
        }

        public void Undo()
        {
            Row.MultGroups.Remove(Group);

            Group.HostCanvas.Children.Remove(Group.Rectangle);

            foreach (Cell c in Group.Cells)
            {
                c.MultGroups.Remove(Group);
            }

            EditorWindow.Instance.SetChangesApplied(false);
            RedrawReferencers();
        }
    }

    public abstract class AbstractBeatCodeAction : AbstractAction, IEditorAction
    {
        protected string BeforeBeatCode;

        protected string AfterBeatCode;

        protected string _headerText;
        public string HeaderText { get => _headerText; }

        /// <summary>
        /// Code to execute before generating the AfterBeatCode string. Should also dispose of unneeded resources.
        /// </summary>
        abstract protected void Transformation();

        public AbstractBeatCodeAction(Row row, string headerText)
        {
            Row = row;

            if (!row.BeatCodeIsCurrent)
            {
                row.UpdateBeatCode();
            }
            BeforeBeatCode = row.BeatCode;
            _headerText = headerText;
        }

        public virtual void Redo()
        {
            // get current selection range if it's in this row
            int selectionStart = -1;
            int selectionEnd = -1;
            if (Cell.SelectedCells.Cells.Count > 0 && Cell.SelectedCells.FirstCell.Row == Row)
            {
                selectionStart = Row.Cells.IndexOf(Cell.SelectedCells.FirstCell);
                selectionEnd = Row.Cells.IndexOf(Cell.SelectedCells.LastCell);
            }

            if (string.IsNullOrEmpty(AfterBeatCode))
            {
                // perform the transform and get the new beat code
                Transformation();

                AfterBeatCode = Row.Stringify();
            }

            Row.Reset();
            Row.FillFromBeatCode(AfterBeatCode);
            EditorWindow.Instance.SetChangesApplied(false);
            RedrawReferencers();

            if (selectionStart > -1)
            {
                Cell.SelectedCells.SelectRange(selectionStart, selectionEnd, Row);
            }
        }

        public virtual void Undo()
        {
            // get current selection range if it's in this row
            int selectionStart = -1;
            int selectionEnd = -1;
            if (Cell.SelectedCells.Cells.Count > 0 && Cell.SelectedCells.FirstCell.Row == Row)
            {
                selectionStart = Row.Cells.IndexOf(Cell.SelectedCells.FirstCell);
                selectionEnd = Row.Cells.IndexOf(Cell.SelectedCells.LastCell);
            }

            Row.Reset();
            Row.FillFromBeatCode(BeforeBeatCode);
            EditorWindow.Instance.SetChangesApplied(false);
            RedrawReferencers();

            if (selectionStart > -1)
            {
                Cell.SelectedCells.SelectRange(selectionStart, selectionEnd, Row);
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
