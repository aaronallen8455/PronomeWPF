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

        /// <summary>
        /// Used to display action name in undo menu
        /// </summary>
        public virtual string HeaderText { get; set; }

        /// <summary>
        /// Code to execute before generating the AfterBeatCode string. Should also dispose of unneeded resources.
        /// </summary>
        abstract protected void Transformation();

        protected int RightIndexBoundOfTransform;

        public AbstractBeatCodeAction(Row row, string headerText, int rightIndexOfTransform = int.MaxValue)
        {
            Row = row;

            if (!row.BeatCodeIsCurrent)
            {
                row.UpdateBeatCode();
            }
            BeforeBeatCode = row.BeatCode;
            BeforeOffset = row.OffsetValue;
            HeaderText = headerText;
            RightIndexBoundOfTransform = rightIndexOfTransform;
        }

        public virtual void Redo()
        {
            // get current selection range if it's in this row
            int selectionStart = -1;
            int selectionEnd = -1;
            int rowLengthBefore = Row.Cells.Count;
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

            //// if no change, don't do anything
            //if (AfterBeatCode == BeforeBeatCode && AfterOffset == BeforeOffset)
            //{
            //    return;
            //}

            bool selectFromBack = selectionEnd > RightIndexBoundOfTransform;

            if (selectFromBack)
            {
                // get index from back of list
                selectionStart = rowLengthBefore - selectionStart;
                selectionEnd = rowLengthBefore - selectionEnd;
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
                if (selectFromBack)
                {
                    // convert back to forward indexed
                    selectionStart = Row.Cells.Count - selectionStart;
                    selectionEnd = Row.Cells.Count - selectionEnd;
                }

                Cell.SelectedCells.SelectRange(selectionStart, selectionEnd, Row);
            }
        }

        public virtual void Undo()
        {
            // if no change, don't do anything
            if (AfterBeatCode == BeforeBeatCode)
            {
                return;
            }

            // get current selection range if it's in this row
            int selectionStart = -1;
            int selectionEnd = -1;

            if (Cell.SelectedCells.Cells.Count > 0 && Cell.SelectedCells.FirstCell.Row == Row)
            {
                selectionStart = Row.Cells.IndexOf(Cell.SelectedCells.FirstCell);
                selectionEnd = Row.Cells.IndexOf(Cell.SelectedCells.LastCell);

            }

            bool selectFromBack = selectionEnd > RightIndexBoundOfTransform;

            if (selectFromBack)
            {
                // get index from back of list
                selectionStart = Row.Cells.Count - selectionStart;
                selectionEnd = Row.Cells.Count - selectionEnd;
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
                if (selectFromBack)
                {
                    // convert back to forward indexed
                    selectionStart = Row.Cells.Count - selectionStart;
                    selectionEnd = Row.Cells.Count - selectionEnd;
                }

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
