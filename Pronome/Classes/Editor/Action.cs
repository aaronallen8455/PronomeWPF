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

                    r.Redraw();
                }
            }
        }
    }

    public class AddRepeatGroup : AbstractAction, IEditorAction
    {
        protected string BeforeBeatCode;

        protected string AfterBeatCode;

        protected RepeatGroup Group;

        protected Cell[] Cells;

        public string HeaderText { get => "Add Repeat Group"; }

        public AddRepeatGroup(Cell[] cells, int times, string ltm)
        {
            Cells = cells;

            Group = new RepeatGroup()
            {
                Times = times,
                LastTermModifier = ltm
            };

            Row = Cells[0].Row;
            if (!Row.BeatCodeIsCurrent)
            {
                Row.UpdateBeatCode();
            }
            BeforeBeatCode = Row.BeatCode;

            // the UICommand is where we check that the selected cells can form a rep group.
        }

        public void Undo()
        {
            Row.Reset();
            Row.FillFromBeatCode(BeforeBeatCode);

            RedrawReferencers();

            EditorWindow.Instance.SetChangesApplied(false);
        }

        public void Redo()
        {
            if (string.IsNullOrEmpty(AfterBeatCode))
            {
                // add cells to the group
                Cells[0].RepeatGroups.AddLast(Group);
                if (Cells.Length > 1)
                {
                    Cells[Cells.Length - 1].RepeatGroups.AddLast(Group);
                }

                AfterBeatCode = Row.Stringify();
            }

            Row.Reset();
            Row.FillFromBeatCode(AfterBeatCode);

            RedrawReferencers();

            EditorWindow.Instance.SetChangesApplied(false);
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
