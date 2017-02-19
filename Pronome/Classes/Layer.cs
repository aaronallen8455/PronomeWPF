using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

namespace Pronome
{
    /** <summary>A layer representing a rhythmic pattern within the complete beat.</summary> */
    [DataContract]
    public class Layer : IDisposable
    {
        /** <summary>The individual beat cells contained by this layer.</summary> */
        public List<BeatCell> Beat;

        /** <summary>The audio sources that are not pitch are the base sound.</summary> */
        public Dictionary<string, IStreamProvider> AudioSources = new Dictionary<string, IStreamProvider>();

        /** <summary>The base audio source. Could be a pitch or wav file source.</summary> */
        public IStreamProvider BaseAudioSource;

        /** <summary>If this layer has any pitch sounds, they are held here.</summary> */
        public PitchStream BasePitchSource; // only use one pitch source per layer

        /** <summary>True if the base source is a pitch.</summary> */
        [DataMember]
        public bool IsPitch;

        /** <summary>The beat code string that was passed in to create the rhythm of this layer.</summary> */
        [DataMember]
        public string ParsedString;

        /** <summary>The fractional portion of sample per second values are accumulated here and added in when over 1.</summary> */
        [DataMember]
        public double Remainder = .0; // holds the accumulating fractional milliseconds.

        /** <summary>A value in quarter notes that all sounds in this layer are offset by.</summary> */
        public double Offset = 0; // in BPM

        /**<summary>The string that was parsed to get the offset value.</summary>*/
        [DataMember]
        public string ParsedOffset = "0";

        /** <summary>The name of the base source.</summary> */
        [DataMember]
        public string BaseSourceName;

        /** <summary>True if the layer 
         * is muted.</summary> */
        public bool IsMuted = false;

        /** <summary>True if the layer is part of the soloed group.</summary> */
        public bool IsSoloed = false;

        /** <summary>True if a solo group exists.</summary> */
        public static bool SoloGroupEngaged = false; // is there a solo group?

        /** <summary>Does the layer contain a hihat closed source?</summary> */
        public bool HasHiHatClosed = false;

        /** <summary>Does the layer contain a hihat open source?</summary> */
        public bool HasHiHatOpen = false;

        [DataMember]
        protected double volume;
        /** <summary>Set the volume of all sound sources in this layer.</summary> */
        public double Volume
        {
            get { return volume; }
            set
            {
                volume = value;
                if (AudioSources != null)
                    foreach (IStreamProvider src in AudioSources.Values) src.Volume = value * Metronome.GetInstance().Volume;
                if (BasePitchSource != null) BasePitchSource.Volume = value * Metronome.GetInstance().Volume;
            }
        }

        [DataMember]
        protected float pan;
        /** <summary>Set the pan value for all sound sources in this layer.</summary> */
        public float Pan
        {
            get
            {
                return pan;
            }
            set
            {
                pan = value;
                foreach (IStreamProvider src in AudioSources.Values) src.Pan = value;
                if (BasePitchSource != null) BasePitchSource.Pan = value;
            }
        }

        /** <summary>Layer constructor</summary>
         * <param name="baseSourceName">Name of the base sound source.</param>
         * <param name="beat">Beat code.</param>
         * <param name="offset">Amount of offset</param>
         * <param name="pan">Set the pan</param>
         * <param name="volume">Set the volume</param> */
        public Layer(string beat, string baseSourceName = null, string offset = "", float pan = 0f, float volume = 1f)
        {
            if (baseSourceName == null) // auto generate a pitch if no source is specified
            {
                SetBaseSource(GetAutoPitch());
            }
            else
                SetBaseSource(baseSourceName);

            if (offset != "")
                SetOffset(offset);
            Parse(beat); // parse the beat code into this layer
            Volume = volume;
            if (pan != 0f)
                Pan = pan;
            Metronome.GetInstance().AddLayer(this);
        }

        /** <summary>Parse the beat code, generating beat cells.</summary>
         * <param name="beat">Beat code.</param> 
         * <param name="parsedReferencers">Indexes of referencer layers that have been parsed already.</param>
         */
        public void Parse(string beat, HashSet<int> parsedReferencers = null)
        {
            ParsedString = beat;
            // remove comments
            beat = Regex.Replace(beat, @"!.*?!", "");
            // remove whitespace
            beat = Regex.Replace(beat, @"\s", "");

            //string pitchModifier = @"@[a-gA-G]?[#b]?[pP]?[1-9.]+";

            if (beat.Contains('$'))
            {
                // prep single cell repeat on ref if exists
                beat = Regex.Replace(beat, @"(\$[\ds]+)(\(\d\))", "[$1]$2");
                
                //resolve beat referencing
                while (beat.Contains('$'))
                {
                    var match = Regex.Match(beat, @"\$(\d+|s)");
                    string indexString = match.Groups[1].Value;
                    int refIndex;
                    int selfIndex = Metronome.GetInstance().Layers.IndexOf(this);
                    if (indexString == "s") refIndex = selfIndex;
                    else refIndex = int.Parse(indexString) - 1;
                    // perform the replacement
                    beat = beat.Substring(0, match.Index) + 
                        ResolveReferences(refIndex, new HashSet<int>(new int[] { selfIndex })) + 
                        beat.Substring(match.Index + match.Length);
                }
            }
            
            // allow 'x' to be multiply operator
            beat = beat.Replace('x', '*');
            beat = beat.Replace('X', '*');

            // handle group multiply
            while (beat.Contains('{'))
            {
                var match = Regex.Match(beat, @"\{([^}]*)}([^,\]]+)"); // match the inside and the factor
                // insert the multiplication
                string inner = Regex.Replace(match.Groups[1].Value, @"(?<!\]\d*)(?=([\]\(\|,+-]|$))", "*" + match.Groups[2].Value);
                // switch the multiplier to be in front of pitch modifiers
                inner = Regex.Replace(inner, @"(@[a-gA-GpP]?[#b]?[\d.]+)(\*[\d.*/]+)", "$2$1");
                // insert into beat
                beat = beat.Substring(0, match.Index) + inner + beat.Substring(match.Index + match.Length);
                // get rid of errant multiplier on closing parantheses
                //beat = Regex.Replace(beat, @"\)\*[\d./\-+*]+", ")");
            }

            // handle single cell repeats
            while (Regex.IsMatch(beat, @"[^\]]\(\d+\)"))
            {
                var match = Regex.Match(beat, @"([.\d+\-/*]+@?[a-gA-G]?[#b]?[Pp]?\d*)\((\d+)\)([\d\-+/*.]*)");
                StringBuilder result = new StringBuilder(beat.Substring(0, match.Index));
                for (int i = 0; i < int.Parse(match.Groups[2].Value); i++)
                {
                    result.Append(match.Groups[1].Value);
                    // add comma or last term modifier
                    if (i == int.Parse(match.Groups[2].Value) - 1)
                    {
                        result.Append("+0").Append(match.Groups[3].Value);
                    }
                    else result.Append(",");
                }
                // insert into beat
                beat = result.Append(beat.Substring(match.Index + match.Length)).ToString();
            }

            // handle multi-cell repeats
            while (beat.Contains('['))
            {
                var match = Regex.Match(beat, @"\[([^\][]+?)\]\(?(\d+)\)?([\d\-+/*.]*)");
                StringBuilder result = new StringBuilder();
                int itr = int.Parse(match.Groups[2].Value);
                for (int i = 0; i < itr; i++)
                {
                    // if theres a last time exit point, only copy up to that
                    if (i == itr - 1 && match.Value.Contains('|'))
                    {
                        result.Append(match.Groups[1].Value.Substring(0, match.Groups[1].Value.IndexOf('|')));
                    }
                    else result.Append(match.Groups[1].Value); // copy the group

                    if (i == itr - 1)
                    {
                        result.Append("+0").Append(match.Groups[3].Value);
                    }
                    else result.Append(",");
                }
                result.Replace('|', ',');
                beat = beat.Substring(0, match.Index) + result.Append(beat.Substring(match.Index + match.Length)).ToString();
            }

            // fix instances of a pitch modifier being following by +0 from repeater
            beat = Regex.Replace(beat, $@"(@[a-gA-G]?[#b]?[pP]?[0-9.]+)(\+[\d.\-+/*]+)", "$2$1");

            if (beat != string.Empty)
            {
                BeatCell[] cells = beat.Split(',').Select((x) =>
                {
                    var match = Regex.Match(x, @"([\d.+\-/*]+)@?(.*)");
                    string source = match.Groups[2].Value;

                    //if (Regex.IsMatch(source, @"^[a-gA-G][#b]?\d{1,2}$|^[pP][\d.]+$"))
                    if (!Regex.IsMatch(source, @"^\d{1,2}$"))
                    {
                        // is a pitch reference
                        return new BeatCell(match.Groups[1].Value, source);
                    }
                    else // ref is a plain number (wav source) or "" base source.
                    {
                        return new BeatCell(match.Groups[1].Value, source != "" ? WavFileStream.FileNameIndex[int.Parse(source), 0] : "");
                    }

                }).ToArray();

                SetBeat(cells);

                // reparse any layers that reference this one
                Metronome met = Metronome.GetInstance();
                int index = met.Layers.IndexOf(this);
                if (parsedReferencers == null)
                {
                    parsedReferencers = new HashSet<int>();
                }
                parsedReferencers.Add(index);
                var layers = met.Layers.Where(
                    x => x != this 
                    && x.ParsedString.Contains($"${index + 1}") 
                    && !parsedReferencers.Contains(met.Layers.IndexOf(x)));
                foreach (Layer layer in layers)
                {
                    // account for deserializing a beat
                    if (layer.Beat != null && layer.Beat.Count > 0)
                    {
                        layer.Parse(layer.ParsedString, parsedReferencers);
                    }
                }
            }
        }

        /// <summary>
        /// Recursively build a beat string based on the reference index.
        /// </summary>
        /// <param name="reference">Referenced beat index</param>
        /// <param name="visitedIndexes">Holds the previously visited beats</param>
        /// <returns>The replacement string</returns>
        protected string ResolveReferences(int reference, HashSet<int> visitedIndexes = null)
        {
            Metronome met = Metronome.GetInstance();

            if (visitedIndexes == null) visitedIndexes = new HashSet<int>();

            if (reference >= met.Layers.Count || reference < 0) reference = 0;

            string refString = met.Layers[reference].ParsedString;
            // remove comments
            refString = Regex.Replace(refString, @"!.*?!", "");
            // remove whitespace
            refString = Regex.Replace(refString, @"\s", "");
            // remove source modifiers if not @0
            //refString = Regex.Replace(refString, @"@[a-gA-G]?[#b]?[pP]?[1-9.]+", "");
            // prep single cell repeats
            refString = Regex.Replace(refString, @"(\$[\ds]+)(\(\d\))", "[$1]$2");

            if (refString.IndexOf('$') > -1 && visitedIndexes.Contains(reference))
            {
                // strip references and their inner nests
                while (refString.Contains('$'))
                {
                    if (Regex.IsMatch(refString, @"[[{][^[{\]}]*\$[^[{\]}]*[\]}][^\]},]*"))
                        refString = Regex.Replace(refString, @"[[{][^[{\]}]*\$[^[{\]}]*[\]}][^\]},]*", "$s");
                    else
                        refString = Regex.Replace(refString, @"\$[\ds]+,?", ""); // straight up replace
                }
                // clean out empty cells
                refString = Regex.Replace(refString, @",,", ",");
                //refBeat = Regex.Replace(refBeat, @",$", "");
                refString = refString.Trim(',');
            }
            else
            {
                // recurse over references of the reference
                visitedIndexes.Add(reference);

                while (refString.IndexOf('$') > -1)
                {
                    int refIndex;
                    var match = Regex.Match(refString, @"\$(\d+|s)");
                    string embedIndex = match.Groups[1].Value;
                    if (embedIndex == "s")
                    {
                        refIndex = reference;
                    }
                    else
                    {
                        refIndex = int.Parse(embedIndex) - 1;
                    }

                    refString = refString.Substring(0, match.Index) +
                        ResolveReferences(refIndex, new HashSet<int>(visitedIndexes)) +
                        refString.Substring(match.Index + match.Length);
                }
            }

            return refString;
        }

        /**<summary>Apply a new base source to the layer.</summary>*/
        public void NewBaseSource(string baseSourceName)
        {
            if (BaseAudioSource != null && Beat != null)
            {
                Metronome.GetInstance().RemoveAudioSource(BaseAudioSource);

                // remove old wav base source from AudioSources
                if (AudioSources.Keys.Contains(""))
                {
                    AudioSources[""].Dispose();
                    AudioSources.Remove("");
                }
                BasePitchSource = null;

                IStreamProvider newBaseSource = null;

                var met = Metronome.GetInstance();
                // is new source a pitch or a wav?
                if (Regex.IsMatch(baseSourceName, @"^[A-Ga-g][#b]?\d+$|^[\d.]+$"))
                {
                    // Pitch
                    
                    PitchStream newSource = new PitchStream()
                    {
                        BaseFrequency = PitchStream.ConvertFromSymbol(baseSourceName),
                        Layer = this,
                        Volume = Volume * met.Volume,
                        Pan = Pan
                    };

                    if (IsPitch)
                    {
                        // we can resuse the old beat collection
                        BaseAudioSource.BeatCollection.isWav = false;
                        newSource.BeatCollection = BaseAudioSource.BeatCollection;

                        foreach (BeatCell bc in Beat.Where(x => x.AudioSource == BaseAudioSource))
                        {
                            if (bc.SourceName == "")
                                newSource.AddFrequency(baseSourceName, bc);
                            else
                                newSource.AddFrequency(bc.SourceName, bc);

                            bc.AudioSource = newSource;
                        }
                    }
                    else
                    {
                        // old base was a wav, we need to rebuild the beatcollection
                        List<double> beats = new List<double>();
                        double accumulator = 0;
                        int indexOfFirst = Beat.FindIndex(x => x.AudioSource.IsPitch || x.SourceName == "");
                        
                        for (int i=0; i<Beat.Count; i++)
                        {
                            int index = indexOfFirst + i;
                            if (index >= Beat.Count) index -= Beat.Count;
                            if (Beat[index].AudioSource.IsPitch || Beat[index].SourceName == "")
                            {
                                Beat[index].AudioSource = newSource;
                                newSource.AddFrequency(Beat[index].SourceName == "" ? baseSourceName : Beat[index].SourceName, Beat[index]);
                                if (i > 0)
                                {
                                    beats.Add(accumulator);
                                    accumulator = 0;
                                }
                            }
                            accumulator += Beat[index].Bpm;
                        }
                        beats.Add(accumulator);
                        var sbc = new SourceBeatCollection(this, beats.ToArray(), newSource);
                        newSource.BeatCollection = sbc;
                    }

                    // Done
                    newBaseSource = newSource;
                    BasePitchSource = newSource;
                    IsPitch = true;
                }
                else
                {
                    // Wav
                    WavFileStream newSource = new WavFileStream(baseSourceName)
                    {
                        Layer = this,
                        Volume = Volume * met.Volume,
                        Pan = Pan
                    };

                    foreach (BeatCell bc in Beat.Where(x => x.SourceName == ""))
                    {
                        bc.AudioSource = newSource;
                    }
                    // if this was formerly a pitch layer, we'll need to rebuild the pitch source, freq enumerator and beatCollection
                    // this is because of cells that have a pitch modifier - not using base pitch
                    if (IsPitch && !Beat.All(x => x.SourceName == ""))
                    {
                        // see if we need to make a new pitch source
                        int indexOfFirstPitch = Beat.FindIndex(x => x.AudioSource.IsPitch);
                        List<double> beats = new List<double>();
                        double accumulator = 0;

                        if (indexOfFirstPitch > -1)
                        {
                            // build the new pitch source
                            var newPitchSource = new PitchStream()
                            {
                                Layer = this,
                                Volume = Volume * met.Volume,
                                Pan = Pan
                            };

                            // build its Beat collection and freq enum
                            for (int i = 0; i < Beat.Count; i++)
                            {
                                int index = indexOfFirstPitch + i;
                                if (index >= Beat.Count) index -= Beat.Count;
                                if (Beat[index].AudioSource.IsPitch)
                                {
                                    Beat[index].AudioSource = newPitchSource;
                                    newPitchSource.AddFrequency(Beat[index].SourceName, Beat[index]);
                                    if (i > 0)
                                    {
                                        beats.Add(accumulator);
                                        accumulator = 0;
                                    }
                                }
                                accumulator += Beat[index].Bpm;
                            }
                            beats.Add(accumulator);
                            var sbc = new SourceBeatCollection(this, beats.ToArray(), newPitchSource);
                            newPitchSource.BeatCollection = sbc;
                            BasePitchSource = newPitchSource;
                            // get the offset
                            double pOffset = Beat.TakeWhile(x => x.AudioSource != BasePitchSource).Select(x => x.Bpm).Sum() + Offset;
                            pOffset = BeatCell.ConvertFromBpm(pOffset, BasePitchSource);
                            BasePitchSource.SetOffset(pOffset);
                            Metronome.GetInstance().AddAudioSource(BasePitchSource);
                        }

                        // build the beatcollection for the new wav base source.
                        beats.Clear();
                        accumulator = 0;
                        int indexOfFirst = Beat.FindIndex(x => x.SourceName == "");
                        for (int i=0; i<Beat.Count; i++)
                        {
                            int index = indexOfFirst + i;
                            if (index >= Beat.Count) index -= Beat.Count;
                            if (Beat[index].SourceName == "" && i > 0)
                            {
                                beats.Add(accumulator);
                                accumulator = 0;
                            }
                            accumulator += Beat[index].Bpm;
                        }
                        beats.Add(accumulator);
                        var baseSbc = new SourceBeatCollection(this, beats.ToArray(), newSource);
                        newSource.BeatCollection = baseSbc;
                    }
                    else
                    {
                        newSource.BeatCollection = BaseAudioSource.BeatCollection;
                        BaseAudioSource.BeatCollection.isWav = true;
                    }

                    newBaseSource = newSource;
                    AudioSources.Add("", newBaseSource);
                    IsPitch = false;
                }
                // update hihat statuses
                HasHiHatClosed = Beat.Where(x => BeatCell.HiHatClosedFileNames.Contains(x.SourceName)).Any();
                HasHiHatOpen = Beat.Where(x => BeatCell.HiHatOpenFileNames.Contains(x.SourceName)).Any();
                // do initial muting
                foreach (IStreamProvider src in AudioSources.Values)
                {
                    src.SetInitialMuting();
                }

                BaseSourceName = baseSourceName;

                BaseAudioSource = null;
                BaseAudioSource = newBaseSource;

                // set initial offset
                double offset;
                if (!IsPitch)
                {
                    offset = Beat.TakeWhile(x => x.SourceName != "").Select(x => x.Bpm).Sum() + Offset;
                }
                else
                {
                    offset = Beat.TakeWhile(x => x.SourceName == WavFileStream.SilentSourceName).Select(x => x.Bpm).Sum() + Offset;
                }
                offset = BeatCell.ConvertFromBpm(offset, BaseAudioSource);
                BaseAudioSource.SetOffset(offset);

                // add new base to mixer
                Metronome.GetInstance().AddAudioSource(BaseAudioSource);
            }
        }

        /** <summary>Set the base source. Will also set Base pitch if a pitch.</summary>
         * <param name="baseSourceName">Name of source to use.</param> */
        public void SetBaseSource(string baseSourceName)
        {
            // remove base source from AudioSources if exists
            //if (!IsPitch && BaseSourceName != null)
            //{
            //    // remove the old base wav source from mixer
            //    Metronome.GetInstance().RemoveAudioSource(BaseAudioSource);
            //    AudioSources.Remove(BaseSourceName); // for pitch layers, base source is not in AudioSources.
            //    BaseAudioSource.Dispose();
            //} 

            // is sample or pitch source?
            if (Regex.IsMatch(baseSourceName, @"^[A-Ga-g][#b]?\d+$|^[pP][\d.]+$"))
            {
                if (BasePitchSource == default(PitchStream))
                {
                    BasePitchSource = new PitchStream()
                    {
                        BaseFrequency = PitchStream.ConvertFromSymbol(baseSourceName),
                        Layer = this,
                        Volume = Volume
                    };
                    BaseAudioSource = BasePitchSource; // needs to be cast back to ISampleProvider when added to mixer
                }
                else
                {
                    BaseAudioSource = BasePitchSource;
                }

                IsPitch = true;
            }
            else
            {
                // remove pitch source if this was formerly a pitch based layer
                //if (IsPitch)
                //{
                //    // remove from mixer
                //    Metronome.GetInstance().RemoveAudioSource(BasePitchSource);
                //    BasePitchSource.Dispose();
                //    BasePitchSource = null;
                //}

                if (AudioSources.ContainsKey(""))
                {
                    Metronome.GetInstance().RemoveAudioSource(AudioSources[""]);
                    AudioSources.Remove("");
                }

                BaseAudioSource = new WavFileStream(baseSourceName)
                {
                    Layer = this,
                    Volume = Volume
                };
                
                AudioSources.Add("", BaseAudioSource);
                IsPitch = false;

                if (BeatCell.HiHatOpenFileNames.Contains(baseSourceName)) HasHiHatOpen = true;
                else if (BeatCell.HiHatClosedFileNames.Contains(baseSourceName)) HasHiHatClosed = true;
            }

            BaseSourceName = baseSourceName;

            // reassign source to existing cells that use the base source. base source beats will have an empty string
            if (Beat != null)
            {
                SetBeat(Beat.ToArray());
            }
        }

        /** <summary>Set the offset for this layer.</summary>
         * <param name="offset">Quarter notes to offset by.</param> */
        public void SetOffset(double offset)
        {
            foreach (IStreamProvider src in AudioSources.Values)
            {
                double current = src.GetOffset();
                double add = BeatCell.ConvertFromBpm(offset - Offset, src);
                src.SetOffset(current + add);
            }

            //// set for pitch / base source

            if (BasePitchSource != default(PitchStream))
            {
                double current3 = BasePitchSource.GetOffset();
                double add3 = BeatCell.ConvertFromBpm(offset - Offset, BasePitchSource);
                BasePitchSource.SetOffset(current3 + add3);
            }

            Offset = offset;
        }

        /** <summary>Set the offset for this layer.</summary>
         * <param name="offset">Beat code value to offset by.</param> */
        public void SetOffset(string offset)
        {
            double os = BeatCell.Parse(offset);
            SetOffset(os);
        }

        /** <summary>Add array of beat cells and create all audio sources.</summary>
         * <param name="beat">Array of beat cells.</param> */
        public void SetBeat(BeatCell[] beat)
        {
            // deal with the old audio sources.
            if (Beat != null)
            {
                // dispose wav audio sources if not the base
                foreach (IStreamProvider src in AudioSources.Values.Where(x => x != BaseAudioSource))
                {
                    Metronome.GetInstance().RemoveAudioSource(src);
                    src.Dispose();
                }
                AudioSources.Clear();
                BaseAudioSource.SetOffset(0);
            
                if (IsPitch) // need to rebuild the pitch source
                {
                    BasePitchSource?.Frequencies.Clear();
                    BasePitchSource.BaseFrequency = PitchStream.ConvertFromSymbol(BaseSourceName);
                }
                else
                {
                    if (BasePitchSource != null)
                    {
                        Metronome.GetInstance().RemoveAudioSource(BasePitchSource);
                        BasePitchSource.Dispose();
                        BasePitchSource = null;
                    }
            
                    AudioSources.Add("", BaseAudioSource);
                }
            }

            // refresh the hashihatxxx bools
            if (BeatCell.HiHatOpenFileNames.Contains(BaseSourceName)) HasHiHatOpen = true;
            else if (BeatCell.HiHatClosedFileNames.Contains(BaseSourceName)) HasHiHatClosed = true;

            for (int i = 0; i < beat.Count(); i++)
            {
                beat[i].Layer = this;
                if (beat[i].SourceName != string.Empty && !Regex.IsMatch(beat[i].SourceName, @"^[A-Ga-g][#b]?\d+$|^[Pp][\d.]+$"))
                {
                    // should cells of the same source use the same audiosource instead of creating new source each time? Yes
                    if (!AudioSources.ContainsKey(beat[i].SourceName))
                    {
                        var wavStream = new WavFileStream(beat[i].SourceName)
                        {
                            Layer = this
                        };
                        AudioSources.Add(beat[i].SourceName, wavStream);
                    }
                    beat[i].AudioSource = AudioSources[beat[i].SourceName];

                    if (BeatCell.HiHatOpenFileNames.Contains(beat[i].SourceName)) HasHiHatOpen = true;
                    else if (BeatCell.HiHatClosedFileNames.Contains(beat[i].SourceName)) HasHiHatClosed = true;
                }
                else
                {
                    if (beat[i].SourceName != string.Empty/* && Regex.IsMatch(beat[i].SourceName, @"^[A-Ga-g][#b]?\d+$|^[\d.]+$")*/)
                    {
                        // beat has a defined pitch
                        // check if basepitch source exists
                        if (BasePitchSource == default(PitchStream))
                        {
                            BasePitchSource = new PitchStream()
                            {
                                Layer = this,
                                Volume = Volume,
                                BaseFrequency = PitchStream.ConvertFromSymbol(beat[i].SourceName)
                            };
                            BasePitchSource.AddFrequency(beat[i].SourceName, beat[i]);
                        }
                        else BasePitchSource.AddFrequency(beat[i].SourceName, beat[i]);
                        beat[i].AudioSource = BasePitchSource;
                    }
                    else
                    {
                        if (IsPitch)
                        {
                            // no pitch defined, use base pitch
                            BasePitchSource.AddFrequency(BasePitchSource.BaseFrequency.ToString(), beat[i]);
                        }
                        beat[i].AudioSource = BaseAudioSource;
                        // is hihat sound?
                        if (BeatCell.HiHatClosedFileNames.Contains(BaseSourceName)) beat[i].IsHiHatClosed = true;
                        else if (BeatCell.HiHatOpenFileNames.Contains(BaseSourceName)) beat[i].IsHiHatOpen = true;
                    }
                }
                // set beat's value based on tempo and bytes/sec
                beat[i].SetBeatValue();
            }

            Beat = beat.ToList();

            SetBeatCollectionOnSources();
        }

        /** <summary>Set the beat collections for each sound source.</summary> 
         * <param name="Beat">The cells to process</param>
         */
        public void SetBeatCollectionOnSources()
        {
            List<IStreamProvider> completed = new List<IStreamProvider>();

            // for each beat, iterate over all beats and build a beat list of values from beats of same source.
            for (int i = 0; i < Beat.Count; i++)
            {
                List<double> cells = new List<double>();
                double accumulator = 0;
                // Once per audio source
                if (completed.Contains(Beat[i].AudioSource)) continue;
                // if selected beat is not first in cycle, set it's offset
                //if (i != 0)
                //{
                double offsetAccumulate = Offset;
                for (int p = 0; p < i; p++)
                {
                    offsetAccumulate += Beat[p].Bpm;
                }

                Beat[i].AudioSource.SetOffset(BeatCell.ConvertFromBpm(offsetAccumulate, Beat[i].AudioSource));
                //}
                // iterate over beats starting with current one. Aggregate with cells that have the same audio source.
                for (int p = i; ; p++)
                {

                    if (p == Beat.Count) p = 0;

                    if (Beat[p].AudioSource == Beat[i].AudioSource)
                    {

                        // add accumulator to previous element in list
                        if (cells.Count != 0)
                        {
                            cells[cells.Count - 1] += accumulator;
                            accumulator = 0f;
                        }
                        cells.Add(Beat[p].Bpm);
                    }
                    else accumulator += Beat[p].Bpm;

                    // job done if current beat is one before the outer beat.
                    if (p == i - 1 || (i == 0 && p == Beat.Count - 1))
                    {
                        cells[cells.Count - 1] += accumulator;
                        break;
                    }
                }
                completed.Add(Beat[i].AudioSource);

                Beat[i].AudioSource.BeatCollection = new SourceBeatCollection(this, cells.ToArray(), Beat[i].AudioSource);
            }

            // do any initial muting, includes hihat timings
            AudioSources.Values.ToList().ForEach(x => x.SetInitialMuting());

            Metronome.GetInstance().AddSourcesFromLayer(this);
        }

        /**<summary>Get a random pitch based on existing pitch layers</summary>*/
        public string GetAutoPitch()
        {
            string note;
            byte octave;

            string[] noteNames =
            {
                "A", "A#", "B", "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#"
            };

            ushort[] intervals = { 3, 4, 5, 7, 8, 9 };

            do
            {
                // determine the octave
                octave = Metronome.GetRandomNum() > 49 ? (byte)5 : (byte)4;
                // 80% chance to make a sonorous interval with last pitch layer
                if (Metronome.GetInstance().Layers.Exists(x => x.IsPitch) && Metronome.GetRandomNum() < 80)
                {
                    var last = Metronome.GetInstance().Layers.Last(x => x.IsPitch);
                    int index = Array.IndexOf(noteNames, last.BaseSourceName.TakeWhile(x => !char.IsNumber(x)));
                    index += intervals[(int)(Metronome.GetRandomNum() / 16.6667)];
                    if (index > 11) index -= 12;
                    note = noteNames[index];
                }
                else
                {
                    // randomly pick note
                    note = noteNames[(int)(Metronome.GetRandomNum() / 8.3333)];
                }
            }
            while (Metronome.GetInstance().Layers.Where(x => x.IsPitch).Any(x => x.BaseSourceName == note + octave));

            return note + octave;
        }

        /**<summary>Sum up all the Bpm values for beat cells.</summary>*/
        public double GetTotalBpmValue()
        {
            return Beat.Select(x => x.Bpm).Sum();
        }

        /** <summary>Reset this layer so that it will play from the start.</summary> */
        public void Reset()
        {
            Remainder = 0;
            foreach (IStreamProvider src in AudioSources.Values)
            {
                src.Reset();
                src.SetInitialMuting();
            }
            //BaseAudioSource.Reset();
            if (BasePitchSource != default(PitchStream))
                BasePitchSource.Reset();
        }

        /** <summary>Mute or unmute this layer.</summary> */
        public void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        /** <summary>Add to soloed group.</summary> */
        public void ToggleSoloGroup()
        {
            if (IsSoloed)
            {
                // unsolo and close the solo group if this was the only member
                IsSoloed = false;
                if (Metronome.GetInstance().Layers.Where(x => x.IsSoloed == true).Count() == 0)
                {
                    SoloGroupEngaged = false;
                }
            }
            else
            {
                // add this layer to solo group. all layers not in group will be muted.
                IsSoloed = true;
                SoloGroupEngaged = true;
            }
        }

        /** <summary>Create necessary components from the serialized values.</summary> */
        public void Deserialize()
        {
            AudioSources = new Dictionary<string, IStreamProvider>();
            SetBaseSource(BaseSourceName);
            Parse(ParsedString);
            if (ParsedOffset != string.Empty)
                SetOffset(BeatCell.Parse(ParsedOffset));
            if (pan != 0)
                Pan = pan;
            Volume = volume;
        }

        public void Dispose()
        {
            // unmute other layers if this was the only soloed layer
            if (IsSoloed) ToggleSoloGroup();

            Metronome.GetInstance().RemoveLayer(this);

            foreach (IStreamProvider src in AudioSources.Values)
            {
                src.Dispose();
            }

            if (BasePitchSource != null)
                BasePitchSource.Dispose();
        }
    }
}
