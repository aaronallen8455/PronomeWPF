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
        public double Position;
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
            Rectangle = Editor.Instance.Resources["cellRectangle"] as Rectangle;
            // set Canvas.Top
            Canvas.SetTop(Rectangle,
                (double)Editor.Instance.Resources["rowHeight"] / 2 - (double)Editor.Instance.Resources["cellHeight"] / 2);
            // set position
            Canvas.SetLeft(Rectangle, Position * Editor.Instance.Scale);

            row.Canvas.Children.Add(Rectangle);
        }

        public struct CellRepeat
        {
            public int Times;
            public double LastTermModifier;
        }
    }
}
