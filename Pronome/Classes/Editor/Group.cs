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

        /// <summary>
        /// The cells belonging to this group as well as any nested groups
        /// </summary>
        public LinkedList<Cell> Cells = new LinkedList<Cell>();

        /// <summary>
        /// The cells belonging to this group. Does not include cells from nested groups, only the direct members.
        /// </summary>
        public LinkedList<Cell> ExclusiveCells = new LinkedList<Cell>();

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

        /// <summary>
        /// The canvas in which this group's canvas is being hosted. Also hosts the the HostRects rectangles with dupe content.
        /// </summary>
        public Canvas HostCanvas;

        public void RefreshRectParams()
        {
            Rectangle.Width = Duration * EditorWindow.Scale * EditorWindow.BaseFactor;
            Canvas.SetLeft(Rectangle, Position * EditorWindow.Scale * EditorWindow.BaseFactor);
        }

        virtual protected void SetRectPosition(double value)
        {
            Canvas.SetLeft(Rectangle, value * EditorWindow.Scale * EditorWindow.BaseFactor);
        }

        abstract public void RemoveGroupFromRow();

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
                            // because the size of the host rects does not include the duration of a cell, just the rectangle elements.
                            if (rg.BreakCell == null)
                            {
                                // if there's a break cell, we don't resize
                                foreach (Rectangle rect in rg.HostRects)
                                {
                                    rect.Width = rect.Width + (rg.Cells.Last.Value.Duration * EditorWindow.Scale * EditorWindow.BaseFactor);
                                }
                            }
                            rg.Cells.AddLast(cell);
                        }
                    }
                }
            }
            else
            {
                // no repeat group, add to base canvas
                return false;
            }

            if (cell.RepeatGroups.Any())
            {
                // add the group actions to the cell if applicable
                foreach (var tuple in below.GroupActions.Where(x => !x.Item1 && x.Item2.Cells.Contains(cell)).ToList())
                {
                    below.GroupActions.Remove(tuple);
                    cell.GroupActions.AddLast(tuple);
                }

                return true;
            }

            return false;
        }
    }

    public class MultGroup : Group
    {
        /// <summary>
        /// The factor to multiply by, ex 1+1/3.
        /// </summary>
        public string FactorValue;

        /// <summary>
        /// The numeric value of the factor.
        /// </summary>
        public double Factor;

        public MultGroup()
        {
            Rectangle.Style = EditorWindow.Instance.Resources["multRectStyle"] as System.Windows.Style;
            Canvas.SetTop(Rectangle, (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - ((double)EditorWindow.Instance.Resources["cellHeight"] / 2));
            Panel.SetZIndex(Rectangle, 5);
        }

        override public void RemoveGroupFromRow()
        {
            Row.MultGroups.Remove(this);
            HostCanvas.Children.Remove(Rectangle);
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

        /// <summary>
        /// Aggregated mult factors from all parent mult groups
        /// </summary>
        public string MultFactor;

        /// <summary>
        /// If this rep group has a break cell, this is a ref to it. Otherwise, it's null.
        /// </summary>
        public Cell BreakCell = null;

        /// <summary>
        /// The duration expanded to include rep times and break point. Does not include the LTM
        /// </summary>
        public double FullDuration;

        protected string MultedLtm;

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

        public override void RemoveGroupFromRow()
        {
            Row.RepeatGroups.Remove(this);
            Row.Canvas.Children.Remove(Rectangle);
            HostCanvas.Children.Remove(Canvas);
            foreach (Rectangle rect in HostRects)
            {
                HostCanvas.Children.Remove(rect);
            }
        }

        /// <summary>
        /// Gets the ltm with mult factor.
        /// </summary>
        /// <returns>The ltm with mult factor.</returns>
        public string GetLtmWithMultFactor(bool ignoreScaleSetting = false)
        {
            if (string.IsNullOrEmpty(LastTermModifier) || (!ignoreScaleSetting && !UserSettings.GetSettings().DrawMultToScale))
            {
                return LastTermModifier;
            }

            if (string.IsNullOrEmpty(MultedLtm))
            {
                MultedLtm = BeatCell.MultiplyTerms(LastTermModifier, MultFactor);
            }

            return MultedLtm;
        }

        public string GetValueDividedByMultFactor(string value, bool ignoreScaleSetting = false)
        {
            if (string.IsNullOrEmpty(value) || (!ignoreScaleSetting && !UserSettings.GetSettings().DrawMultToScale))
            {
                return value;
            }

            return BeatCell.DivideTerms(value, MultFactor);
        }

        /// <summary>
        /// Resets the multed ltm, so that it will be recalculated against a new value.
        /// </summary>
        public void ResetMultedLtm()
        {
            MultedLtm = string.Empty;
        }

        /// <summary>
        /// Produce a deep copy of this group and it's components.
        /// </summary>
        /// <returns>The copy.</returns>
        public Group DeepCopy()
        {
            return DeepCopy(this);
        }

        /// <summary>
        /// Produce a deep copy of a group and it's components
        /// </summary>
        /// <returns>The copy.</returns>
        /// <param name="group">Group.</param>
        public static Group DeepCopy(Group group)
        {
            Group copy = null;

            if (group is RepeatGroup)
            {
                var r = group as RepeatGroup;

                copy = new RepeatGroup()
                {
                    LastTermModifier = r.LastTermModifier,
                    Duration = r.Duration,
                    MultFactor = r.MultFactor,
                    Position = r.Position,
                    Row = r.Row,
                    Times = r.Times
                };
            }
            else if (group is MultGroup)
            {
                var m = group as MultGroup;

                copy = new MultGroup()
                {
                    Factor = m.Factor,
                    FactorValue = m.FactorValue,
                    Duration = m.Duration,
                    Position = m.Position,
                    Row = m.Row
                };
            }

            Dictionary<Group, Group> copiedGroups = new Dictionary<Group, Group>();

            bool first = true;
            foreach (Cell c in group.Cells)
            {
                IEnumerable<(bool, Group)> nested = c.GroupActions;

                // the first cell, we skip any opening groups up to and including the target
                if (first)
                {
                    nested = nested.SkipWhile(x => x.Item2 != group).Skip(1);

                    first = false;
                }

                // only clone a group that is opening
                nested = nested.SkipWhile(x => !x.Item1);
                if (nested.Any())
                {
                    (bool _, Group g) = nested.First();
                    Group nestedCopy = DeepCopy(g);

                    copy.Cells = new LinkedList<Cell>(copy.Cells.Concat(nestedCopy.Cells));
                }
                else
                {
                    Cell copyCell = new Cell(group.Row)
                    {
                        Duration = c.Duration,
                        MultFactor = c.MultFactor,
                        Position = c.Position,
                        Source = c.Source,
                        Value = c.Value,
                        Reference = c.Reference,
                        IsBreak = c.IsBreak,
                    };

                    // add the group to the cell's collection
                    if (copy is RepeatGroup)
                    {
                        copyCell.RepeatGroups.AddLast(copy as RepeatGroup);
                    }
                    else if (copy is MultGroup)
                    {
                        copyCell.MultGroups.AddLast(copy as MultGroup);
                    }

                    copy.ExclusiveCells.AddLast(copyCell);
                    copy.Cells.AddLast(copyCell);
                }
            }

            copy.ExclusiveCells.First.Value.GroupActions.AddFirst((true, copy));
            copy.ExclusiveCells.Last.Value.GroupActions.AddLast((false, copy));

            return copy;
        }
    }
}
