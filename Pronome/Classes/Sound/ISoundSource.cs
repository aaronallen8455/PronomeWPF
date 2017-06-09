using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pronome
{
    /// <summary>
    /// Provides information about a sound source.
    /// </summary>
    public interface ISoundSource : IEquatable<ISoundSource>
    {
        /// <summary>
        /// Used to signify the source material. could be an audio file or a pitch symbol.
        /// </summary>
        string Uri { get; }

        /// <summary>
        /// Signifys if this sound has special hihat status such as open or down.
        /// </summary>
        InternalSource.HiHatStatuses HiHatStatus { get; }

        /// <summary>
        /// True if this source is a pitch and the Uri will be a note symbol
        /// </summary>
        bool IsPitch { get; }

        string Label { get; }
    }
}
