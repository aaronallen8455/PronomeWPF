using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pronome.Editor
{
    abstract class Group
    {
        public Row Row;
        public List<Cell> Cells;
    }

    class MultGroup : Group
    {
        public double Factor;
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
    }
}
