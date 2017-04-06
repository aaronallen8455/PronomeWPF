using System;
using System.Collections.Generic;
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

    public class AddMultGroup : AbstractBeatCodeAction
    {
        protected MultGroup Group;

        public AddMultGroup(Cell[] cells, string factor) : base(cells[0].Row, "Create Multiply Group")
        {
            Group = new MultGroup();
            Group.Row = cells[0].Row;
            Group.Cells = new LinkedList<Cell>(cells);
            Group.Factor = factor;
        }

        protected override void Transformation()
        {
            Group.Cells.First.Value.MultGroups.AddLast(Group);
            Group.Cells.Last.Value.MultGroups.AddLast(Group);

            Group = null;
        }
    }

    public class EditMultGroup : AbstractBeatCodeAction
    {
        protected MultGroup Group;

        protected string Factor;

        public EditMultGroup(MultGroup group, string factor) : base(group.Row, "Edit Multiply Group")
        {
            Group = group;
            Factor = factor;
        }

        protected override void Transformation()
        {
            Group.Factor = Factor;

            Group = null;
        }
    }

    public class RemoveMultGroup : AbstractBeatCodeAction
    {
        protected MultGroup Group;

        public RemoveMultGroup(MultGroup group) : base(group.Row, "Remove Multiply Group")
        {
            Group = group;
        }

        protected override void Transformation()
        {
            foreach (Cell c in Group.Cells)
            {
                c.MultGroups.Remove(Group);
            }

            Group = null;
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
