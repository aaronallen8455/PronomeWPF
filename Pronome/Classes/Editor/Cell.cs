using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;

namespace Pronome.Editor
{
    class Cell
    {
        public Row Row;
        public Rectangle Rectangle;
        /// <summary>
        /// The cell's value in BPM
        /// </summary>
        public double Duration;
        protected double _position;
        /// <summary>
        /// The horizontal position of the cell in BPM. Changes actual position when set.
        /// </summary>
        public double Position
        {
            get => _position;
            set
            {
                _position = value;
                Canvas.SetLeft(Rectangle, Position * EditorWindow.Scale * EditorWindow.BaseFactor);
            }
        }
        public bool IsSelected = false;

        /// <summary>
        /// Whether cell is a break point for a loop - |
        /// </summary>
        public bool IsBreak = false;

        /// <summary>
        /// The index of the layer that this cell is a reference for. Null if it's a regular cell.
        /// </summary>
        public string Reference;
        public CellRepeat Repeat;
        public string Source;
        public LinkedList<MultGroup> MultGroups = new LinkedList<MultGroup>();
        public LinkedList<RepeatGroup> RepeatGroups = new LinkedList<RepeatGroup>();
        /// <summary>
        /// Is this cell part of a reference. Should not be manipulable if so
        /// </summary>
        public bool IsReference = false;

        public Cell(Row row)
        {
            Row = row;
            Rectangle = EditorWindow.Instance.Resources["cellRectangle"] as Rectangle;
            Rectangle.Height = (double)EditorWindow.Instance.Resources["cellHeight"];
            // set Canvas.Top
            Canvas.SetTop(Rectangle,
                (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - (double)EditorWindow.Instance.Resources["cellHeight"] / 2);
            Panel.SetZIndex(Rectangle, 10);
            Rectangle.MouseDown += Rectangle_MouseDown; ;
        }

        private void Rectangle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ToggleSelect();
        }

        private void ToggleSelect(bool Clicked = true)
        {
            IsSelected = !IsSelected;
            Rectangle.Stroke = IsSelected ? System.Windows.Media.Brushes.DeepPink : System.Windows.Media.Brushes.Black;
            // if not a multi select, deselect currently selected cell(s)
            if (IsSelected)
            {
                if (Clicked)
                {
                    // multiSelect
                    if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        foreach (Cell c in Row.Cells
                            .SkipWhile(x => !x.IsSelected)
                            .SkipWhile(x => x.IsSelected)
                            .TakeWhile(x => !x.IsSelected))
                        {
                            c.ToggleSelect(false);
                        }
                    }
                    else
                    { // single select - deselect others
                        while (Row.SelectedCells.Any())
                        {
                            Row.SelectedCells.First().ToggleSelect();
                        }
                    }
                }

                Row.SelectedCells.Add(this);
            }
            else if (!IsSelected)
            { // deselect if not a cell in a multi-select being clicked
                if (Row.SelectedCells.Count > 1 && Clicked)
                {
                    //ToggleSelect(false);
                    foreach (Cell c in Row.SelectedCells.ToArray())
                    {
                        c.ToggleSelect(false);
                    }
                }

                Row.SelectedCells.Remove(this);
            }
        }

        public struct CellRepeat
        {
            public int Times;
            public double LastTermModifier;
        }
    }
}
