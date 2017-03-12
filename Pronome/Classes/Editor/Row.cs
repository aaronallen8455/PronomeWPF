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
        /// <summary>
        /// Layer that this row is based on
        /// </summary>
        public Layer Layer;

        /// <summary>
        /// All the cells in this row, including referenced cells
        /// </summary>
        public LinkedList<Cell> Cells = new LinkedList<Cell>();

        /// <summary>
        /// Amount of offset in BPM
        /// </summary>
        public double Offset;

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

        public Row(Layer layer)
        {
            Layer = layer;
            Offset = layer.Offset;
            Canvas = EditorWindow.Instance.Resources["rowCanvas"] as Canvas;
            //Panel.SetZIndex(Canvas, 20);
            Canvas.Margin = new System.Windows.Thickness(Offset * EditorWindow.Scale * EditorWindow.BaseFactor, 0, 0, 0);
            Background = EditorWindow.Instance.Resources["rowBackgroundRectangle"] as Rectangle;
            BackgroundBrush = new VisualBrush(Canvas);
            BackgroundBrush.TileMode = TileMode.Tile;
            Background.Fill = BackgroundBrush;
            Canvas.Children.Add(Sizer);
            ParsedBeatResult pbr = ParseBeat(layer.ParsedString);
            Cells = pbr.Cells;
            SetBackground(pbr.Duration);

            BaseElement.Children.Add(Canvas);
            BaseElement.Children.Add(Background);
        }

        protected ParsedBeatResult ParseBeat(string beat)
        {
            LinkedList<Cell> cells = new LinkedList<Cell>();

            string[] chunks = beat.Split(new char[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
            Stack<MultGroup> OpenMultGroups = new Stack<MultGroup>();
            Stack<RepeatGroup> OpenRepeatGroups = new Stack<RepeatGroup>();
            // BPM value
            double position = 0;// Offset;

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
                else if (OpenMultGroups.Any())
                {
                    cell.MultGroups = new LinkedList<MultGroup>(OpenMultGroups);
                }

                // check for opening repeat group
                if (chunk.IndexOf('[') > -1)
                {
                    OpenRepeatGroups.Push(new RepeatGroup() { Row = this });
                    cell.RepeatGroups = new LinkedList<RepeatGroup>(OpenRepeatGroups);
                    OpenRepeatGroups.Peek().Cells.AddLast(cell);
                    OpenRepeatGroups.Peek().Position = cell.Position;
                }
                else if (RepeatGroups.Any())
                {
                    cell.RepeatGroups = new LinkedList<RepeatGroup>(OpenRepeatGroups);
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

                    foreach (Cell c in pbr.Cells)
                    {
                        cells.AddLast(c);
                    }

                    // draw reference rect
                    cell.ReferenceRectangle = EditorWindow.Instance.Resources["referenceRectangle"] as Rectangle;
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
                bool singleCellRepeat = false;
                if (Regex.IsMatch(chunk, @"\(\d+\)[\d+\-/*.]*"))
                {
                    singleCellRepeat = true;
                    Match m = Regex.Match(chunk, @"\((\d+)\)([\d+\-/*.]*)");
                    //cell.Repeat = new Cell.CellRepeat()
                    //{
                    //    Times = int.Parse(m.Groups[1].Value),
                    //    LastTermModifier = m.Groups[2].Length != 0 ? BeatCell.Parse(m.Groups[2].Value) : 0
                    //};
                    
                    var rg = new RepeatGroup() { Row = this };
                    rg.Cells.AddLast(cell);
                    rg.Position = cell.Position;
                    rg.Duration = cell.Duration;
                    rg.Times = int.Parse(m.Groups[1].Value);
                    rg.LastTermModifier = m.Groups[2].Value;
                    cell.RepeatGroups.AddLast(rg);

                    position = BuildRepeatGroup(cell, rg, OpenRepeatGroups, position);
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

                    // build the group
                    position = BuildRepeatGroup(cell, rg, OpenRepeatGroups, position);
                }
                else
                {
                    // add cell rect to canvas or repeat group sub-canvas
                    if (OpenRepeatGroups.Any())
                    {
                        OpenRepeatGroups.Peek().Canvas.Children.Add(cell.Rectangle);
                    }
                    else if (string.IsNullOrEmpty(cell.Reference) && !singleCellRepeat) // cell's rect is not used if it's a reference
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
            string beat = Metronome.GetInstance().Layers[refIndex].ParsedString;
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

                    // clean out empty cells
                    beat = Regex.Replace(beat, @",,", ",");
                    //refBeat = Regex.Replace(refBeat, @",$", "");
                    beat = beat.Trim(',');
                }
            }

            touchedRefs.Add(refIndex);

            // recurse
            var pbr = ParseBeat(beat);

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
                    rg.Position += position;
                }
                foreach (MultGroup mg in c.MultGroups)
                {
                    mg.Position += position;
                }
            }

            // no longer block this refIndex
            touchedRefs.Remove(refIndex);

            ReferencedLayers.Add(refIndex);

            return pbr;
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
                    if (rg.Cells.First.Value == cell && rg.Cells.Count > 1)
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
                            if (string.IsNullOrEmpty(rg.LastTermModifier))
                            {
                                result.Append(rg.LastTermModifier);
                            }
                        }
                        else
                        {
                            // multi cell
                            if (string.IsNullOrEmpty(rg.LastTermModifier))
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
            }
            else
            {
                // added to row canvas
                Canvas.Children.Add(rg.Canvas);
            }

            rg.Canvas.Children.Add(cell.Rectangle);
            // append duplicates of sub-canvas
            for (int i = 0; i < rg.Times - 1; i++)
            {
                VisualBrush duplicate = new VisualBrush(rg.Canvas);
                var dupHost = EditorWindow.Instance.Resources["repeatRectangle"] as Rectangle;
                // size the rect
                dupHost.Width = (rg.Duration - cell.Duration) * EditorWindow.Scale * EditorWindow.BaseFactor + (double)EditorWindow.Instance.Resources["cellWidth"];
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
            Background.Margin = new System.Windows.Thickness(Background.Margin.Left + change + offset, 0, 0, 0);
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
                foreach (Cell cell in Cell.SelectedCells)
                {
                    duration += cell.Duration;
                    if (cell.Position < positionBpm)
                    {
                        positionBpm = cell.Position;
                    }
                }
                duration -= Cell.SelectedCells.Last().Duration;

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
    }
}
