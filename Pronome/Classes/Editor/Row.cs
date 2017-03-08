using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Text;

namespace Pronome.Editor
{
    class Row
    {
        public Layer Layer;
        public LinkedList<Cell> Cells;
        public double Offset;
        public LinkedList<MultGroup> MultGroups = new LinkedList<MultGroup>();
        public LinkedList<RepeatGroup> RepeatGroups = new LinkedList<RepeatGroup>();
        public Canvas Canvas;
        protected Rectangle Sizer = EditorWindow.Instance.Resources["rowSizer"] as Rectangle;
        public Rectangle Background;
        protected VisualBrush BackgroundBrush;
        public List<Cell> SelectedCells = new List<Cell>();
        public HashSet<int> ReferencedLayers = new HashSet<int>();

        public Row(Layer layer)
        {
            Layer = layer;
            Offset = layer.Offset;
            Canvas = EditorWindow.Instance.Resources["rowCanvas"] as Canvas;
            Background = EditorWindow.Instance.Resources["rowBackgroundRectangle"] as Rectangle;
            BackgroundBrush = new VisualBrush(Canvas);
            BackgroundBrush.TileMode = TileMode.Tile;
            Background.Fill = BackgroundBrush;
            Canvas.Children.Add(Sizer);
            ParsedBeatResult pbr = ParseBeat(layer.ParsedString);
            Cells = pbr.Cells;
            SetBackground(pbr.Duration);
        }

        protected ParsedBeatResult ParseBeat(string beat)
        {
            LinkedList<Cell> cells = new LinkedList<Cell>();

            string[] chunks = beat.Split(new char[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            Stack<MultGroup> OpenMultGroups = new Stack<MultGroup>();
            Stack<RepeatGroup> OpenRepeatGroups = new Stack<RepeatGroup>();
            double position = Offset;

            // remove comments
            beat = Regex.Replace(beat, @"!.*?!", "");
            // remove whitespace
            beat = Regex.Replace(beat, @"\s", "");

            // split the string into cells
            foreach (Match match in Regex.Matches(beat, @".+?([,|]|$)"))
            {
                Cell cell = new Cell(this) { Position = position };

                string chunk = match.Value;

                // check for opening mult group
                if (chunk.IndexOf('{') > -1)
                {
                    OpenMultGroups.Push(new MultGroup() { Row = this });
                    cell.MultGroups = new LinkedList<MultGroup>(OpenMultGroups);
                    OpenMultGroups.Peek().Cells.AddLast(cell);
                    OpenMultGroups.Peek().Position = cell.Position;
                }

                // check for opening repeat group
                if (chunk.IndexOf('[') > -1)
                {
                    OpenRepeatGroups.Push(new RepeatGroup() { Row = this });
                    cell.RepeatGroups = new LinkedList<RepeatGroup>(OpenRepeatGroups);
                    OpenRepeatGroups.Peek().Cells.AddLast(cell);
                    OpenRepeatGroups.Peek().Position = cell.Position;
                }

                // parse the BPM value or get reference
                if (chunk.IndexOf('$') > -1)
                {
                    // get reference
                    string r = Regex.Match(chunk, @"((?<=\$)\d+|s)").Value;
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
                        refIndex = int.Parse(cell.Reference);
                    }

                    ParsedBeatResult pbr = ResolveReference(refIndex, position);
                    // add the ref cells in
                    //cells = new LinkedList<Cell>(cells.Concat(pbr.Cells));
                    // progress position
                    position += pbr.Duration;
                }
                else
                {
                    // get bpm value
                    string bpm = Regex.Match(chunk, @"[\d./+*\-]+").Value;
                    if (!string.IsNullOrEmpty(bpm))
                    {
                        cell.Value = bpm;
                        cell.Duration = BeatCell.Parse(bpm);
                        // progress position
                        position += cell.Duration;
                    }
                }

                // check for source modifier
                if (chunk.IndexOf('@') > -1)
                {
                    string source = Regex.Match(chunk, @"(?<=@)([pP]\d*\.?\d*|\d|[a-gA-G][b#]?\d+)").Value;
                    cell.Source = source;
                }

                // check for cell repeat
                if (Regex.IsMatch(chunk, @"\(\d+\)[\d+\-/*.]*"))
                {
                    Match m = Regex.Match(chunk, @"\((\d+)\)([\d+\-/*.]*)");
                    //cell.Repeat = new Cell.CellRepeat()
                    //{
                    //    Times = int.Parse(m.Groups[1].Value),
                    //    LastTermModifier = m.Groups[2].Length != 0 ? BeatCell.Parse(m.Groups[2].Value) : 0
                    //};
                    
                    var rg = new RepeatGroup() { Row = this };
                    rg.Cells.AddLast(cell);
                    rg.Position = cell.Position;
                    cell.RepeatGroups.AddLast(rg);
                }

                // check for closing mult group
                if (chunk.IndexOf('}') > -1)
                {
                    MultGroup mg = OpenMultGroups.Pop();
                    mg.Factor = Regex.Match(chunk, @"(?<=})[\d.+\-/*]+").Value;
                    // set duration
                    mg.Duration = cell.Position + cell.Duration - mg.Position;
                    MultGroups.AddLast(mg);
                    // render
                    Canvas.Children.Add(mg.Rectangle);
                }

                // check for closing repeat group, getting times and last term modifier
                if (chunk.IndexOf(']') > -1)
                {
                    RepeatGroup rg = OpenRepeatGroups.Pop();
                    rg.Duration = cell.Position + cell.Duration - rg.Position;
                    Match mtch = Regex.Match(chunk, @"](\d+)");
                    if (mtch.Length == 0)
                    {
                        mtch = Regex.Match(chunk, @"]\((\d+)\)([\d+\-/*.]*)");
                        rg.Times = int.Parse(mtch.Groups[1].Value);
                        rg.LastTermModifier = mtch.Groups[2].Value;//.Length != 0 ? BeatCell.Parse(mtch.Groups[2].Value) : 0;
                    }
                    else
                    {
                        rg.Times = int.Parse(mtch.Groups[1].Value);
                    }
                    RepeatGroups.AddLast(rg);
                    // render
                    Canvas.Children.Add(rg.Rectangle);
                    // add the child canvas
                    if (OpenRepeatGroups.Any())
                    {
                        // nested repeats
                        OpenRepeatGroups.Peek().Canvas.Children.Add(rg.Canvas);
                    }
                    else
                    {
                        // added to row canvas
                        Canvas.Children.Add(rg.Canvas);
                    }
                    // forward the position to account for repeats
                    //position += rg.Duration * (rg.Times - 1);
                    rg.Canvas.Children.Add(cell.Rectangle);
                    // append duplicates of sub-canvas
                    for (int i=0; i < rg.Times - 1; i++)
                    {
                        VisualBrush duplicate = new VisualBrush(rg.Canvas);
                        var dupHost = EditorWindow.Instance.Resources["repeatRectangle"] as System.Windows.Shapes.Rectangle;
                        // size the rect
                        dupHost.Width = (rg.Duration - cell.Duration) * EditorWindow.Scale * EditorWindow.BaseFactor + (double)EditorWindow.Instance.Resources["cellWidth"];
                        // fill with dupe content
                        dupHost.Fill = duplicate;
                        // do offsets
                        Canvas.SetLeft(dupHost, position * EditorWindow.Scale * EditorWindow.BaseFactor);
                        Canvas.SetTop(dupHost, (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - (double)EditorWindow.Instance.Resources["cellHeight"] / 2);
                        rg.HostRects.AddLast(dupHost);
                        // render it
                        Canvas.Children.Add(dupHost);
                        // move position forward
                        position += rg.Duration;
                    }
                    position += BeatCell.Parse(rg.LastTermModifier) * EditorWindow.Scale * EditorWindow.BaseFactor;
                }
                else
                {
                    // add cell rect to canvas or repeat group sub-canvas
                    if (OpenRepeatGroups.Any())
                    {
                        OpenRepeatGroups.Peek().Canvas.Children.Add(cell.Rectangle);
                    }
                    else if (cell.Reference == string.Empty) // cell's rect is not used if it's a reference
                    {
                        Canvas.Children.Add(cell.Rectangle);
                    }
                }

                // check if its a break, |
                if (chunk.Last() == '|')
                {
                    cell.IsBreak = true;
                }

                cells.AddLast(cell);
            }

            // set the background tiling
            //SetBackground(position);

            return new ParsedBeatResult(cells, position);
        }

        protected struct ParsedBeatResult
        {
            public LinkedList<Cell> Cells;
            public double Duration;
            public ParsedBeatResult(LinkedList<Cell> cells, double duration)
            {
                Cells = cells;
                Duration = duration;
            }
        }

        private HashSet<int> touchedRefs = new HashSet<int>();

        protected ParsedBeatResult ResolveReference(int refIndex, double position)
        {
            touchedRefs.Add(refIndex);
            string beat = Metronome.GetInstance().Layers[refIndex].ParsedString;
            // remove comments
            beat = Regex.Replace(beat, @"!.*?!", "");
            // remove whitespace
            beat = Regex.Replace(beat, @"\s", "");
            // convert self references
            beat = Regex.Replace(beat, @"(?<=\$)[sS]", refIndex.ToString());
            var matches = Regex.Matches(beat, @"(?<=\$)d+");
            foreach (Match match in matches)
            {
                int ind;
                if (!int.TryParse(match.Value, out ind))
                {
                    ind = refIndex;
                }
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

                    // clean out empty cells
                    beat = Regex.Replace(beat, @",,", ",");
                    //refBeat = Regex.Replace(refBeat, @",$", "");
                    beat = beat.Trim(',');
                }
            }

            // recurse
            var pbr = ParseBeat(beat);

            // mark the cells as refs
            foreach (Cell c in pbr.Cells)
            {
                c.IsReference = true;
                c.Position += position;
            }

            // no longer block this refIndex
            touchedRefs.Remove(refIndex);

            ReferencedLayers.Add(refIndex);

            return pbr;
        }

        public string Stringify()
        {
            StringBuilder result = new StringBuilder();

            foreach (Cell cell in Cells.Where(x => !x.IsReference))
            {
                // check for open mult group
                foreach (MultGroup mg in cell.MultGroups)
                {
                    if (mg.Cells.First.Value == cell)
                    {
                        //OpenMultGroups.Push(cell.MultGroup);
                        result.Append('{');
                    }
                }
                // check for open repeat group
                foreach (RepeatGroup rg in cell.RepeatGroups)
                {
                    if (rg.Cells.First.Value == cell && rg.Cells.Count > 1)
                    {
                        //OpenRepeatGroups.Push(cell.RepeatGroup);
                        result.Append('[');
                    }
                }
                // get duration or reference ID
                if (cell.Reference == string.Empty)
                {
                    result.Append(cell.Value);
                }
                else
                {
                    result.Append($"${cell.Reference}");
                }
                // check for source modifier
                if (cell.Source != Layer.BaseSourceName)
                {
                    string source;
                    // is pitch or wav?
                    if (cell.Source.IndexOf(".wav") == -1)
                    {
                        source = cell.Source;
                    }
                    else
                    {
                        source = WavFileStream.FileNameIndex[int.Parse(cell.Source), 0];
                    }
                    result.Append($"@{cell.Source}");
                }
                // check for close repeat group
                foreach (RepeatGroup rg in cell.RepeatGroups)
                {
                    if (rg.Cells.Last.Value == cell)
                    {
                        // is single cell rep?
                        if (rg.Cells.Count == 1)
                        {
                            result.Append($"({rg.Times})");
                            if (rg.LastTermModifier != string.Empty)
                            {
                                result.Append(rg.LastTermModifier);
                            }
                        }
                        else
                        {
                            // multi cell
                            if (rg.LastTermModifier != string.Empty)
                            {
                                result.Append($"]({rg.Times}){rg.LastTermModifier}");
                            }
                            else
                            {
                                result.Append($"]{rg.Times}");
                            }
                        }
                    }
                }
                // check for close mult group
                foreach (MultGroup mg in cell.MultGroups)
                {
                    if (mg.Cells.Last.Value == cell)
                    {
                        result.Append($"}}{mg.Factor}");
                    }
                }
                // check if is break point |
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
        /// Apply current state to the background element
        /// </summary>
        /// <param name="widthBpm"></param>
        protected void SetBackground(double widthBpm)
        {
            // set background tile size
            double rowHeight = (double)EditorWindow.Instance.Resources["rowHeight"];
            double width = (widthBpm - Offset) * EditorWindow.Scale * EditorWindow.BaseFactor;
            double offset = Offset * EditorWindow.Scale * EditorWindow.BaseFactor;
            BackgroundBrush.Viewport = new System.Windows.Rect(0, rowHeight, width, rowHeight);
            BackgroundBrush.ViewportUnits = BrushMappingMode.Absolute;
            Background.Margin = new System.Windows.Thickness(width + offset, 0, 0, 0);
            Sizer.Width = width;
            // offset the sizer
            Canvas.SetLeft(Sizer, offset);
        }
    }
}
