using System.Collections.Generic;
using System.Collections;
using System.Linq;


namespace Pronome
{
    /// <summary>
    /// A class for enumerating and describing the internal sound sources
    /// </summary>
    public class InternalSource : ISoundSource
    {
        public int Index { get; }

        public string Uri { get; }

        public string Label;

        public enum HiHatStatuses { None, Open, Closed };

        public HiHatStatuses HiHatStatus { get; }

        public bool IsPitch { get; set; }

        public InternalSource(int index, string uri, string label = "", HiHatStatuses hhStatus = HiHatStatuses.None)
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
            if (Label == "Pitch")
            {
                return "Pitch";
            }
            return (Index.ToString() + '.').PadRight(4) + Label;
        }

        /// <summary>
        /// Used for a default value or if a source cannot be found.
        /// </summary>
        /// <returns></returns>
        static public InternalSource GetDefault()
        {
            return new InternalSource(-1, "A4") { IsPitch = true };
        }

        /// <summary>
        /// Create a pitch stub
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        static public InternalSource GetPitch(string uri)
        {
            return new InternalSource(-1, uri) { IsPitch = true };
        }

        static public ISoundSource GetFromUri(string uri)
        {
            if (PitchStream.IsPitchSourceName(uri))
            {
                return GetPitch(uri);
            }
            if (uri.IndexOf("Pronome") == 0)
            {
                // internal wav
                return Library.Find(x => x.Uri == uri);
            }
            var custom = UserSource.Library.Where(x => x.Uri == uri).FirstOrDefault();
            return custom == null ? GetDefault() : (ISoundSource)custom;
        }

        public static List<InternalSource> Library = new List<InternalSource>()
        {
            //new InternalSource(-1, "A4", "Pitch") { IsPitch = true },
            new InternalSource(0, WavFileStream.SilentSourceName, "Silent"),
            new InternalSource(1, "Pronome.wav.crash1_edge_v5.wav", "Crash Edge V1"),
            new InternalSource(2, "Pronome.wav.crash1_edge_v8.wav", "Crash Edge V2"),
            new InternalSource(3, "Pronome.wav.crash1_edge_v10.wav", "Crash Edge V3"),
            new InternalSource(4, "Pronome.wav.floortom_v6.wav", "FloorTom V1"),
            new InternalSource(5, "Pronome.wav.floortom_v11.wav", "FloorTom V2"),
            new InternalSource(6, "Pronome.wav.floortom_v16.wav", "FloorTom V3"),
            new InternalSource(7, "Pronome.wav.hihat_closed_center_v4.wav", "HiHat Closed Center V1"),
            new InternalSource(8, "Pronome.wav.hihat_closed_center_v7.wav", "HiHat Closed Center V2"),
            new InternalSource(9, "Pronome.wav.hihat_closed_center_v10.wav", "HiHat Closed Center V3"),
            new InternalSource(10, "Pronome.wav.hihat_closed_edge_v7.wav", "HiHat Closed Edge V1"),
            new InternalSource(11, "Pronome.wav.hihat_closed_edge_v10.wav", "HiHat Closed Edge V2"),
            new InternalSource(12, "Pronome.wav.hihat_half_center_v4.wav", "HiHat Half Center V1", HiHatStatuses.Open),
            new InternalSource(13, "Pronome.wav.hihat_half_center_v7.wav", "HiHat Half Center V2", HiHatStatuses.Open),
            new InternalSource(14, "Pronome.wav.hihat_half_center_v10.wav", "HiHat Half Center V3", HiHatStatuses.Open),
            new InternalSource(15, "Pronome.wav.hihat_half_edge_v7.wav", "HiHat Half Edge V1", HiHatStatuses.Open),
            new InternalSource(16, "Pronome.wav.hihat_half_edge_v10.wav", "HiHat Half Edge V2", HiHatStatuses.Open),
            new InternalSource(17, "Pronome.wav.hihat_open_center_v4.wav", "HiHat Open Center V1", HiHatStatuses.Open),
            new InternalSource(18, "Pronome.wav.hihat_open_center_v7.wav", "HiHat Open Center V2", HiHatStatuses.Open),
            new InternalSource(19, "Pronome.wav.hihat_open_center_v10.wav", "HiHat Open Center V3", HiHatStatuses.Open),
            new InternalSource(20, "Pronome.wav.hihat_open_edge_v7.wav", "HiHat Open Edge V1", HiHatStatuses.Open),
            new InternalSource(21, "Pronome.wav.hihat_open_edge_v10.wav", "HiHat Open Edge V2", HiHatStatuses.Open),
            new InternalSource(22, "Pronome.wav.hihat_pedal_v3.wav", "HiHat Pedal V1", HiHatStatuses.Closed),
            new InternalSource(23, "Pronome.wav.hihat_pedal_v5.wav", "HiHat Pedal V2", HiHatStatuses.Closed),
            new InternalSource(24, "Pronome.wav.kick_v7.wav", "Kick Drum V1"),
            new InternalSource(25, "Pronome.wav.kick_v11.wav", "Kick Drum V2"),
            new InternalSource(26, "Pronome.wav.kick_v16.wav", "Kick Drum V3"),
            new InternalSource(27, "Pronome.wav.racktom_v6.wav", "RackTom V1"),
            new InternalSource(28, "Pronome.wav.racktom_v11.wav", "RackTom V2"),
            new InternalSource(29, "Pronome.wav.racktom_v16.wav", "RackTom V3"),
            new InternalSource(30, "Pronome.wav.ride_bell_v5.wav", "Ride Bell V1"),
            new InternalSource(31, "Pronome.wav.ride_bell_v8.wav", "Ride Bell V2"),
            new InternalSource(32, "Pronome.wav.ride_bell_v10.wav", "Ride Bell V3"),
            new InternalSource(33, "Pronome.wav.ride_center_v5.wav", "Ride Center V1"),
            new InternalSource(34, "Pronome.wav.ride_center_v6.wav", "Ride Center V2"),
            new InternalSource(35, "Pronome.wav.ride_center_v8.wav", "Ride Center V3"),
            new InternalSource(36, "Pronome.wav.ride_center_v10.wav", "Ride Center V4"),
            new InternalSource(37, "Pronome.wav.ride_edge_v4.wav", "Ride Edge V1"),
            new InternalSource(38, "Pronome.wav.ride_edge_v7.wav", "Ride Edge V2"),
            new InternalSource(39, "Pronome.wav.ride_edge_v10.wav", "Ride Edge V3"),
            new InternalSource(40, "Pronome.wav.snare_center_v6.wav", "Snare Center V1"),
            new InternalSource(41, "Pronome.wav.snare_center_v11.wav", "Snare Center V2"),
            new InternalSource(42, "Pronome.wav.snare_center_v16.wav", "Snare Center V3"),
            new InternalSource(43, "Pronome.wav.snare_edge_v6.wav", "Snare Edge V1"),
            new InternalSource(44, "Pronome.wav.snare_edge_v11.wav", "Snare Edge V2"),
            new InternalSource(45, "Pronome.wav.snare_edge_v16.wav", "Snare Edge V3"),
            new InternalSource(46, "Pronome.wav.snare_rim_v6.wav", "Snare Rim V1"),
            new InternalSource(47, "Pronome.wav.snare_rim_v11.wav", "Snare Rim V2"),
            new InternalSource(48, "Pronome.wav.snare_rim_v16.wav", "Snare Rim V3"),
            new InternalSource(49, "Pronome.wav.snare_xstick_v6.wav", "Snare XStick V1"),
            new InternalSource(50, "Pronome.wav.snare_xstick_v11.wav", "Snare XStick V2"),
            new InternalSource(51, "Pronome.wav.snare_xstick_v16.wav", "Snare XStick V3")
        };
    }

    public class CompleteSourceLibrary : IEnumerable<ISoundSource>
    {
        public IEnumerator<ISoundSource> GetEnumerator()
        {
            return GetAllSources().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerable<ISoundSource> GetAllSources()
        {
            return InternalSource.Library
                .Select(x => (ISoundSource)x)
                .Concat(UserSource.Library.Select(x => (ISoundSource)x));
        }
    }
}