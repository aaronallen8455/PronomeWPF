using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace Pronome.Editor
{
    class Cell
    {
        public Row Row;
        public Rectangle Rectangle;
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

        public Cell(Row row)
        {
            Row = row;
            Rectangle = EditorWindow.Instance.Resources["cellRectangle"] as Rectangle;
            Rectangle.Height = (double)EditorWindow.Instance.Resources["cellHeight"];
            // set Canvas.Top
            Canvas.SetTop(Rectangle,
                (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - (double)EditorWindow.Instance.Resources["cellHeight"] / 2);
            Panel.SetZIndex(Rectangle, 10);
            Rectangle.MouseDown += Rectangle_MouseDown;
        }

        private void Rectangle_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            IsSelected = !IsSelected;
            Rectangle.Stroke = IsSelected ? System.Windows.Media.Brushes.DeepPink : System.Windows.Media.Brushes.Black;
        }

        public struct CellRepeat
        {
            public int Times;
            public double LastTermModifier;
        }
    }
}
