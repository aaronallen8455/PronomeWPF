using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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

        public Canvas SelectionCanvas = new Canvas();

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

            BaseElement.Children.Add(SelectionCanvas);
            // Add the handlers for the drag select box
            //BaseElement.MouseDown += Grid_MouseDownSelectBox;
            BaseElement.MouseUp += Grid_MouseUpSelectBox;
            BaseElement.MouseMove += Grid_MouseMoveSelectBox;
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
            }
        }

        /// <summary>
        /// Remove the selection box and select cells within it's range. Deselect all if no cells selected
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
                double start = Math.Min(selectorOrigin.X, Canvas.GetLeft(selector)) / EditorWindow.Scale / EditorWindow.BaseFactor;
                double end = start + selector.Width / EditorWindow.Scale / EditorWindow.BaseFactor;
                IEnumerable<Cell> cells = Cells.SkipWhile(x => x.Position < start).TakeWhile(x => x.Position < end);

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
        }

        Point selectorOrigin = new Point();
        /// <summary>
        /// Start drawing the selection box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_MouseDownSelectBox(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // only draw the selection box if control key is down.
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                // get selection origin
                double x = e.GetPosition(BaseElement).X;
                double y = e.GetPosition(BaseElement).Y;
                selectorOrigin.X = x;
                selectorOrigin.Y = y;

                // attach the selection box to the canvas
                SelectionCanvas.CaptureMouse();
                Rectangle selector = EditorWindow.Instance.Resources["boxSelect"] as Rectangle;
                selector.Width = 0;
                selector.Height = 0;
                Canvas.SetTop(selector, y);
                Canvas.SetLeft(selector, x);
                Canvas.SetZIndex(selector, 500);
                SelectionCanvas.Children.Add(selector);
            }
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
                cells.Add(cell);

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
                    string source = Regex.Match(chunk, @"(?<=@)([pP]\d*\.?\d*|\d+|[a-gA-G][b#]?\d+)").Value;
                    if (char.IsNumber(source[0]) && source != "0")
                    {
                        source = WavFileStream.FileNameIndex[int.Parse(source), 0];
                    }
                    cell.Source = source;
                }
                
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
                
                // add cell rect to canvas or repeat group sub-canvas
                if (OpenRepeatGroups.Any())
                {
                    OpenRepeatGroups.Peek().Canvas.Children.Add(cell.Rectangle);
                }
                else if (string.IsNullOrEmpty(cell.Reference)) // cell's rect is not used if it's a reference
                {
                    Canvas.Children.Add(cell.Rectangle);
                }

                // check if its a break, |
                if (chunk.Last() == '|')
                {
                    cell.IsBreak = true;
                }
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
                        source = WavFileStream.GetIndexByName(cell.Source).ToString();//WavFileStream.FileNameIndex[int.Parse(cell.Source), 0];
                    }
                    result.Append($"@{source}");
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

        /// <summary>
        /// Draw the grid lines for selected cells in this row. Also sets the FirstCell and LastCell of selection object.
        /// </summary>
        /// <param name="intervalCode"></param>
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
                foreach (Cell cell in Cell.SelectedCells.Cells.Where(x => !x.IsReference))
                {
                    //else
                    //{
                        duration += cell.Duration;
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
                    duration -= Cell.SelectedCells.LastCell.Duration;
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

            // pass to the select box handler
            Grid_MouseDownSelectBox(sender, e);
        }
    }
}
