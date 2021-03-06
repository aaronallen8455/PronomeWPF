﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Pronome.Editor
{
    public class Row
    {
        /// <summary>
        /// Layer that this row is based on
        /// </summary>
        public Layer Layer;

        /// <summary>
        /// True if the BeatCode field represents the current state of the row.
        /// </summary>
        public bool BeatCodeIsCurrent = true;

        protected string _beatCode;
        /// <summary>
        /// A beat code representation of the row. Must be manually updated.
        /// </summary>
        public string BeatCode
        {
            get => _beatCode;
            set
            {
                BeatCodeIsCurrent = true;
                _beatCode = value;
            }
        }

        /// <summary>
        /// All the cells in this row, including referenced cells
        /// </summary>
        public CellList Cells; //= new CellList();

        protected double _offset;
        /// <summary>
        /// Amount of offset in BPM. Setting will effect change in UI
        /// </summary>
        public double Offset
        {
            get => _offset;
            set
            {
                if (value >= 0)
                {
                    double off = value * EditorWindow.Scale * EditorWindow.BaseFactor;
                    Canvas.Margin = new System.Windows.Thickness(off, 0, 0, 0);
                    // reposition background
                    _offset = value;
                    if (Background != null)
                    {
                        // repostion the background
                        SetBackground(Duration);
                    }
                }
            }
        }

        /// <summary>
        /// The UI friendly string version of the offset value
        /// </summary>
        public string OffsetValue;

        /// <summary>
        /// Total BPM length of the row
        /// </summary>
        public double Duration;

        /// <summary>
        /// All the mult groups in this row
        /// </summary>
        public LinkedList<MultGroup> MultGroups = new LinkedList<MultGroup>();

        /// <summary>
        /// All Repeat groups in this layer
        /// </summary>
        public LinkedList<RepeatGroup> RepeatGroups = new LinkedList<RepeatGroup>();

        /// <summary>
        /// The canvas on which all visuals are drawn, except for the 'background'
        /// </summary>
        public Canvas Canvas;

        /// <summary>
        /// The element added to the layerPanel stackpanel. Contains everything from this row.
        /// </summary>
        public Grid BaseElement;

        /// <summary>
        /// Sets the size of the row and supplies background color
        /// </summary>
        protected Rectangle Sizer;

        /// <summary>
        /// Shows the cell pattern repeating after the row ends
        /// </summary>
        public Rectangle Background;
        protected VisualBrush BackgroundBrush;

        /// <summary>
        /// Layers indexes that are referenced in this row. 0 based.
        /// </summary>
        public HashSet<int> ReferencedLayers = new HashSet<int>();

        /// <summary>
        /// Maps the index of a row to the indexes of the rows that reference it.
        /// </summary>
        public static Dictionary<int, HashSet<int>> ReferenceMap = new Dictionary<int, HashSet<int>>();

        /// <summary>
        /// The index of this row
        /// </summary>
        public int Index;

        public Canvas SelectionCanvas = new Canvas();

        public bool IsDraggingCell;
        public double CellDragAnchor = -1;

        public Row(Layer layer, bool ignoreScalingSetting = false)
        {
            Layer = layer;
            Index = Metronome.GetInstance().Layers.IndexOf(Layer);
            touchedRefs.Add(Index); // current layer ref should recurse only once

            Canvas = EditorWindow.Instance != null ? EditorWindow.Instance.Resources["rowCanvas"] as Canvas : new Canvas();
            Offset = layer.Offset;
            OffsetValue = layer.GetOffsetValue();

            Background = EditorWindow.Instance != null ? EditorWindow.Instance.Resources["rowBackgroundRectangle"] as Rectangle : new Rectangle();
            BackgroundBrush = new VisualBrush(Canvas);
            BackgroundBrush.TileMode = TileMode.Tile;
            Background.Fill = BackgroundBrush;

            Sizer = EditorWindow.Instance != null ? EditorWindow.Instance.Resources["rowSizer"] as Rectangle : new Rectangle();
            Canvas.Children.Add(Sizer);

            FillFromBeatCode(layer.ParsedString, ignoreScalingSetting);

            BaseElement = EditorWindow.Instance != null ? EditorWindow.Instance.Resources["rowBaseElement"] as Grid : new Grid();

            // handler for creating new cells on the grid
            BaseElement.MouseLeftButtonDown += BaseElement_MouseLeftButtonDown;
            BaseElement.MouseRightButtonDown += Grid_MouseDownSelectBox;

            BaseElement.Background = Brushes.Transparent;
            BaseElement.Children.Add(Canvas);
            BaseElement.Children.Add(Background);

            BaseElement.Children.Add(SelectionCanvas);
            // Add the handlers for the drag select box
            BaseElement.MouseUp += Grid_MouseUpSelectBox;
            BaseElement.MouseMove += Grid_MouseMoveSelectBox;
        }

        /// <summary>
        /// Generate the UI from a beat code string. Sets the BeatCode to the input string.
        /// </summary>
        /// <param name="beatCode"></param>
        public void FillFromBeatCode(string beatCode, bool ignoreScalingSetting = false)
        {
            OpenMultFactor = new Stack<double>();
            OpenMultFactor.Push(1);

            OpenMultFactorValue = new Stack<string>();
            OpenMultFactorValue.Push("1");

            ParsedBeatResult result = ParseBeat(beatCode, ignoreScalingSetting);
            Cells = result.Cells;
            SetBackground(result.Duration);
            // set the new beatcode string
            BeatCode = beatCode;
            BeatCodeIsCurrent = true;
        }

        /// <summary>
        /// Used in the ParseBeat method to track the currently open, nested repeat groups
        /// </summary>
        Stack<RepeatGroup> OpenRepeatGroups = new Stack<RepeatGroup>();

        /// <summary>
        /// Tracks the aggregate mult factor.
        /// </summary>
        Stack<double> OpenMultFactor;

        /// <summary>
        /// Tracks the aggregate mult factor string value.
        /// </summary>
        Stack<string> OpenMultFactorValue;

        /// <summary>
        /// Build the cell and group objects based on layer. Also adds all visuals to the canvas.
        /// </summary>
        /// <param name="beat"></param>
        /// <returns></returns>
        protected ParsedBeatResult ParseBeat(string beat, bool ignoreScalingSetting = false)
        {
            CellList cells = new CellList();

            string[] chunks = beat.Split(new char[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            Stack<MultGroup> OpenMultGroups = new Stack<MultGroup>();
            
            // BPM value
            double position = 0;// Offset;

            // build list of mult group factors ahead of time
            // this is so we already know the factor when the mult group gets created (factors are at end of group).
            List<string> MultFactors = new List<string>();
            Stack<int> mIndexStack = new Stack<int>();
            int mIndex = 0;
            foreach (Match m in Regex.Matches(beat, @"([\{\}])([\d+\-*/.]*)"))
            {
                if (m.Groups[1].Value == "{")
                {
                    MultFactors.Add("");
                    mIndexStack.Push(mIndex);
                    mIndex++;
                }
                else
                {
                    MultFactors[mIndexStack.Pop()] = m.Groups[2].Value;
                }
            }
            mIndex = 0;

            // remove comments
            beat = Regex.Replace(beat, @"!.*?!", "");
            // remove whitespace
            beat = Regex.Replace(beat, @"\s", "");
            // switch single cell repeats to bracket notation
            beat = Regex.Replace(beat, @"(?<=^|\[|\{|,|\|)([^\]\}\,\|]*?)(?=\(\d+\))", "[$1]");

            // split the string into cells
            foreach (Match match in Regex.Matches(beat, @".+?([,|]|$)"))
            {
                Cell cell = new Cell(this) { Position = position };
                cells.Add(cell);

                string chunk = match.Value;

                // add all rep and mult groups in order
                int repIndex = chunk.IndexOf('[');
                int multIndex = chunk.IndexOf('{');

                if (repIndex == -1 && OpenRepeatGroups.Any())
                {
                    foreach (RepeatGroup rep in OpenRepeatGroups)
                    {
                        rep.Cells.AddLast(cell);
                    }

                    OpenRepeatGroups.Peek().ExclusiveCells.AddLast(cell);
                }
                if (multIndex == -1 && OpenMultGroups.Any())
                {
                    foreach (MultGroup mult in OpenMultGroups)
                    {
                        mult.Cells.AddLast(cell);
                    }
                    OpenMultGroups.Peek().ExclusiveCells.AddLast(cell);
                }

                while (multIndex != -1 || repIndex != -1)
                {
                    // check for opening repeat group
                    if (repIndex != -1 && (multIndex == -1 || repIndex < multIndex))
                    {
                        OpenRepeatGroups.Push(new RepeatGroup() { Row = this });

                        foreach (RepeatGroup rep in OpenRepeatGroups)
                        {
                            rep.Cells.AddLast(cell);
                        }
                        OpenRepeatGroups.Peek().ExclusiveCells.AddLast(cell);

                        OpenRepeatGroups.Peek().Position = position;

                        chunk = chunk.Remove(repIndex, 1);

                        cell.GroupActions.AddLast((true, OpenRepeatGroups.Peek()));
                    }
                    else if (multIndex != -1)
                    {
                        // open mult group
                        OpenMultGroups.Push(new MultGroup()
                        {
                            Row = this,
                            FactorValue = MultFactors[mIndex],
                            Factor = BeatCell.Parse(MultFactors[mIndex++])
                        });

                        foreach (MultGroup mult in OpenMultGroups)
                        {
                            mult.Cells.AddLast(cell);
                        }
                        OpenMultGroups.Peek().ExclusiveCells.AddLast(cell);

                        OpenMultGroups.Peek().Position = position;

                        // track the factor if we need to scale.
                        if (ignoreScalingSetting || UserSettings.DrawMultToScaleStatic)
                        {
                            OpenMultFactor.Push(OpenMultFactor.Peek() * OpenMultGroups.Peek().Factor);
                            OpenMultFactorValue.Push(BeatCell.MultiplyTerms(OpenMultFactorValue.Peek(), OpenMultGroups.Peek().FactorValue));
                        }

                        chunk = chunk.Remove(multIndex, 1);

                        cell.GroupActions.AddLast((true, OpenMultGroups.Peek()));
                    }

                    repIndex = chunk.IndexOf('[');
                    multIndex = chunk.IndexOf('{');
                }

                cell.RepeatGroups = new LinkedList<RepeatGroup>(OpenRepeatGroups);
                cell.MultGroups = new LinkedList<MultGroup>(OpenMultGroups);

                // parse the BPM value or get reference
                if (chunk.IndexOf('$') > -1)
                {
                    // get reference
                    string r = Regex.Match(chunk, @"((?<=\$)\d+|s)").Value;
                    // validate the ref index
                    if (char.IsNumber(r, 0))
                    {
                        if (Metronome.GetInstance().Layers.Count < int.Parse(r))
                        {
                            r = "1";
                        }
                    }
                    cell.Reference = r;
                    // need to parse the reference
                    int refIndex;
                    if (cell.Reference == "s")
                    {
                        // self reference
                        refIndex = Metronome.GetInstance().Layers.IndexOf(Layer);
                    }
                    else
                    {
                        refIndex = int.Parse(cell.Reference) - 1;
                    }

                    ParsedBeatResult pbr = ResolveReference(refIndex, position);
                    
                    if (ignoreScalingSetting || UserSettings.DrawMultToScaleStatic)
                    {
                        pbr.Duration *= OpenMultFactor.Peek();
                    }

                    // progress position
                    position += pbr.Duration;
                    cell.SetDurationDirectly(pbr.Duration);

                    foreach (Cell c in pbr.Cells)
                    {
                        cells.Add(c);
                    }

                    // draw reference rect
                    Canvas.SetLeft(cell.ReferenceRectangle, cell.Position * EditorWindow.Scale * EditorWindow.BaseFactor);
                    cell.ReferenceRectangle.Width = pbr.Duration * EditorWindow.Scale * EditorWindow.BaseFactor;
                    Panel.SetZIndex(cell.ReferenceRectangle, 30);
                    // add to main canvas or repeat group canvas
                    if (OpenRepeatGroups.Any())
                    {
                        OpenRepeatGroups.Peek().Canvas.Children.Add(cell.ReferenceRectangle);
                    }
                    else
                    {
                        Canvas.Children.Add(cell.ReferenceRectangle);
                    }
                }
                else
                {
                    cell.MultFactor = OpenMultFactorValue.Peek();
                    // get bpm value
                    string bpm = Regex.Match(chunk, @"[\d./+*\-]+").Value;
                    if (!string.IsNullOrEmpty(bpm))
                    {
                        cell.Value = bpm;

                        double duration = BeatCell.Parse(bpm);

                        if (ignoreScalingSetting || UserSettings.DrawMultToScaleStatic)
                        {
                            duration *= OpenMultFactor.Peek();
                        }

                        cell.SetDurationDirectly(duration);
                        // progress position
                        position += cell.Duration; //* OpenMultFactor.Peek();
                    }
                }

                // check for source modifier
                if (chunk.IndexOf('@') > -1)
                {
                    string sourceCode = Regex.Match(chunk, @"(?<=@)([pP]\d*\.?\d*|u?\d+|[a-gA-G][b#]?\d+)").Value;

                    ISoundSource source = InternalSource.GetFromModifier(sourceCode);

                    cell.Source = source;
                }

                bool addedToRepCanvas = false;

                // close groups
                multIndex = chunk.IndexOf('}');
                repIndex = chunk.IndexOf(']');
                while (multIndex != -1 || repIndex != -1)
                {
                    // create the mult and rep groups in the correct order

                    if (multIndex > -1 && (multIndex < repIndex || repIndex == -1))
                    {
                        // close mult group
                        if (ignoreScalingSetting || UserSettings.GetSettings().DrawMultToScale)
                        {
                            OpenMultFactor.Pop();
                            OpenMultFactorValue.Pop();
                        }

                        MultGroup mg = OpenMultGroups.Pop();

                        // set duration
                        mg.Duration = position - mg.Position - OpenRepeatGroups.Select(x => x.Position).Sum();
                        mg.Duration *= OpenMultFactor.Peek();

                        var m = Regex.Match(chunk, @"\}[\d.+\-/*]+");

                        chunk = chunk.Remove(m.Index, m.Length);

                        MultGroups.AddLast(mg);

                        // log group end
                        cell.GroupActions.AddLast((false, mg));

                        // render
                        if (OpenRepeatGroups.Any())
                        {
                            OpenRepeatGroups.Peek().Canvas.Children.Add(mg.Rectangle);
                        }
                        else
                        {
                            Canvas.Children.Add(mg.Rectangle);
                        }
                    }
                    else if (repIndex > -1)
                    {
                        // close rep group

                        RepeatGroup rg = OpenRepeatGroups.Pop();
                        rg.Duration = position - rg.Position;// - OpenRepeatGroups.Select(x => x.Position).Sum();
                        Match mtch = Regex.Match(chunk, @"](\d+)");
                        if (mtch.Length == 0)
                        {
                            mtch = Regex.Match(chunk, @"]\((\d+)\)([\d+\-/*.]*)");
                            rg.Times = int.Parse(mtch.Groups[1].Value);
                            if (mtch.Groups[2].Length != 0)
                            {
                                rg.LastTermModifier = mtch.Groups[2].Value;
                            }
                        }
                        else
                        {
                            rg.Times = int.Parse(mtch.Groups[1].Value);
                        }

                        RepeatGroups.AddLast(rg);

                        cell.GroupActions.AddLast((false, rg));

                        // build the group
                        position = BuildRepeatGroup(cell, rg, OpenRepeatGroups, position, OpenMultFactor.Peek(), OpenMultFactorValue.Peek());

                        addedToRepCanvas = true;
                        // move to outer group if exists
                        chunk = chunk.Substring(chunk.IndexOf(']') + 1);
                    }

                    multIndex = chunk.IndexOf('}');
                    repIndex = chunk.IndexOf(']');
                }

                if (!addedToRepCanvas)
                {
                    // add cell rect to canvas or repeat group sub-canvas
                    if (OpenRepeatGroups.Any())
                    {
                        OpenRepeatGroups.Peek().Canvas.Children.Add(cell.Rectangle);
                    }
                    else if (string.IsNullOrEmpty(cell.Reference)) // cell's rect is not used if it's a reference
                    {
                        Canvas.Children.Add(cell.Rectangle);
                    }
                }

                // check if its a break, |, and that current rep group doesn't have a break assigned
                if (chunk.Last() == '|' && OpenRepeatGroups.Any() && OpenRepeatGroups.Peek().BreakCell == null)
                {
                    OpenRepeatGroups.Peek().BreakCell = cell;
                    cell.IsBreak = true;
                }
            }

            return new ParsedBeatResult(cells, position);
        }

        protected struct ParsedBeatResult
        {
            public CellList Cells;
            public double Duration;
            public ParsedBeatResult(CellList cells, double duration)
            {
                Cells = cells;
                Duration = duration;
            }
        }

        private HashSet<int> touchedRefs = new HashSet<int>();

        protected ParsedBeatResult ResolveReference(int refIndex, double position)
        {
            // get beat code from the layer, or from the row if available
            string beat;
            if (EditorWindow.Instance.Rows.ElementAtOrDefault(refIndex) != null)
            {
                beat = EditorWindow.Instance.Rows[refIndex].BeatCode;
            }
            else
            {
                beat = Metronome.GetInstance().Layers[refIndex].ParsedString;
            }

            // remove comments
            beat = Regex.Replace(beat, @"!.*?!", "");
            // remove whitespace
            beat = Regex.Replace(beat, @"\s", "");
            // convert self references
            beat = Regex.Replace(beat, @"(?<=\$)[sS]", (refIndex + 1).ToString());
            var matches = Regex.Matches(beat, @"(?<=\$)\d+");
            foreach (Match match in matches)
            {
                int ind;
                
                int.TryParse(match.Value, out ind);
                if (touchedRefs.Contains(refIndex))
                {
                    // remove refs that have been touched
                    // remove closest nest
                    if (Regex.IsMatch(beat, @"[[{][^[{\]}]*\$" + ind.ToString() + @"[^[{\]}]*[\]}][^\]},]*"))
                    {
                        beat = Regex.Replace(beat, @"[[{][^[{\]}]*\$" + ind.ToString() + @"[^[{\]}]*[\]}][^\]},]*", "");
                    }
                    else
                    {
                        // no nest
                        beat = Regex.Replace(beat, $@"\${ind},?", "");
                    }

                    // get rid of empty single cell repeats.
                    beat = Regex.Replace(beat, @"(?<!\]|\d)\(\d+\)[\d.+\-/*]*", "");
                    // clean out empty cells
                    beat = Regex.Replace(beat, @",,", ",");

                    beat = beat.Trim(',');
                }
            }

            touchedRefs.Add(refIndex);

            // recurse
            var pbr = ParseBeat(beat);

            HashSet<Group> touchedGroups = new HashSet<Group>();
            // mark the cells as refs
            foreach (Cell c in pbr.Cells)
            {
                c.IsReference = true;
                c.Position += position;

                // repostion reference indicator rect
                if (!string.IsNullOrEmpty(c.Reference))
                {
                    double l = Canvas.GetLeft(c.ReferenceRectangle);
                    Canvas.SetLeft(c.ReferenceRectangle, l + (position * EditorWindow.Scale * EditorWindow.BaseFactor));
                }

                // reposition groups
                foreach (RepeatGroup rg in c.RepeatGroups)
                {
                    // only reposition groups that were created within the reference
                    if (OpenRepeatGroups.Count > 0 && OpenRepeatGroups.Peek() == rg) break;
                    // only reposition each group once
                    if (touchedGroups.Contains(rg)) continue;
                    touchedGroups.Add(rg);
                    rg.Position += position;
                }
                foreach (MultGroup mg in c.MultGroups)
                {
                    if (touchedGroups.Contains(mg)) continue;
                    touchedGroups.Add(mg);
                    mg.Position += position;
                }
            }

            // no longer block this refIndex
            if (refIndex != Index)
            {
                touchedRefs.Remove(refIndex);
            }

            ReferencedLayers.Add(refIndex);
            // map referenced layer to this one
            if (ReferenceMap.ContainsKey(refIndex))
            {
                ReferenceMap[refIndex].Add(Index);
            }
            else
            {
                ReferenceMap.Add(refIndex, new HashSet<int>(new int[] { Index }));
            }

            return pbr;
        }

        /// <summary>
        /// Clear out all the data, prepare row to be rebuilt using a code string.
        /// </summary>
        public void Reset()
        {
            Canvas.Children.Clear();
            Canvas.Children.Add(Sizer);
            RepeatGroups.Clear();
            MultGroups.Clear();
            ReferencedLayers.Clear();
            Cells.Clear();
        }

        /// <summary>
        /// Redraw the editor to reflect the internal state
        /// </summary>
        public void Redraw()
        {
            string code = Stringify();
            Reset();
            FillFromBeatCode(code);
            Offset = BeatCell.Parse(OffsetValue);
        }

        /// <summary>
        /// Update the beat code for this row
        /// </summary>
        public string UpdateBeatCode()
        {
            BeatCode = Stringify();
            BeatCodeIsCurrent = true;
            return BeatCode;
        }

        /// <summary>
		/// Outputs the string representation of the beat layer from the editor.
		/// </summary>
		/// <returns></returns>
		public string Stringify()
        {
            StringBuilder result = new StringBuilder();

            foreach (Cell cell in Cells)
            {
                if (cell.IsReference) continue;

                bool innersAdded = false;

                foreach ((bool begun, Group group) in cell.GroupActions)
                {
                    if (!begun && !innersAdded)
                    {
                        // add inner components
                        StringifyInnerComponents(result, cell);

                        innersAdded = true;
                    }

                    if (begun)
                    {
                        if (group.GetType() == typeof(RepeatGroup))
                        {
                            // open repeat group
                            // is single cell?
                            if (((RepeatGroup)group).ExclusiveCells.Count != 1)
                            {
                                result.Append('[');
                            }
                        }
                        else
                        {
                            // open mult group
                            result.Append('{');
                        }
                    }
                    else
                    {
                        if (group.GetType() == typeof(RepeatGroup))
                        {
                            var rg = group as RepeatGroup;
                            // close repeat group
                            if (rg.ExclusiveCells.Count != 1)
                            {
                                result.Append(']');
                                // multi cell
                                if (!string.IsNullOrEmpty(rg.LastTermModifier))
                                {
                                    result.Append($"({rg.Times.ToString()}){rg.LastTermModifier}");
                                }
                                else
                                {
                                    result.Append($"{rg.Times.ToString()}");
                                }
                            }
                            else
                            {
                                // single cell
                                if (!string.IsNullOrEmpty(rg.LastTermModifier))
                                {
                                    result.Append($"({rg.Times.ToString()}){rg.LastTermModifier}");
                                }
                                else
                                {
                                    result.Append($"({rg.Times.ToString()})");
                                }
                            }
                        }
                        else
                        {
                            // close mult group
                            result.Append('}');
                            result.Append((group as MultGroup).FactorValue);
                        }
                    }
                }

                if (!innersAdded)
                {
                    StringifyInnerComponents(result, cell);
                }

                if (cell.IsBreak)
                {
                    result.Append('|');
                }
                else
                {
                    result.Append(',');
                }
            }

            return result.ToString().TrimEnd(',');
        }

        /// <summary>
        /// Stringifies the inner components - value, source mod, reference.
        /// </summary>
        /// <param name="result">Result.</param>
        /// <param name="cell">Cell.</param>
        private void StringifyInnerComponents(StringBuilder result, Cell cell)
        {
            if (string.IsNullOrEmpty(cell.Reference))
            {
                result.Append(cell.Value);
            }
            else
            {
                result.Append($"${cell.Reference}");
            }
            // check for source modifier
            if (cell.Source != null && cell.Source != Layer.BaseAudioSource.SoundSource)
            {
                string source;
                // is pitch or wav?
                if (cell.Source.IsPitch)
                {
                    source = cell.Source.Uri;
                }
                else
                {
                    source = cell.Source.Index.ToString();
                }
                result.Append($"@{source}");
            }
        }

        public void BeginDraggingCell(double xPos)
        {
            CellDragAnchor = xPos;
            IsDraggingCell = true;
            BaseElement.CaptureMouse();
        }

        /// <summary>
        /// Perform all graphical tasks with initializing a repeat group. Group must have and Times, LastTermMod, Postion, Duration already set.
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="rg"></param>
        /// <param name="openRepeatGroups"></param>
        /// <returns></returns>
        protected double BuildRepeatGroup(Cell cell, RepeatGroup rg, Stack<RepeatGroup> openRepeatGroups, double position, double multGroupFactor, string multGroupFactorValue, bool addToCanvas = true)
        {
            // render
            Canvas.Children.Add(rg.Rectangle);
            // add the child canvas
            if (openRepeatGroups.Any())
            {
                // nested repeats
                openRepeatGroups.Peek().Canvas.Children.Add(rg.Canvas);
                rg.HostCanvas = openRepeatGroups.Peek().Canvas;
            }
            else
            {
                // added to row canvas
                Canvas.Children.Add(rg.Canvas);
                rg.HostCanvas = Canvas;
            }

            // add cell rect if not a reference
            if (string.IsNullOrEmpty(cell.Reference) && addToCanvas)
            {
                rg.Canvas.Children.Add(cell.Rectangle);
            }

            double breakOff = 0;
            // subtract the break position, if exists
            if (rg.BreakCell != null)
            {
                // find the length of the segment after the break, which needs to be subtracted
                breakOff = rg.Duration - (rg.BreakCell.Position + rg.BreakCell.Duration - rg.Position);
            }

            // get size of the host rects
            double hostWidth = (rg.Duration - (string.IsNullOrEmpty(cell.Reference) ? cell.ActualDuration : 0)) * EditorWindow.Scale * EditorWindow.BaseFactor;
            if (string.IsNullOrEmpty(cell.Reference) && EditorWindow.Instance != null)
            {
                hostWidth += (double)EditorWindow.Instance.Resources["cellWidth"];
            }

            // append duplicates of sub-canvas
            for (int i = 0; i < rg.Times - 1; i++)
            {
                VisualBrush duplicate = new VisualBrush(rg.Canvas);
                var dupHost = EditorWindow.Instance != null ? EditorWindow.Instance.Resources["repeatRectangle"] as Rectangle : new Rectangle();
                dupHost.Width = hostWidth;
                if (i == rg.Times - 1)
                {
                    dupHost.Width -= breakOff * EditorWindow.Scale * EditorWindow.BaseFactor;
                }
                // fill with dupe content
                dupHost.Fill = duplicate;
                // do offsets
                Canvas.SetLeft(dupHost, position * EditorWindow.Scale * EditorWindow.BaseFactor);
                if (EditorWindow.Instance != null)
                {
                    Canvas.SetTop(dupHost, (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - (double)EditorWindow.Instance.Resources["cellHeight"] / 2);
                }
                rg.HostRects.AddLast(dupHost);
                // render it
                if (openRepeatGroups.Any())
                {
                    openRepeatGroups.Peek().Canvas.Children.Add(dupHost);
                }
                else
                {
                    Canvas.Children.Add(dupHost);
                }
                // move position forward
                position += rg.Duration;
            }

            // account for portion cut off by a break cell
            position -= breakOff;
            
            double ltmDur = BeatCell.Parse(rg.LastTermModifier);

            if (UserSettings.DrawMultToScaleStatic)
            {
                ltmDur *= multGroupFactor;
            }
            rg.MultFactor = multGroupFactorValue;
            
            rg.FullDuration = position - rg.Position;

            position += ltmDur;

            return position;
        }

        /// <summary>
        /// Apply current state to the background element and position the sizer.
        /// </summary>
        /// <param name="widthBpm"></param>
        protected void SetBackground(double widthBpm)
        {
            Duration = widthBpm;
            // set background tile size
            if (EditorWindow.Instance != null)
            {
                double rowHeight = (double)EditorWindow.Instance.Resources["rowHeight"];
                double width = widthBpm * EditorWindow.Scale * EditorWindow.BaseFactor;
                double offset = Offset * EditorWindow.Scale * EditorWindow.BaseFactor;
                BackgroundBrush.Viewport = new System.Windows.Rect(0, rowHeight, width, rowHeight);
                BackgroundBrush.ViewportUnits = BrushMappingMode.Absolute;
                Background.Margin = new System.Windows.Thickness(width + offset, 0, 0, 0);
                Sizer.Width = width;
            }
        }

        /// <summary>
        /// Change the sizer width and reposition background by an amount in BPM
        /// </summary>
        /// <param name="diff"></param>
        public void ChangeSizerWidthByAmount(double diff)
        {
            if (EditorWindow.Instance != null)
            {
                double change = diff * EditorWindow.Scale * EditorWindow.BaseFactor;
                double offset = Offset * EditorWindow.Scale * EditorWindow.BaseFactor;
                double rowHeight = (double)EditorWindow.Instance.Resources["rowHeight"];
                Sizer.Width += change;
                // reposition background
                BackgroundBrush.Viewport = new System.Windows.Rect(0, rowHeight, Sizer.Width, rowHeight);
                Background.Margin = new System.Windows.Thickness(Background.Margin.Left + change, 0, 0, 0);
            }
        }

        /// <summary>
        /// Draw the grid lines for selected cells in this row. Also sets the FirstCell and LastCell of selection object.
        /// </summary>
        /// <param name="intervalCode"></param>
        public void DrawGridLines(string intervalCode)
        {
            if (EditorWindow.Instance == null) return;

            double gridCellSize;
            if (BeatCell.TryParse(intervalCode, out gridCellSize))
            {
                gridCellSize *= EditorWindow.BaseFactor * EditorWindow.Scale;
                // get duration of selection and leftmost position
                double duration = 0; // BPM
                double positionBpm = double.MaxValue;
                double maxPostion = -1;
                foreach (Cell cell in Cell.SelectedCells.Cells.Where(x => !x.IsReference))
                {
                    //else
                    //{
                        duration += cell.ActualDuration;
                    //}
                    // find first cell
                    if (cell.Position < positionBpm)
                    {
                        positionBpm = cell.Position;
                        Cell.SelectedCells.FirstCell = cell;
                    }
                    // find last cell
                    if (cell.Position > maxPostion)
                    {
                        maxPostion = cell.Position;
                        Cell.SelectedCells.LastCell = cell;
                    }
                }
                if (string.IsNullOrEmpty(Cell.SelectedCells.LastCell.Reference))
                {
                    // leave the duration in for references, otherwise it's zero width
                    duration -= Cell.SelectedCells.LastCell.ActualDuration;
                }

                Rectangle sizer = EditorWindow.Instance.GridSizer;
                // set grid cell size
                sizer.Width = gridCellSize;
                Rectangle tick = EditorWindow.Instance.GridTick;
                Rectangle leftGrid = EditorWindow.Instance.GridLeft;
                // set left grid width
                leftGrid.Width = (positionBpm + Offset) * EditorWindow.Scale * EditorWindow.BaseFactor + tick.Width;
                Rectangle rightGrid = EditorWindow.Instance.GridRight;
                // position right grid
                rightGrid.Margin = new System.Windows.Thickness(leftGrid.Width + duration * EditorWindow.Scale * EditorWindow.BaseFactor - tick.Width, 0, 0, 0);
                VisualBrush gridBrush = EditorWindow.Instance.GridBrush;
                // set viewport size
                gridBrush.Viewport = new System.Windows.Rect(0, sizer.Height, gridCellSize, sizer.Height);

                BaseElement.Children.Add(leftGrid);
                BaseElement.Children.Add(rightGrid);
            }
        }

        /// <summary>
        /// Create a new cell at the position on grid if within a certain range of a grid line
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BaseElement_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // pass to the select box handler
                Grid_MouseDownSelectBox(sender, e);
            }
            else
            {
                if (Cell.SelectedCells.Cells.Any())
                {
                    AddCell action = new AddCell(e.GetPosition((Grid)sender).X, this);

                    action.Redo();

                    if (action.IsValid)
                    {
                        EditorWindow.Instance.AddUndoAction(action);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Move mouse handler while selection box is being drawn
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_MouseMoveSelectBox(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (SelectionCanvas.IsMouseCaptured)
            {
                double x = e.GetPosition(BaseElement).X;
                double y = Math.Max(
                    Math.Min(e.GetPosition(BaseElement).Y, (double)EditorWindow.Instance.Resources["rowHeight"]),
                    0);

                Rectangle selector = EditorWindow.Instance.Resources["boxSelect"] as Rectangle;

                // change the size and/or position of the selector based on new mouse position
                if (x < selectorOrigin.X)
                {
                    Canvas.SetLeft(selector, x);
                    selector.Width = selectorOrigin.X - x;
                }
                else if (x >= selectorOrigin.X)
                {
                    Canvas.SetLeft(selector, selectorOrigin.X);
                    selector.Width = x - selectorOrigin.X;
                }

                if (y < selectorOrigin.Y)
                {
                    Canvas.SetTop(selector, y);
                    selector.Height = selectorOrigin.Y - y;
                }
                else if (y >= selectorOrigin.Y)
                {
                    Canvas.SetTop(selector, selectorOrigin.Y);
                    selector.Height = y - selectorOrigin.Y;
                }

                // scroll the window if necessary

                double windowWidth = EditorWindow.Instance.Width - 20;
                double scrollAmount = EditorWindow.Instance.layerPanelScrollViewer.HorizontalOffset;
                // scroll right
                if (windowWidth < x - scrollAmount)
                {
                    EditorWindow.Instance.layerPanelScrollViewer.ScrollToHorizontalOffset(scrollAmount + .1);
                }
                else if (x - scrollAmount < -20) // scroll left
                {
                    EditorWindow.Instance.layerPanelScrollViewer.ScrollToHorizontalOffset(scrollAmount - .1);
                }
            }
            else if (IsDraggingCell && BaseElement.IsMouseCaptured)
            {
                // dragging a cell
                double x = e.GetPosition(BaseElement).X / EditorWindow.Scale / EditorWindow.BaseFactor;
                //double startPos = Cell.SelectedCells.FirstCell.Position;
                //double endPos = Cell.SelectedCells.LastCell.Position;
                double increment = EditorWindow.Instance.GetGridIncrement();

                if (increment > 0)
                {
                    if (x >= CellDragAnchor + increment && MoveCells.CanPerformRightMove())
                    {
                        // shift right
                        var action = new MoveCells(
                            Cell.SelectedCells.Cells.ToArray(),
                            EditorWindow.Instance.incrementInput.Text, 
                            (int)((x - CellDragAnchor) / increment));

                        action.Redo();
                        EditorWindow.Instance.AddUndoAction(action);

                        CellDragAnchor += increment;
                    }
                    else if (x <= CellDragAnchor - increment && MoveCells.CanPerformLeftMove())
                    {
                        // shift left
                        var action = new MoveCells(
                            Cell.SelectedCells.Cells.ToArray(),
                            EditorWindow.Instance.incrementInput.Text,
                            (int)(-(CellDragAnchor - x) / increment));

                        action.Redo();
                        EditorWindow.Instance.AddUndoAction(action);

                        CellDragAnchor -= increment;
                    }
                }
            }
        }

        /// <summary>
        /// Remove the selection box and select cells within it's range. Deselect all if no cells selected
        /// Also ends the cell drag action
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_MouseUpSelectBox(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SelectionCanvas.IsMouseCaptured)
            {
                SelectionCanvas.ReleaseMouseCapture();
                Rectangle selector = EditorWindow.Instance.Resources["boxSelect"] as Rectangle;

                // select all cells within the range
                double start = Math.Min(selectorOrigin.X, Canvas.GetLeft(selector)) / EditorWindow.Scale / EditorWindow.BaseFactor - Offset;
                double end = start + selector.Width / EditorWindow.Scale / EditorWindow.BaseFactor;
                IEnumerable<Cell> cells = Cells.Where(x => !x.IsReference).SkipWhile(x => x.Position < start).TakeWhile(x => x.Position < end);

                SelectionCanvas.Children.Remove(selector);

                Cell.SelectedCells.DeselectAll(false);

                if (cells.Any())
                {
                    foreach (Cell cell in cells)
                    {
                        cell.ToggleSelect(false);
                    }
                }

                EditorWindow.Instance.UpdateUiForSelectedCell();
            }
            else if (IsDraggingCell && BaseElement.IsMouseCaptured)
            {
                // end cell dragging
                BaseElement.ReleaseMouseCapture();
                IsDraggingCell = false;
                CellDragAnchor = -1;
            }
        }

        Point selectorOrigin = new Point();
        /// <summary>
        /// Start drawing the selection box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_MouseDownSelectBox(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // unfocus any ui elements (prevents a value holding over to a group selection)
            //Keyboard.ClearFocus();
            //Keyboard.Focus(EditorWindow.Instance);
            var focused = Keyboard.FocusedElement;
            if (focused.GetType() == typeof (TextBox))
            {
                focused.RaiseEvent(new RoutedEventArgs(TextBox.LostFocusEvent));
            }

            Rectangle selector = EditorWindow.Instance.Resources["boxSelect"] as Rectangle;

            if (selector.Parent == null)
            {
                // get selection origin
                double x = e.GetPosition(BaseElement).X;
                double y = e.GetPosition(BaseElement).Y;
                selectorOrigin.X = x;
                selectorOrigin.Y = y;

                // attach the selection box to the canvas
                SelectionCanvas.CaptureMouse();
                selector.Width = 0;
                selector.Height = 0;
                Canvas.SetTop(selector, y);
                Canvas.SetLeft(selector, x);
                Canvas.SetZIndex(selector, 500);
                SelectionCanvas.Children.Add(selector);
            }
        }


        private void BaseElement_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // unfocus
            Keyboard.ClearFocus();
        }
    }
}
