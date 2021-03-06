﻿using System;
using System.Linq;

namespace Pronome.Editor
{
    public class CellDuration : AbstractBeatCodeAction
    {
        protected Cell[] Cells;

        //protected double[] PreviousDurations;

        protected string[] PreviousValues;

        protected string NewValue;

        //public string HeaderText { get => Cells.Length > 1 ? "Change Cells' Duration" : "Change Cell's Duration"; }

        /// <summary>
        /// Should be done before changing the cell properties.
        /// </summary>
        /// <param name="cells"></param>
        /// <param name="newValue">What will be the new value</param>
        /// <param name="newDuration">What will be the new duration</param>
        public CellDuration(Cell[] cells, string newValue) : base(cells[0].Row, cells.Length > 1 ? "Change Duration of Cells" : "Change Cell Duration")
        {
            Cells = cells;
            //PreviousDurations = Cells.Select(x => x.Duration).ToArray();
            PreviousValues = Cells.Select(x => x.Value).ToArray();
            NewValue = newValue;
            //NewDuration = newDuration;
            Row = cells[0].Row;
        }

        protected override void Transformation()
        {
            for (int i = 0; i < Cells.Length; i++)
            {
                Cells[i].Value = NewValue;
            }

            Cells = null;
        }

        public override void Undo()
        {
            base.Undo();

            EditorWindow.Instance.UpdateUiForSelectedCell();
        }

        public override void Redo()
        {
            base.Redo();

            EditorWindow.Instance.UpdateUiForSelectedCell();
        }

        //public void Undo()
        //{
        //    for (int i = 0; i < Cells.Length; i++)
        //    {
        //        Cells[i].Duration = PreviousDurations[i];
        //        Cells[i].Value = PreviousValues[i];
        //    }
        //
        //    //UpdateUI();
        //    EditorWindow.Instance.UpdateUiForSelectedCell();
        //
        //    // update referencers
        //    Row.BeatCodeIsCurrent = false;
        //    RedrawReferencers();
        //    EditorWindow.Instance.SetChangesApplied(false);
        //}
        //
        //public void Redo()
        //{
        //    foreach (Cell c in Cells)
        //    {
        //        c.Value = NewValue;
        //        c.Duration = NewDuration;
        //    }
        //
        //    //UpdateUI();
        //    EditorWindow.Instance.UpdateUiForSelectedCell();
        //
        //    // update referencers
        //    Row.BeatCodeIsCurrent = false;
        //    RedrawReferencers();
        //    EditorWindow.Instance.SetChangesApplied(false);
        //}
        //
        //protected void UpdateUI()
        //{
        //    if (Cell.SelectedCells.Cells.Count == 1 && Cells.Contains(Cell.SelectedCells.FirstCell))
        //    {
        //        EditorWindow.Instance.durationInput.Text = Cell.SelectedCells.FirstCell.Value;
        //    }
        //    else
        //    {
        //        EditorWindow.Instance.durationInput.Text = "";
        //    }
        //}
    }
}
