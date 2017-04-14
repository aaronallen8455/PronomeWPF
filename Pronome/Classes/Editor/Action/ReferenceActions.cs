namespace Pronome.Editor
{
    public class AddReference : AbstractBeatCodeAction
    {
        protected Cell Cell;

        protected int ReferenceIndex;

        public AddReference(Cell cell, int refIndex) : base(cell.Row, "Insert Reference", cell.Row.Cells.IndexOf(cell))
        {
            Cell = cell;
            ReferenceIndex = refIndex;
        }

        protected override void Transformation()
        {
            // is this a self reference?
            string r = Row.Index.ToString() == ReferenceIndex.ToString() ? "s" : ReferenceIndex.ToString();
            Cell.Reference = ReferenceIndex.ToString();

            Cell = null;
        }
    }

    public class EditReference : AddReference
    {
        public override string HeaderText { get => "Edit Reference"; }

        public EditReference(Cell cell, int refIndex) : base(cell, refIndex) { }
    }

    public class RemoveReference : AbstractBeatCodeAction
    {
        protected Cell Cell;

        public RemoveReference(Cell cell) : base(cell.Row, "Remove Reference", cell.Row.Cells.IndexOf(cell))
        {
            Cell = cell;
        }

        protected override void Transformation()
        {
            Cell.Reference = null;

            // give a value if none
            if (string.IsNullOrEmpty(Cell.Value))
            {
                Cell.Value = "1";
            }

            Cell = null;
        }
    }
}
