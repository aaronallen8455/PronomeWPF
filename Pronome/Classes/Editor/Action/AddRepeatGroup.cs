namespace Pronome.Editor
{
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
            // get current selection range if it's in this row
            int selectionStart = -1;
            int selectionEnd = -1;
            if (Cell.SelectedCells.FirstCell.Row == Row)
            {
                selectionStart = Cell.SelectedCells.FirstCell.Row.Cells.IndexOf(Cell.SelectedCells.FirstCell);
                selectionEnd = Cell.SelectedCells.FirstCell.Row.Cells.IndexOf(Cell.SelectedCells.LastCell);
            }

            Row.Reset();
            Row.FillFromBeatCode(BeforeBeatCode);

            RedrawReferencers();

            EditorWindow.Instance.SetChangesApplied(false);

            if (selectionStart >= 0)
            {
                Cell.SelectedCells.SelectRange(selectionStart, selectionEnd, Row);
            }
        }

        public void Redo()
        {
            if (string.IsNullOrEmpty(AfterBeatCode))
            {
                // add cells to the group
                Cells[0].RepeatGroups.AddLast(Group);
                Group.Cells.AddFirst(Cells[0]);
                if (Cells.Length > 1)
                {
                    Cells[Cells.Length - 1].RepeatGroups.AddLast(Group);
                    Group.Cells.AddLast(Cells[Cells.Length - 1]);
                }

                AfterBeatCode = Row.Stringify();
            }

            // get current selection range if it's in this row
            int selectionStart = -1;
            int selectionEnd = -1;
            if (Cell.SelectedCells.FirstCell.Row == Row)
            {
                selectionStart = Cell.SelectedCells.FirstCell.Row.Cells.IndexOf(Cell.SelectedCells.FirstCell);
                selectionEnd = Cell.SelectedCells.FirstCell.Row.Cells.IndexOf(Cell.SelectedCells.LastCell);
            }

            Row.Reset();
            Row.FillFromBeatCode(AfterBeatCode);

            RedrawReferencers();

            EditorWindow.Instance.SetChangesApplied(false);

            if (selectionStart >= 0)
            {
                Cell.SelectedCells.SelectRange(selectionStart, selectionEnd, Row);
            }
        }
    }
}
