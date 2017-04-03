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
        /// Determines how close a mouse click needs to be to a grid line to count as that line. It's a factor of the increment size.
        /// </summary>
        public const float GridProx = .1f;

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
        public Grid BaseElement = new Grid();
        
        /// <summary>
        /// Sets the size of the row and supplies background color
        /// </summary>
        protected Rectangle Sizer = EditorWindow.Instance.Resources["rowSizer"] as Rectangle;

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

        public Row(Layer layer)
        {
            Layer = layer;
            Index = Metronome.GetInstance().Layers.IndexOf(Layer);
            touchedRefs.Add(Index); // current layer ref should recurse only once

            Canvas = EditorWindow.Instance.Resources["rowCanvas"] as Canvas;
            Offset = layer.Offset;
            OffsetValue = layer.GetOffsetValue();
            //Panel.SetZIndex(Canvas, 20);
            //Canvas.Margin = new System.Windows.Thickness(Offset * EditorWindow.Scale * EditorWindow.BaseFactor, 0, 0, 0);
            Background = EditorWindow.Instance.Resources["rowBackgroundRectangle"] as Rectangle;
            BackgroundBrush = new VisualBrush(Canvas);
            BackgroundBrush.TileMode = TileMode.Tile;
            Background.Fill = BackgroundBrush;
            Canvas.Children.Add(Sizer);

            FillFromBeatCode(layer.ParsedString);
            //ParsedBeatResult pbr = ParseBeat(layer.ParsedString);
            //Cells = pbr.Cells;
            //SetBackground(pbr.Duration);

            // handler for creating new cells on the grid
            BaseElement.MouseLeftButtonDown += BaseElement_MouseLeftButtonDown;

            BaseElement.Background = Brushes.Transparent;
            BaseElement.Children.Add(Canvas);
            BaseElement.Children.Add(Background);
        }

        /// <summary>
        /// Generate the UI from a beat code string. Sets the BeatCode to the input string.
        /// </summary>
        /// <param name="beatCode"></param>
        public void FillFromBeatCode(string beatCode)
        {
            ParsedBeatResult result = ParseBeat(beatCode);
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
        /// Build the cell and group objects based on layer. Also adds all visuals to the canvas.
        /// </summary>
        /// <param name="beat"></param>
        /// <returns></returns>
        protected ParsedBeatResult ParseBeat(string beat)
        {
            CellList cells = new CellList();

            string[] chunks = beat.Split(new char[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            Stack<MultGroup> OpenMultGroups = new Stack<MultGroup>();
            
            // BPM value
            double position = 0;// Offset;

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

                string chunk = match.Value;

                // check for opening mult group
                if (chunk.IndexOf('{') > -1)
                {
                    while (chunk.Contains('{'))
                    {
                        OpenMultGroups.Push(new MultGroup() { Row = this });
                        cell.MultGroups = new LinkedList<MultGroup>(OpenMultGroups);
                        OpenMultGroups.Peek().Cells.AddLast(cell);
                        OpenMultGroups.Peek().Position = cell.Position;
                        MultGroups.AddLast(OpenMultGroups.Peek());

                        chunk = chunk.Remove(chunk.IndexOf('{'), 1);
                    }
                }
                else if (OpenMultGroups.Any())
                {
                    cell.MultGroups = new LinkedList<MultGroup>(OpenMultGroups);
                    OpenMultGroups.Peek().Cells.AddLast(cell);
                }

                // check for opening repeat group
                if (chunk.IndexOf('[') > -1)
                {
                    while (chunk.Contains('['))
                    {
                        OpenRepeatGroups.Push(new RepeatGroup() { Row = this });
                        cell.RepeatGroups = new LinkedList<RepeatGroup>(OpenRepeatGroups);
                        OpenRepeatGroups.Peek().Cells.AddLast(cell);
                        OpenRepeatGroups.Peek().Position = cell.Position;
                        RepeatGroups.AddLast(OpenRepeatGroups.Peek());

                        chunk = chunk.Remove(chunk.IndexOf('['), 1);
                    }
                }
                else if (OpenRepeatGroups.Any())
                {
                    cell.RepeatGroups = new LinkedList<RepeatGroup>(OpenRepeatGroups);
                    OpenRepeatGroups.Peek().Cells.AddLast(cell);
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
                        refIndex = int.Parse(cell.Reference) - 1;
                    }

                    ParsedBeatResult pbr = ResolveReference(refIndex, position);
                    // add the ref cells in
                    //cells = new LinkedList<Cell>(cells.Concat(pbr.Cells));
                    // progress position
                    position += pbr.Duration;
                    cell.SetDurationDirectly(pbr.Duration);

                    foreach (Cell c in pbr.Cells)
                    {
                        cells.Add(c);
                    }

                    // draw reference rect
                    //cell.ReferenceRectangle = EditorWindow.Instance.Resources["referenceRectangle"] as Rectangle;
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
                    // get bpm value
                    string bpm = Regex.Match(chunk, @"[\d./+*\-]+").Value;
                    if (!string.IsNullOrEmpty(bpm))
                    {
                        cell.Value = bpm;
                        cell.SetDurationDirectly(BeatCell.Parse(bpm));
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
                bool singleCellRepeat = false;
                //if (Regex.IsMatch(chunk, @"\(\d+\)[\d+\-/*.]*"))
                //{
                //    singleCellRepeat = true;
                //    Match m = Regex.Match(chunk, @"\((\d+)\)([\d+\-/*.]*)");
                //    //cell.Repeat = new Cell.CellRepeat()
                //    //{
                //    //    Times = int.Parse(m.Groups[1].Value),
                //    //    LastTermModifier = m.Groups[2].Length != 0 ? BeatCell.Parse(m.Groups[2].Value) : 0
                //    //};
                //    
                //    var rg = new RepeatGroup() { Row = this };
                //    rg.Cells.AddLast(cell);
                //    rg.Position = cell.Position;
                //    rg.Duration = cell.Duration;
                //    rg.Times = int.Parse(m.Groups[1].Value);
                //    rg.LastTermModifier = m.Groups[2].Value;
                //    cell.RepeatGroups.AddLast(rg);
                //
                //    position = BuildRepeatGroup(cell, rg, OpenRepeatGroups, position);
                //}

                // check for closing mult group
                //if (chunk.IndexOf('}') > -1)
                //{
                    // handle multiple groups
                while (chunk.Contains('}'))
                {
                    MultGroup mg = OpenMultGroups.Pop();
                    mg.Factor = Regex.Match(chunk, @"(?<=})[\d.+\-/*]+").Value;
                    // set duration
                    mg.Duration = cell.Position + cell.Duration - mg.Position;
                    // render
                    if (OpenRepeatGroups.Any())
                    {
                        OpenRepeatGroups.Peek().Canvas.Children.Add(mg.Rectangle);
                    }
                    else
                    {
                        Canvas.Children.Add(mg.Rectangle);
                    }
                    var m = Regex.Match(chunk, @"\}[\d.+\-/*]+");

                    chunk = chunk.Remove(m.Index, m.Length);
                }
                //}

                // check for closing repeat group, getting times and last term modifier
                if (chunk.IndexOf(']') > -1)
                {
                    // handle closely nested repeat group ends
                    while (chunk.Contains(']'))
                    {
                        RepeatGroup rg = OpenRepeatGroups.Pop();
                        rg.Duration = cell.Position + cell.Duration - rg.Position;
                        Match mtch = Regex.Match(chunk, @"](\d+)");
                        if (mtch.Length == 0)
                        {
                            mtch = Regex.Match(chunk, @"]\((\d+)\)([\d+\-/*.]*)");
                            rg.Times = int.Parse(mtch.Groups[1].Value);
                            if (mtch.Groups[2].Length != 0)
                            {
                                rg.LastTermModifier = mtch.Groups[2].Value;//.Length != 0 ? BeatCell.Parse(mtch.Groups[2].Value) : 0;
                            }
                        }
                        else
                        {
                            rg.Times = int.Parse(mtch.Groups[1].Value);
                        }

                        // build the group
                        position = BuildRepeatGroup(cell, rg, OpenRepeatGroups, position);
                        // move to outer group if exists
                        chunk = chunk.Substring(chunk.IndexOf(']') + 1);
                    }
                }
                else if (!singleCellRepeat)
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

                // check if its a break, |
                if (chunk.Last() == '|')
                {
                    cell.IsBreak = true;
                }

                cells.Add(cell);
            }

            // set the background tiling
            //SetBackground(position);

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
            if (EditorWindow.Instance.Rows.Count > refIndex)
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
                //if (!int.TryParse(match.Value, out ind))
                //{
                //    ind = refIndex + 1;
                //}
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
                    //refBeat = Regex.Replace(refBeat, @",$", "");
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

        public void Redraw()
        {
            string code = Stringify();
            Reset();
            FillFromBeatCode(code);
        }

        /// <summary>
        /// Update the beat code for this row
        /// </summary>
        public void UpdateBeatCode()
        {
            BeatCode = Stringify();
        }

        /// <summary>
        /// Outputs the string representation of the beat layer from the editor.
        /// </summary>
        /// <returns></returns>
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
                    if (rg.Cells.First.Value == cell && rg.Cells.Where(x => !x.IsReference).Count() > 1)
                    {
                        //OpenRepeatGroups.Push(cell.RepeatGroup);
                        result.Append('[');
                    }
                }
                // get duration or reference ID
                if (string.IsNullOrEmpty(cell.Reference))
                {
                    result.Append(cell.Value);
                }
                else
                {
                    result.Append($"${cell.Reference}");
                }
                // check for source modifier
                if (cell.Source != null && cell.Source != Layer.BaseSourceName)
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
                    Cell[] cells = rg.Cells.Where(x => !x.IsReference).ToArray();
                    if (cells.Last() == cell)
                    {
                        // is single cell rep?
                        if (cells.Length == 1)
                        {
                            result.Append($"({rg.Times})");
                            if (!string.IsNullOrEmpty(rg.LastTermModifier))
                            {
                                result.Append(rg.LastTermModifier);
                            }
                        }
                        else
                        {
                            // multi cell
                            if (!string.IsNullOrEmpty(rg.LastTermModifier))
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
        /// Perform all graphical tasks with initializing a repeat group. Group must have and Times, LastTermMod, Postion, Duration already set.
        /// </summary>
        /// <param name="cell"></param>
        /// <param name="rg"></param>
        /// <param name="openRepeatGroups"></param>
        /// <returns></returns>
        protected double BuildRepeatGroup(Cell cell, RepeatGroup rg, Stack<RepeatGroup> openRepeatGroups, double position)
        {
            //double position = 0;
            RepeatGroups.AddLast(rg);
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
            if (string.IsNullOrEmpty(cell.Reference))
            {
                rg.Canvas.Children.Add(cell.Rectangle);
            }
            // get size of the host rects
            double hostWidth = (rg.Duration - (string.IsNullOrEmpty(cell.Reference) ? cell.Duration : 0)) * EditorWindow.Scale * EditorWindow.BaseFactor;
            if (string.IsNullOrEmpty(cell.Reference))
            {
                hostWidth += (double)EditorWindow.Instance.Resources["cellWidth"];
            }
            // append duplicates of sub-canvas
            for (int i = 0; i < rg.Times - 1; i++)
            {
                VisualBrush duplicate = new VisualBrush(rg.Canvas);
                var dupHost = EditorWindow.Instance.Resources["repeatRectangle"] as Rectangle;
                dupHost.Width = hostWidth;
                // fill with dupe content
                dupHost.Fill = duplicate;
                // do offsets
                Canvas.SetLeft(dupHost, position * EditorWindow.Scale * EditorWindow.BaseFactor);
                Canvas.SetTop(dupHost, (double)EditorWindow.Instance.Resources["rowHeight"] / 2 - (double)EditorWindow.Instance.Resources["cellHeight"] / 2);
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

            position += BeatCell.Parse(rg.LastTermModifier); //* EditorWindow.Scale * EditorWindow.BaseFactor;

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
            double rowHeight = (double)EditorWindow.Instance.Resources["rowHeight"];
            double width = widthBpm * EditorWindow.Scale * EditorWindow.BaseFactor;
            double offset = Offset * EditorWindow.Scale * EditorWindow.BaseFactor;
            BackgroundBrush.Viewport = new System.Windows.Rect(0, rowHeight, width, rowHeight);
            BackgroundBrush.ViewportUnits = BrushMappingMode.Absolute;
            Background.Margin = new System.Windows.Thickness(width + offset, 0, 0, 0);
            Sizer.Width = width;
            // offset the sizer
            //Canvas.SetLeft(Sizer, offset);
        }

        /// <summary>
        /// Change the sizer width and reposition background by an amount in BPM
        /// </summary>
        /// <param name="diff"></param>
        public void ChangeSizerWidthByAmount(double diff)
        {
            double change = diff * EditorWindow.Scale * EditorWindow.BaseFactor;
            double offset = Offset * EditorWindow.Scale * EditorWindow.BaseFactor;
            double rowHeight = (double)EditorWindow.Instance.Resources["rowHeight"];
            Sizer.Width += change;
            // reposition background
            BackgroundBrush.Viewport = new System.Windows.Rect(0, rowHeight, Sizer.Width, rowHeight);
            Background.Margin = new System.Windows.Thickness(Background.Margin.Left + change, 0, 0, 0);
        }

        public void DrawGridLines(string intervalCode)
        {
            double gridCellSize;
            if (BeatCell.TryParse(intervalCode, out gridCellSize))
            {
                gridCellSize *= EditorWindow.BaseFactor * EditorWindow.Scale;
                // get duration of selection and leftmost position
                double duration = 0; // BPM
                double positionBpm = double.MaxValue;
                double maxPostion = -1;
                foreach (Cell cell in Cell.SelectedCells.Cells)
                {
                    duration += cell.Duration;
                    if (cell.Position < positionBpm)
                    {
                        positionBpm = cell.Position;
                        Cell.SelectedCells.FirstCell = cell;
                    }

                    if (cell.Position > maxPostion)
                    {
                        maxPostion = cell.Position;
                        Cell.SelectedCells.LastCell = cell;
                    }
                }
                duration -= Cell.SelectedCells.Cells.Last().Duration;

                // set postion and duration on Selection
                Cell.SelectedCells.Duration = duration;
                Cell.SelectedCells.Position = positionBpm;

                Rectangle sizer = EditorWindow.Instance.GridSizer;
                // set grid cell size
                sizer.Width = gridCellSize;
                Rectangle tick = EditorWindow.Instance.GridTick;
                Rectangle leftGrid = EditorWindow.Instance.GridLeft;
                // set left grid width
                leftGrid.Width = (positionBpm + Offset) * EditorWindow.Scale * EditorWindow.BaseFactor;
                Rectangle rightGrid = EditorWindow.Instance.GridRight;
                // position right grid
                rightGrid.Margin = new System.Windows.Thickness(leftGrid.Width + duration * EditorWindow.Scale * EditorWindow.BaseFactor, 0, 0, 0);
                VisualBrush gridBrush = EditorWindow.Instance.GridBrush;
                // set viewport size
                gridBrush.Viewport = new System.Windows.Rect(0, sizer.Height, gridCellSize, sizer.Height);

                //Canvas.Children.Add(leftGrid);
                //Canvas.Children.Add(rightGrid);
                //EditorWindow.Instance.LayerPanel.Children.Add(rightGrid);
                //Canvas.Children.Add(EditorWindow.Instance.GridCanvas);
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
            // click position in BPM
            double position = e.GetPosition((Grid)sender).X / EditorWindow.Scale / EditorWindow.BaseFactor;
            position -= Offset; // will be negative if inserting before the start

            // is it before or after the current selection?
            if (Cell.SelectedCells.Cells.Any())
            {
                AbstractAction action = null;
                // find the grid line within 10% of increment value of the click
                double increment = BeatCell.Parse(EditorWindow.CurrentIncrement);

                if (position > Cell.SelectedCells.LastCell.Position)
                {
                    // check if position is within the ghosted area of a repeat
                    // TODO: what about last term modifiers? If a new cell is placed in the last term modifier area, what to do?
                    bool outsideRepeat = true;
                    foreach (RepeatGroup rg in RepeatGroups)
                    {
                        if (position > rg.Position + rg.Duration - increment * GridProx && position < rg.Position + rg.Duration * rg.Times)
                        {
                            outsideRepeat = false;
                            break;
                        }
                    }
                    if (outsideRepeat)
                    {
                        // if the new cell will be above the current row
                        if (position > Cells.Last().Position + Cells.Last().Duration - increment * GridProx) // should a cell placed within duration of prev cell maintain overall duration?
                        {
                            action = AddCellAboveRow(position, increment);
                        }
                        else
                        {
                            // cell will be above selection but within the row
                            action = AddCellToRowAboveSelection(position, increment);
                        }
                    }
                }
                else if (position < Cell.SelectedCells.FirstCell.Position)
                {
                    // New cell is below selection
                    // is new cell in the offset area, or is inside the row?
                    // check by seeing if the position is less than the least posible grid line position within the row from the selected cell.
                    if (position < Cell.SelectedCells.FirstCell.Position - ((int)(Cell.SelectedCells.FirstCell.Position / increment) * increment - increment * GridProx))
                    {
                        action = AddCellBelowRow(position, increment);
                    }
                    else
                    {
                        // insert withinin row, below selection
                        // check if it's within a repeat's ghosted zone
                        bool outsideRepeat = true;
                        foreach (RepeatGroup rg in RepeatGroups)
                        {
                            if (position > rg.Position + rg.Duration - increment * GridProx && position < rg.Position + rg.Duration * rg.Times)
                            {
                                outsideRepeat = false;
                                break;
                            }
                        }
                        if (outsideRepeat)
                        {
                            action = AddCellToRowBelowSelection(position, increment);
                        }
                    }
                }

                if (action != null)
                {
                    // add the action to the undo stack
                    EditorWindow.Instance.AddUndoAction(action as IEditorAction);
                    // invalidate beat code
                    BeatCodeIsCurrent = false;
                    // update the referencer rows
                    action.RedrawReferencers();
                }
            }
        }

        /**
         * Test Cases:
         * 
         * 1) Above row
         * 2) Above row where last cell is in a repeat
         * 3) Above row and within the duration of the last cell
         * 4) Above selection and within row
         * 5) ^ Where selection is in a repeat and new cell is not
         * 6) ^ Where selection and new cell are in the same repeat
         * 7) ^ Where new cell is in a repeat group
         * 8) ^ Selection is in a repeat group that is nested
         * 9) ^ new cell is in a repeat group that is nested
         * 10) Below selection and within row
         * 11) ^ Where selection is in a repeat and new cell is not
         * 12) ^ Where selection and new cell are in the same repeat
         * 13) ^ Where new cell is in a repeat group
         * 14) ^ Selection is in a repeat group that is nested
         * 15) ^ new cell is in a repeat group that is nested
         * 16) Below the row, in offset area
         * 17) ^ selection is in a repeat group
         * 18) ^ there is a repeat group between the selection and the start
         */

        protected AbstractAction AddCellAboveRow(double position, double increment)
        {
            double diff = position - Cell.SelectedCells.LastCell.Position;
            int div = (int)(diff / increment);
            double lower = increment * div + GridProx * increment;
            double upper = lower + increment - GridProx * 2 * increment;
            Cell cell = null;
            // use upper or lower grid line?
            if (diff <= lower && diff > 0)
            {
                // use left grid line
                // make new cell
                cell = new Cell(this);
            }
            else if (diff >= upper)
            {
                // use right grid line
                div++;
                // make new cell
                cell = new Cell(this);
            }

            if (cell != null)
            {
                cell.Value = BeatCell.SimplifyValue(EditorWindow.CurrentIncrement);
                cell.Position = Cell.SelectedCells.LastCell.Position + increment * div;
                // set new duration of previous cell
                Cell below = Cells.Last();
                // add to groups and put rectangle in correct canvas
                if (Group.AddToGroups(cell, below))
                {
                    cell.RepeatGroups.Last.Value.Canvas.Children.Add(cell.Rectangle);
                }
                else
                {
                    Canvas.Children.Add(cell.Rectangle);
                }

                // find the value string
                StringBuilder val = new StringBuilder();
                val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));

                HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                foreach (Cell c in Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell))
                {
                    val.Append("+0").Append(BeatCell.Invert(c.Value));
                    // account for rep groups and their LTMs
                    Dictionary<RepeatGroup, int> ltmTimes = new Dictionary<RepeatGroup, int>();
                    foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                    {
                        if (repGroups.Contains(rg)) continue;

                        foreach (Cell ce in rg.Cells)
                        {
                            val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(ce.Value), rg.Times - 1));
                        }
                        foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                        {
                            ltmTimes[kv.Key] = kv.Value * rg.Times;
                        }

                        repGroups.Add(rg);
                        ltmTimes.Add(rg, 1);
                    }
                    foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                    {
                        val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(kv.Key.LastTermModifier), kv.Value));
                    }
                }

                string oldPrevCellValue = below.Value;
                // if last cell is in a rep group, we need to increase the LTM for that group
                if (below.RepeatGroups.Any())
                {
                    oldPrevCellValue = below.RepeatGroups.First.Value.LastTermModifier;
                    // add to the bottom repeat group's LTM
                    below.RepeatGroups.First.Value.LastTermModifier = BeatCell.SimplifyValue(val.ToString());
                }
                else
                {
                    // add to last cell's duration
                    below.Duration = increment * div - (Cells.Last().Position - Cell.SelectedCells.LastCell.Position);
                    val.Append("+0").Append(below.Value);
                    below.Value = BeatCell.SimplifyValue(val.ToString());
                }

                Cells.Add(cell);
                cell.Duration = increment;
                // set new duration of this row
                Duration = cell.Position + cell.Duration;
                SetBackground(Duration);

                // create the action
                AddCell action = new AddCell(cell, below, oldPrevCellValue);

                return action;
            }

            return null;
        }

        protected AbstractAction AddCellToRowAboveSelection(double position, double increment)
        {
            // find nearest grid line
            double lastCellPosition = Cell.SelectedCells.LastCell.Position;
            double diff = position - lastCellPosition;
            int div = (int)(diff / increment);
            double lower = increment * div;// + .1 * increment;
            double upper = lower + increment;// - .2 * increment;

            Cell cell = null;
            //Cell below = null;
            // is lower, or upper in range?
            if (lower + GridProx * increment > diff)
            {
                //below = Cells.TakeWhile(x => x.Position < lastCellPosition + lower).Last();
                cell = new Cell(this);
                cell.Position = lastCellPosition + lower;
            }
            else if (upper - GridProx * increment < diff)
            {
                //below = Cells.TakeWhile(x => x.Position < lastCellPosition + upper).Last();
                cell = new Cell(this);
                cell.Position = lastCellPosition + upper;
                div++;
            }

            if (cell != null)
            {
                int index = Cells.InsertSorted(cell);
                if (index > -1)
                {
                    Cell below = Cells[index - 1];

                    // is new cell placed in the LTM zone of a rep group?
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * GridProx > below.Position + below.Duration))
                    {
                        repWithLtmToMod = rg;
                    }

                    double duration;

                    if (repWithLtmToMod == null)
                    {
                        duration = below.Position + below.Duration - cell.Position;
                        // set duration of preceding cell.
                        below.SetDurationDirectly(below.Duration - duration);
                    }
                    else
                    {
                        // get duration as a slice of the LTM of preceding group
                        duration = repWithLtmToMod.Position + repWithLtmToMod.Duration 
                            * repWithLtmToMod.Times + BeatCell.Parse(repWithLtmToMod.LastTermModifier) 
                            - cell.Position;
                    }
                    
                    cell.SetDurationDirectly(duration);

                    // add to groups and add it's rectangle to appropriate canvas
                    if (Group.AddToGroups(cell, below))
                    {
                        cell.RepeatGroups.Last.Value.Canvas.Children.Add(cell.Rectangle);
                    }
                    else
                    {
                        Canvas.Children.Add(cell.Rectangle);
                    }

                    // determine new value for the below cell
                    StringBuilder val = new StringBuilder();
                    // take and the distance from the end of the selection
                    val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));
                    // subtract the values up to the previous cell
                    HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                    foreach (Cell c in Cells.SkipWhile(x => x != Cell.SelectedCells.LastCell).TakeWhile(x => x != below))
                    {
                        // subtract each value from the total
                        val.Append("+0").Append(BeatCell.Invert(c.Value));
                        // account for rep group repititions.
                        Dictionary<RepeatGroup, int> ltmTimes = new Dictionary<RepeatGroup, int>();
                        foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                        {
                            if (repGroups.Contains(rg)) continue;
                            // don't include a rep group if the end point is included in it.
                            if (cell.RepeatGroups.Contains(rg))
                            {
                                repGroups.Add(rg);
                                continue;
                            }

                            foreach (Cell ce in rg.Cells)
                            {
                                val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(ce.Value), rg.Times - 1));
                            }
                            // get times to count LTMs for each rg
                            foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                            {
                                ltmTimes[kv.Key] = kv.Value * rg.Times;
                            }

                            ltmTimes.Add(rg, 1);
                            repGroups.Add(rg);
                        }
                        // subtract the LTMs
                        foreach (KeyValuePair<RepeatGroup, int> kv in ltmTimes)
                        {
                            val.Append("+0").Append(
                                BeatCell.MultiplyTerms(
                                    BeatCell.Invert(kv.Key.LastTermModifier), kv.Value));
                        }
                    }

                    // get new cells value by subtracting old value of below cell by new value.
                    string newVal = BeatCell.SimplifyValue(val.ToString());
                    cell.Value = BeatCell.Subtract(below.Value, newVal);
                    string oldValue = below.Value;

                    if (repWithLtmToMod == null)
                    {
                        // changing a cell value
                        below.Value = newVal;
                    }
                    else
                    {
                        // changing a LTM value
                        repWithLtmToMod.LastTermModifier = BeatCell.Subtract(repWithLtmToMod.LastTermModifier, newVal);
                    }

                    // create the action
                    AddCell action = new AddCell(cell, below, oldValue);

                    return action;
                }
            }

            return null;
        }

        protected AbstractAction AddCellBelowRow(double position, double increment)
        {
            // in the offset area
            // how many increments back from first cell selected
            double diff = (Cell.SelectedCells.FirstCell.Position + Offset) - (position + Offset);
            int div = (int)(diff / increment);
            // is it closer to lower of upper grid line?
            Cell cell = null;
            if (diff % increment <= increment * GridProx)
            {
                // upper
                cell = new Cell(this);
            }
            else if (diff % increment >= increment * GridProx)
            {
                // lower
                cell = new Cell(this);
                div++;
            }
            if (cell != null)
            {
                // get the value string
                StringBuilder val = new StringBuilder();
                // value of grid lines, the 
                val.Append(BeatCell.MultiplyTerms(EditorWindow.CurrentIncrement, div));

                HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                foreach (Cell c in Cells.TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                {
                    val.Append("+0").Append(BeatCell.Invert(c.Value));
                    // deal with repeat groups
                    Dictionary<RepeatGroup, int> lcmTimes = new Dictionary<RepeatGroup, int>();
                    foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                    {
                        if (repGroups.Contains(rg)) continue;
                        // if the selected cell is in this rep group, we don't want to include repetitions
                        if (Cell.SelectedCells.FirstCell.RepeatGroups.Contains(rg))
                        {
                            repGroups.Add(rg);
                            continue;
                        }
                        foreach (Cell ce in rg.Cells)
                        {
                            val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(ce.Value), rg.Times - 1));
                        }

                        foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                        {
                            lcmTimes[kv.Key] = kv.Value * rg.Times;
                        }
                        repGroups.Add(rg);
                        lcmTimes.Add(rg, 1);
                    }
                    // subtract the LCMs
                    foreach (KeyValuePair<RepeatGroup, int> kv in lcmTimes)
                    {
                        val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(kv.Key.LastTermModifier), kv.Value));
                    }
                }
                cell.Value = BeatCell.SimplifyValue(val.ToString());

                Cells.Insert(0, cell);
                //Cells.AddFirst(cell);
                cell.Duration = (Cell.SelectedCells.FirstCell.Position - div * increment) * -1;
                cell.Position = 0;

                // set new duration of this row
                Duration += cell.Duration;

                Offset -= cell.Duration; //Cell.SelectedCells.FirstCell.Position - div * increment;
                OffsetValue = BeatCell.Subtract(OffsetValue, cell.Value);
                Canvas.Children.Add(cell.Rectangle);

                // add undo action
                return new AddCell(cell);
            }

            return null;
        }

        protected AbstractAction AddCellToRowBelowSelection(double position, double increment)
        {
            double diff = Cell.SelectedCells.FirstCell.Position - position;
            int div = (int)(diff / increment);
            Cell cell = null;
            // is it in range of the left or right grid line?
            if (diff % increment <= increment * GridProx)
            {
                // right
                cell = new Cell(this);
            }
            else if (diff % increment >= increment * (1 - GridProx))
            {
                // left
                cell = new Cell(this);
                div++;
            }

            if (cell != null)
            {
                cell.Position = Cell.SelectedCells.FirstCell.Position - div * increment;
                int index = Cells.InsertSorted(cell);
                if (index > -1)
                {
                    Cell below = Cells[index - 1];

                    // find new duration of below cell
                    //double newDur = Cells.SkipWhile(x => x != below)
                    //    .TakeWhile(x => x != Cell.SelectedCells.FirstCell)
                    //    .Select(x => x.Position)
                    //    .Sum() - div * increment;

                    // see if the cell is being added to a rep group's LTM zone
                    RepeatGroup repWithLtmToMod = null;
                    foreach (RepeatGroup rg in below.RepeatGroups.Where(
                        x => x.Cells.Last.Value == below && position + increment * GridProx > below.Position + below.Duration))
                    {
                        repWithLtmToMod = rg;
                    }

                    double duration;

                    if (repWithLtmToMod == null)
                    {
                        duration = below.Position + below.Duration - cell.Position;
                        below.SetDurationDirectly(below.Duration - duration);
                        //newDur = cell.Position - below.Position;
                    }
                    else
                    {
                        // find slice of the LTM to use as duration
                        duration = repWithLtmToMod.Position + repWithLtmToMod.Duration 
                            * repWithLtmToMod.Times + BeatCell.Parse(repWithLtmToMod.LastTermModifier) 
                            - cell.Position;
                    }

                    //cell.SetDurationDirectly(below.Duration - newDur);
                    //below.SetDurationDirectly(newDur);
                    cell.SetDurationDirectly(duration);

                    // add to groups and add rectangle to correct canvas
                    if (Group.AddToGroups(cell, below))
                    {
                        cell.RepeatGroups.Last.Value.Canvas.Children.Add(cell.Rectangle);
                    }
                    else
                    {
                        Canvas.Children.Add(cell.Rectangle);
                    }

                    // get new value string for below
                    StringBuilder val = new StringBuilder();

                    HashSet<RepeatGroup> repGroups = new HashSet<RepeatGroup>();
                    foreach (Cell c in Cells.SkipWhile(x => x != below).TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                    {
                        if (c == cell) continue; // don't include the new cell
                        // add the cells value
                        val.Append(c.Value).Append('+');
                        // we need to track how many times to multiply each rep group's LTM
                        Dictionary<RepeatGroup, int> ltmFactors = new Dictionary<RepeatGroup, int>();
                        // if there's a rep group, add the repeated sections
                        // what order are rg's in? reverse
                        foreach (RepeatGroup rg in c.RepeatGroups.Reverse())
                        {
                            if (repGroups.Contains(rg)) continue;
                            // don't count reps for groups that contain the selection
                            if (Cell.SelectedCells.FirstCell.RepeatGroups.Contains(rg))
                            {
                                repGroups.Add(rg);
                                continue;
                            }
                            foreach (Cell ce in rg.Cells)
                            {
                                val.Append('0').Append(
                                    BeatCell.MultiplyTerms(ce.Value, rg.Times - 1))
                                    .Append('+');
                            }
                            // increase multiplier of LTMs
                            foreach (KeyValuePair<RepeatGroup, int> kv in ltmFactors)
                            {
                                ltmFactors[kv.Key] = kv.Value * rg.Times;
                            }
                            ltmFactors.Add(rg, 1);
                            // don't add ghost reps more than once
                            repGroups.Add(rg);
                        }
                        // add in all the LTMs from rep groups
                        foreach (KeyValuePair<RepeatGroup, int> kv in ltmFactors)
                        {
                            val.Append('0')
                                .Append(BeatCell.MultiplyTerms(kv.Key.LastTermModifier, kv.Value))
                                .Append('+');
                        }
                    }

                    val.Append('0');
                    val.Append("+0").Append(BeatCell.MultiplyTerms(BeatCell.Invert(EditorWindow.CurrentIncrement), div));
                    cell.Value = BeatCell.Subtract(below.Value, val.ToString());
                    //cell.Value = BeatCell.SimplifyValue(below.Value + '-' + val.ToString());
                    string oldValue;

                    if (repWithLtmToMod == null)
                    {
                        oldValue = below.Value;
                        below.Value = BeatCell.SimplifyValue(val.ToString());
                    }
                    else
                    {
                        oldValue = repWithLtmToMod.LastTermModifier;
                        repWithLtmToMod.LastTermModifier = BeatCell.Subtract(
                            repWithLtmToMod.LastTermModifier, 
                            BeatCell.SimplifyValue(val.ToString()));
                    }

                    // add undo action
                    return new AddCell(cell, below, oldValue);
                }
            }

            return null;
        }
    }
}
