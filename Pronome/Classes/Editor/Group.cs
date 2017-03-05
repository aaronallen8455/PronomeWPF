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
    abstract class Group
    {
        public Row Row;
        public List<Cell> Cells;
        protected double _position;
        /// <summary>
        /// The left offset of the group in BPM. Setting will adjust rect position.
        /// </summary>
        public double Position
        {
            get => _position;
            set
            {
                _position = value;
                Canvas.SetLeft(Rectangle, value * Editor.Scale * Editor.BaseFactor);
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
                Rectangle.Width = value * Editor.Scale * Editor.BaseFactor;
            }
        }
        public Rectangle Rectangle;

        public void RefreshRectParams()
        {
            Rectangle.Width = Duration * Editor.Scale * Editor.BaseFactor;
            Canvas.SetLeft(Rectangle, Position * Editor.Scale * Editor.BaseFactor);
        }
    }

    class MultGroup : Group
    {
        public double Factor;

        public MultGroup()
        {
            Rectangle = Editor.Instance.Resources["multGroupRectangle"] as Rectangle;
            Rectangle.Fill = Brushes.Orange;
        }
    }

    class RepeatGroup : Group
    {
        /// <summary>
        /// Number of times to repeat
        /// </summary>
        public int Times;

        /// <summary>
        /// The modifier on last term, ex. (#)2+1/3
        /// </summary>
        public double LastTermModifier;

        public RepeatGroup()
        {
            Rectangle.Fill = Brushes.ForestGreen;
        }
    }
}
