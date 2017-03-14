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

        //public SortedSet<Cell> Cells = new SortedSet<Cell>(delegate (Cell a, Cell b) { return a.Position - b.Position; });

        protected double _offset;
        /// <summary>
        /// Amount of offset in BPM
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

        public Row(Layer layer)
        {
            Layer = layer;
            Canvas = EditorWindow.Instance.Resources["rowCanvas"] as Canvas;
            Offset = layer.Offset;
            //Panel.SetZIndex(Canvas, 20);
            //Canvas.Margin = new System.Windows.Thickness(Offset * EditorWindow.Scale * EditorWindow.BaseFactor, 0, 0, 0);
            Background = EditorWindow.Instance.Resources["rowBackgroundRectangle"] as Rectangle;
            BackgroundBrush = new VisualBrush(Canvas);
            BackgroundBrush.TileMode = TileMode.Tile;
            Background.Fill = BackgroundBrush;
            Canvas.Children.Add(Sizer);
            ParsedBeatResult pbr = ParseBeat(layer.ParsedString);
            Cells = pbr.Cells;
            SetBackground(pbr.Duration);

            // handler for creating new cells on the grid
            BaseElement.MouseLeftButtonDown += BaseElement_MouseLeftButtonDown;

            BaseElement.Background = Brushes.Transparent;
            BaseElement.Children.Add(Canvas);
            BaseElement.Children.Add(Background);
        }

        /// <summary>
        /// Build the cell and group objects based on layer. Also adds all visuals to the canvas.
        /// </summary>
        /// <param name="beat"></param>
        /// <returns></returns>
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
            //e.Handled = true;

            // in BPM
            double position = e.GetPosition((Grid)sender).X / EditorWindow.Scale / EditorWindow.BaseFactor;
            position -= Offset; // will be negative if inserting before the start

            // is it before or after the current selection?
            if (Cell.SelectedCells.Cells.Any())
            {
                // find the grid line within 10% of increment value of the click
                double increment = BeatCell.Parse(EditorWindow.CurrentIncrement);

                if (position > Cell.SelectedCells.LastCell.Position)
                {
                    // if the new cell will be above the current row
                    if (position > Cells.Last.Value.Position)
                    {
                        double diff = position - Cell.SelectedCells.LastCell.Position;
                        int div = (int)(diff / increment);
                        double lower = increment * div + .1 * increment;
                        double upper = lower + increment - .2 * increment;
                        Cell cell = null;
                        // use upper or lower grid line?
                        if (diff <= lower && diff > 0)
                        {
                            // use left grid line
                            // make new cell
                            cell = new Cell(this) { Value = EditorWindow.CurrentIncrement };
                        }
                        else if (diff >= upper)
                        {
                            // use right grid line
                            div++;
                            // make new cell
                            cell = new Cell(this) { Value = EditorWindow.CurrentIncrement };
                        }

                        if (cell != null)
                        {
                            // set new duration of previous cell
                            Cells.Last.Value.Duration = increment * div - (Cells.Last.Value.Position - Cell.SelectedCells.LastCell.Position);
                            cell.Position = Cell.SelectedCells.LastCell.Position + increment * div;
                            Cells.AddLast(cell);
                            Canvas.Children.Add(cell.Rectangle);
                            cell.Duration = increment;
                            //ChangeSizerWidthByAmount(Cells.Last.Value.Duration + increment);
                            // set new duration of this row
                            Duration = cell.Position + cell.Duration;
                        }
                    }
                    else
                    {
                        // cell will be above selection but within the row
                        // find nearest grid line
                        double lastCellPosition = Cell.SelectedCells.LastCell.Position;
                        double diff = position - lastCellPosition;
                        int div = (int)(diff / increment);
                        double lower = increment * div;// + .1 * increment;
                        double upper = lower + increment;// - .2 * increment;

                        Cell cell = null;
                        Cell below = null;
                        // is lower, or upper in range?
                        if (lower + .1 * increment > diff)
                        {
                            below = Cells.TakeWhile(x => x.Position < lastCellPosition + lower).Last();
                            cell = new Cell(this);
                            cell.Position = lastCellPosition + lower;
                        }
                        else if (upper - .1 * increment < diff)
                        {
                            below = Cells.TakeWhile(x => x.Position < lastCellPosition + upper).Last();
                            cell = new Cell(this);
                            cell.Position = lastCellPosition + upper;
                            div++;
                        }

                        if (cell != null)
                        {
                            Cells.AddAfter(Cells.Find(below), cell);
                            double duration = below.Position + below.Duration - cell.Position;
                            below.SetDurationDirectly(below.Duration - duration);
                            Canvas.Children.Add(cell.Rectangle);
                            cell.SetDurationDirectly(duration);

                            // determine new value for the below cell
                            StringBuilder val = new StringBuilder();
                            foreach (Cell c in Cells.TakeWhile(x => x != Cell.SelectedCells.LastCell))
                            {
                                val.Append(c.Value + "+");
                            }
                            val.Append($"{EditorWindow.CurrentIncrement}*{div}");
                            foreach (Cell c in Cells.TakeWhile(x => x != below))
                            {
                                val.Append("-" + c.Value);
                            }

                            // get new cells value by subtracting old value of below cell by new value.
                            cell.Value = $"{below.Value}-{val.ToString()}";
                            below.Value = val.ToString();
                            // TODO: compact the value string
                        }
                    }
                }
                else if (position < Cell.SelectedCells.FirstCell.Position)
                {
                    // New cell is below selection
                    // is new cell in the offset area, or is inside the row?
                    if (position < (int)(Cell.SelectedCells.FirstCell.Position / increment) * increment - increment * .1)
                    {
                        // in the offset area
                        // how many increments back from first cell selected
                        double diff = (Cell.SelectedCells.FirstCell.Position + Offset) - (position + Offset);
                        int div = (int)(diff / increment);
                        // is it closer to lower of upper grid line?
                        Cell cell = null;
                        if (diff % increment <= increment * .1)
                        {
                            // upper
                            cell = new Cell(this);
                        }
                        else if (diff % increment >= increment * .1)
                        {
                            // lower
                            cell = new Cell(this);
                            div++;
                        }
                        if (cell != null)
                        {
                            // get the value string
                            StringBuilder val = new StringBuilder();
                            val.Append($"{EditorWindow.CurrentIncrement}*{div}");
                            foreach(Cell c in Cells.TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                            {
                                val.Append('-').Append(c.Value);
                            }
                            cell.Value = val.ToString();
                            
                            Cells.AddFirst(cell);
                            cell.Duration = (Cell.SelectedCells.FirstCell.Position - div * increment) * -1;
                            cell.Position = 0;

                            // set new duration of this row
                            Duration += cell.Duration;

                            // find new offset
                            //StringBuilder os = new StringBuilder();
                            //foreach (Cell c in Cells.TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                            //{
                            //    os.Append(c.Value).Append('+');
                            //}
                            //Offset = os.Append('-').Append(Offset).ToString();

                            Offset -= cell.Duration; //Cell.SelectedCells.FirstCell.Position - div * increment;
                            Canvas.Children.Add(cell.Rectangle);
                        }
                    }
                    else
                    {
                        // insert withinin row, below selection
                        double diff = Cell.SelectedCells.FirstCell.Position - position;
                        int div = (int)(diff / increment);
                        Cell cell = null;
                        // is it in range of the left or right grid line?
                        if (diff % increment  <= increment * .1)
                        {
                            // right
                            cell = new Cell(this);
                        }
                        else if (diff % increment >= increment * .9)
                        {
                            // left
                            cell = new Cell(this);
                            div++;
                        }

                        if (cell != null)
                        {
                            cell.Position = Cell.SelectedCells.FirstCell.Position - div * increment;
                            Cell below = Cells.TakeWhile(x => x.Position < cell.Position).Last();
                            Cells.AddAfter(Cells.Find(below), cell);
                            // find new duration of below cell
                            double newDur = Cells.SkipWhile(x => x != below)
                                .TakeWhile(x => x != Cell.SelectedCells.FirstCell)
                                .Select(x => x.Position)
                                .Sum() - div * increment;
                            cell.SetDurationDirectly(below.Duration - newDur);
                            below.SetDurationDirectly(newDur);
                            // get new value string for below
                            StringBuilder val = new StringBuilder();
                            foreach (Cell c in Cells.SkipWhile(x => x != below).TakeWhile(x => x != Cell.SelectedCells.FirstCell))
                            {
                                val.Append(c.Value).Append('+');
                            }
                            val.Append('0');
                            val.Append($"-{EditorWindow.CurrentIncrement}*{div}");
                            cell.Value = below.Value + '-' + val.ToString();
                            below.Value = val.ToString();

                            Canvas.Children.Add(cell.Rectangle);
                        }
                    }
                }
            }

            // insert new cell at this position in the row

            // if it's placed before the first cell, adjust the row offset and the position of all subsequent cells
            
            // set the correct duration on the new cell and it's preceding cell if placing cell within beat
            
            
        }
    }
}
