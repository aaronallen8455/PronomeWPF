using System.Collections.Generic;
using System.Linq;

namespace Pronome.Editor
{
    public class CellSource : IEditorAction
    {
        public string HeaderText { get => "Change Sound Source" + (Length > 1 ? "s" : ""); }

        /// <summary>
        /// The original sound sources
        /// </summary>
        protected LinkedList<ISoundSource> OldSources;

        /// <summary>
        /// The new sound sources
        /// </summary>
        protected ISoundSource NewSource;

        /// <summary>
        /// Index of the cell where the selection starts
        /// </summary>
        protected int StartIndex;

        /// <summary>
        /// Number of cells selected
        /// </summary>
        protected int Length;

        /// <summary>
        /// The row containing the selected cells
        /// </summary>
        protected Row Row;

        public CellSource(Cell.Selection cells, ISoundSource newSource)
        {
            Row = cells.FirstCell.Row;

            StartIndex = Row.Cells.IndexOf(cells.FirstCell);

            Length = cells.Cells.Count;

            OldSources = new LinkedList<ISoundSource>(cells.Cells.Select(x => x.Source));

            NewSource = newSource;
        }

        public void Redo()
        {
            // assign the new source to each cell
            foreach (Cell cell in Row.Cells.GetRange(StartIndex, Length))
            {
                cell.Source = NewSource;
            }

            Row.BeatCodeIsCurrent = false;
            EditorWindow.Instance.UpdateUiForSelectedCell();
            EditorWindow.Instance.SetChangesApplied(false);
        }

        public void Undo()
        {
            // revert to old source
            var node = OldSources.First;
            for (int i = StartIndex; i < StartIndex + Length; i++)
            {
                Row.Cells[i].Source = node.Value;
                node = node.Next;
            }

            Row.BeatCodeIsCurrent = false;
            EditorWindow.Instance.UpdateUiForSelectedCell();
            EditorWindow.Instance.SetChangesApplied(false);
        }
    }
}
