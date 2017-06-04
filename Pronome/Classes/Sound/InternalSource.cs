using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pronome
{
    /// <summary>
    /// A class for enumerating the internal sound sources
    /// </summary>
    public class InternalSource
    {
        public int Index { get; }

        public string Uri { get; }

        public string Label;

        public enum HiHatStatuses { None, Open, Closed };

        public HiHatStatuses HiHatStatus = HiHatStatuses.None;

        public InternalSource(int index, string uri, string label, HiHatStatuses hhStatus)
        {
            Index = index;
            Uri = uri;
            Label = label;
            HiHatStatus = hhStatus;
        }

        /// <summary>
        /// Get the string representation used by source selector dropdowns.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return (Index.ToString() + '.').PadRight(4) + Label;
        }

        public static List<InternalSource> Library = new List<InternalSource>()
        {

        }
    }
}
