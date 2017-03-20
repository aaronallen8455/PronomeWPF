using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Controls;

namespace Pronome.Editor
{
    public abstract class Group
    {
        public Row Row;
        public LinkedList<Cell> Cells = new LinkedList<Cell>();
        protected double _position;
        /// <summary>
        /// The left offset of the group in BPM. Setting will adjust rect position.
        /// </summary>
        public double Position
        {
            get => _position;
            set
            {
                SetRectPosition(value);
                _position = value;
            }
        }

        protected double _duration;
        /// <summary>
        /// The duration of the group in BPM. Setting will adjust rect size.
        /// </summary>
        public double Duration
        {
            get => _duration;
            set
            {
                _duration = value;
                Rectangle.Width = value * EditorWindow.Scale * EditorWindow.BaseFactor;
            }
        }
        public Rectangle Rectangle = new Rectangle();// = EditorWindow.Instance.Resources["groupRectangle"] as Rectangle;

        public void RefreshRectParams()
        {
            Rectangle.Width = Duration * EditorWindow.Scale * EditorWindow.BaseFactor;
            Canvas.SetLeft(Rectangle, Position * EditorWindow.Scale * EditorWindow.BaseFactor);
        }

        virtual protected void SetRectPosition(double value)
        {
            Canvas.SetLeft(Rectangle, value * EditorWindow.Scale * EditorWindow.BaseFactor);
        }

        /// <summary>
        /// Determine if the cell in part of any repeat or mult groups based on the it's lower neighbor cell. If it's part of repeat group, will return true, otherwise return false.
        /// </summary>
        /// <param name="cell">The cell to be tested</param>
        /// <param name="below">The cell's below neighbor</param>
        /// <returns></returns>
        static public bool AddToGroups(Cell cell, Cell below)
        {
            // does the cell belong in a group of either kind?
            if (below.MultGroups.Any())
            {
                foreach (MultGroup mg in below.MultGroups)
                {
                    if (cell.Position < mg.Position + mg.Duration)
                    {
                        cell.MultGroups.AddLast(mg);
                        mg.Cells.AddLast(cell);
                    }
                    else break;
                }
            }
            if (below.RepeatGroups.Any())
            {
                //cell.RepeatGroups = new LinkedList<RepeatGroup>(below.RepeatGroups);
                foreach (RepeatGroup rg in below.RepeatGroups)
                {
                    if (cell.Position < rg.Position + rg.Duration)
                    {
                        cell.RepeatGroups.AddLast(rg);
                        //rg.Cells.AddLast(cell);
                        int count = 0;
                        foreach (Cell c in rg.Cells)
                        {
                            // add in position if it won't be last
                            if (c.Position > cell.Position)
                            {
                                rg.Cells.AddBefore(rg.Cells.Find(c), cell);
                                break;
                            }
                            count++;
                        }
                        if (count == rg.Cells.Count)
                        {
                            // need to resize the host rects because the cell we're adding is the last in the rep group
                            foreach (Rectangle rect in rg.HostRects)
                            {
                                rect.Width = rect.Width + (rg.Cells.Last.Value.Duration * EditorWindow.Scale * EditorWindow.BaseFactor);
                            }
                            rg.Cells.AddLast(cell);
                        }
                    }
                    else break;
                }
            }
            else
            {
                // no repeat group, add to base canvas
                return false;
            }

            if (cell.RepeatGroups.Any())
            {
                return true;
            }

            return false;
        }
    }

    public class MultGroup : Group
    {
        /// <summary>
        /// The factor to multiply by. ex 1+1/3
        /// </summary>
        public string Factor;

        public MultGroup()
        {
            Rectangle.Style = EditorWindow.Instance.Resources["multRectStyle"] as System.Windows.Style;
            Panel.SetZIndex(Rectangle, 5);
        }
    }

    public class RepeatGroup : Group
    {
        /// <summary>
        /// Number of times to repeat
        /// </summary>
        public int Times;

        /// <summary>
        /// The modifier on last term, ex. (#)2+1/3
        /// </summary>
        public string LastTermModifier;

        /// <summary>
        /// Holds the cell's within this group for easy duplication. Does not have a left offset
        /// </summary>
        public Canvas Canvas = new Canvas();

        /// <summary>
        /// Holds the rects used to display the duplicated cells
        /// </summary>
        public LinkedList<Rectangle> HostRects = new LinkedList<Rectangle>();

        public RepeatGroup()
        {
            Rectangle.Style = EditorWindow.Instance.Resources["repeatRectStyle"] as System.Windows.Style;
            Panel.SetZIndex(Rectangle, 5);
            Panel.SetZIndex(Canvas, 10);
        }

        protected override void SetRectPosition(double value)
        {
            // Reposition the host rects
            double n = value * EditorWindow.Scale * EditorWindow.BaseFactor;
            double o = Position * EditorWindow.Scale * EditorWindow.BaseFactor;
            double diff = n - o;

            foreach (Rectangle rect in HostRects)
            {
                double left = Canvas.GetLeft(rect);
                Canvas.SetLeft(rect, left + diff);
            }

            base.SetRectPosition(value);
        }
    }
}
