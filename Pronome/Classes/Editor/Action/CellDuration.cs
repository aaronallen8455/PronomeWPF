using System.Linq;

namespace Pronome.Editor
{
    public class CellDuration : AbstractAction, IEditorAction
    {
        protected Cell[] Cells;

        protected double[] PreviousDurations;

        protected string[] PreviousValues;

        protected double NewDuration;

        protected string NewValue;

        public string HeaderText { get => Cells.Length > 1 ? "Change Cells' Duration" : "Change Cell's Duration"; }

        /// <summary>
        /// Should be done before changing the cell properties.
        /// </summary>
        /// <param name="cells"></param>
        /// <param name="newValue">What will be the new value</param>
        /// <param name="newDuration">What will be the new duration</param>
        public CellDuration(Cell[] cells, string newValue, double newDuration)
        {
            Cells = cells;
            PreviousDurations = Cells.Select(x => x.Duration).ToArray();
            PreviousValues = Cells.Select(x => x.Value).ToArray();
            NewValue = newValue;
            NewDuration = newDuration;
            Row = cells[0].Row;
        }

        public void Undo()
        {
            for (int i = 0; i < Cells.Length; i++)
            {
                Cells[i].Duration = PreviousDurations[i];
                Cells[i].Value = PreviousValues[i];
            }

            //UpdateUI();
            EditorWindow.Instance.UpdateUiForSelectedCell();

            RedrawReferencers();
        }

        public void Redo()
        {
            foreach (Cell c in Cells)
            {
                c.Value = NewValue;
                c.Duration = NewDuration;
            }

            //UpdateUI();
            EditorWindow.Instance.UpdateUiForSelectedCell();

            RedrawReferencers();
        }

        protected void UpdateUI()
        {
            if (Cell.SelectedCells.Cells.Count == 1 && Cells.Contains(Cell.SelectedCells.FirstCell))
            {
                EditorWindow.Instance.durationInput.Text = Cell.SelectedCells.FirstCell.Value;
            }
            else
            {
                EditorWindow.Instance.durationInput.Text = "";
            }
        }
    }
}
