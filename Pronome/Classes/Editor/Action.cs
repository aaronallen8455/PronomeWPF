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

    public class MoveCells : AbstractBeatCodeAction
    {
        protected Cell[] Cells;

        /// <summary>
        /// True if cells are being shifted to the right, otherwise shift left.
        /// </summary>
        protected bool ShiftingRight;

        protected string Increment;

        protected int Times;

        public MoveCells(Cell[] cells, string increment, int times) : base(cells[0].Row, cells.Length > 1 ? "Move Cells" : "Move Cell")
        {
            Cells = cells;
            ShiftingRight = times > 0;
            Increment = increment;
            Times = times;
        }

        protected override void Transformation()
        {
            string value = BeatCell.MultiplyTerms(Increment, Times);

            Cell last = Cells[Cells.Length - 1];

            if (Row.Cells[0] == Cells[0])
            {
                // selection is at start of row, offset will be changed
                if (ShiftingRight)
                {
                    // add to offset
                    if (string.IsNullOrEmpty(Row.OffsetValue))
                    {
                        Row.OffsetValue = "0";
                    }
                    Row.OffsetValue = BeatCell.Add(Row.OffsetValue, value);
                    // subtract from last cell's value if not last cell of row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        last.Value = BeatCell.Subtract(last.Value, value);
                    }
                }
                else
                {
                    // subtract from offset
                    Row.OffsetValue = BeatCell.Subtract(Row.OffsetValue, value);
                    // add to last cell's value if not last cell of row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        last.Value = BeatCell.Add(last.Value, value);
                    }
                }
            }
            else
            {
                Cell below = Row.Cells[Row.Cells.IndexOf(Cells[0])];
                // if below is last cell of a repeat group, we instead operate on that group's LTM
                RepeatGroup leftGroup = below.RepeatGroups.Where(x => x.Cells.Last.Value == below).FirstOrDefault();
                bool useLeftGroup = leftGroup != default(RepeatGroup);
                // if last cell in selection is last of a repeat group, operate on it's LTM
                RepeatGroup rightGroup = last.RepeatGroups.Where(x => x.Cells.Last.Value == last).FirstOrDefault();
                bool useRightGroup = rightGroup != default(RepeatGroup);

                if (ShiftingRight)
                {
                    if (useLeftGroup)
                    {
                        // add to LTM
                        leftGroup.LastTermModifier = BeatCell.Add(leftGroup.LastTermModifier, value);
                    }
                    else
                    {
                        // add to below cell's value
                        below.Value = BeatCell.Add(below.Value, value);
                    }
                    // subtract from last cell's value if not last of row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        if (useRightGroup)
                        {
                            // subtract from LTM
                            rightGroup.LastTermModifier = BeatCell.Subtract(rightGroup.LastTermModifier, value);
                        }
                        else
                        {
                            last.Value = BeatCell.Subtract(last.Value, value);
                        }
                    }
                }
                else
                {
                    if (useLeftGroup)
                    {
                        // subtract from LTM
                        leftGroup.LastTermModifier = BeatCell.Subtract(leftGroup.LastTermModifier, value);
                    }
                    else
                    {
                        // subtract from below cell's value
                        below.Value = BeatCell.Subtract(below.Value, value);
                    }
                    // add to last cell's value if not last in row
                    if (last != Row.Cells[Row.Cells.Count - 1])
                    {
                        if (useRightGroup)
                        {
                            rightGroup.LastTermModifier = BeatCell.Add(rightGroup.LastTermModifier, value);
                        }
                        else
                        {
                            last.Value = BeatCell.Add(last.Value, value);
                        }
                    }
                }
            }

            Cells = null;
        }
    }

    public abstract class AbstractBeatCodeAction : AbstractAction, IEditorAction
    {
        /// <summary>
        /// The row's beat code before transformation
        /// </summary>
        protected string BeforeBeatCode;

        /// <summary>
        /// The row's beat code after transformation
        /// </summary>
        protected string AfterBeatCode;

        /// <summary>
        /// The row's offset before transformation, in beat code
        /// </summary>
        protected string BeforeOffset;

        /// <summary>
        /// The row's offset after transformation, in beat code
        /// </summary>
        protected string AfterOffset;

        protected string _headerText;
        /// <summary>
        /// Used to display action name in undo menu
        /// </summary>
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
            BeforeOffset = row.OffsetValue;
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
                AfterOffset = Row.OffsetValue;
            }

            Row.Reset();
            Row.FillFromBeatCode(AfterBeatCode);
            if (BeforeOffset != AfterOffset)
            {
                Row.OffsetValue = AfterOffset;
                Row.Offset = BeatCell.Parse(AfterOffset);
            }
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
            if (BeforeOffset != AfterOffset)
            {
                Row.OffsetValue = BeforeOffset;
                Row.Offset = BeatCell.Parse(BeforeOffset);
            }
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
            else
            {
                MenuItem.Header = Prefix + " " + Peek().HeaderText;
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
