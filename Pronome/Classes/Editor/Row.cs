using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace Pronome.Editor
{
    class Row
    {
        public Layer Layer;
        public List<Cell> Cells;
        public double Offset;
        public LinkedList<MultGroup> MultGroups = new LinkedList<MultGroup>();
        public LinkedList<RepeatGroup> RepeatGroups = new LinkedList<RepeatGroup>();
        public Canvas Canvas;

        public Row(Layer layer)
        {
            Layer = layer;
            Offset = layer.Offset;
            Canvas = EditorWindow.Instance.Resources["rowCanvas"] as Canvas;
            Cells = ParseBeat(layer.ParsedString);
        }

        protected List<Cell> ParseBeat(string beat)
        {
            List<Cell> cells = new List<Cell>();

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
                    cell.MultGroups.AddFirst(OpenMultGroups.Peek());
                    OpenMultGroups.Peek().Cells.Add(cell);
                    OpenMultGroups.Peek().Position = cell.Position;
                }

                // check for opening repeat group
                if (chunk.IndexOf('[') > -1)
                {
                    OpenRepeatGroups.Push(new RepeatGroup() { Row = this });
                    cell.RepeatGroups.AddFirst(OpenRepeatGroups.Peek());
                    OpenRepeatGroups.Peek().Cells.Add(cell);
                    OpenRepeatGroups.Peek().Position = cell.Position;
                }

                // parse the BPM value or get reference
                if (chunk.IndexOf('$') > -1)
                {
                    // get reference
                    string r = Regex.Match(chunk, @"((?<=\$)\d+|s)").Value;
                    cell.Reference = r;
                }
                else
                {
                    // get bpm value
                    string bpm = Regex.Match(chunk, @"[\d./+*\-]+").Value;
                    cell.Duration = BeatCell.Parse(bpm);
                    position += cell.Duration;
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
                    cell.Repeat = new Cell.CellRepeat()
                    {
                        Times = int.Parse(m.Groups[1].Value),
                        LastTermModifier = BeatCell.Parse(m.Groups[2].Value)
                    };
                }

                // check for closing mult group
                if (chunk.IndexOf('}') > -1)
                {
                    MultGroup mg = OpenMultGroups.Pop();
                    mg.Factor = BeatCell.Parse(Regex.Match(chunk, @"(?<=})[\d.+\-/*]+").Value);
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
                        rg.LastTermModifier = BeatCell.Parse(mtch.Groups[2].Value);
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
                    position += rg.LastTermModifier * EditorWindow.Scale * EditorWindow.BaseFactor;
                }
                else
                {
                    // add cell rect to canvas or repeat group sub-canvas
                    if (OpenRepeatGroups.Any())
                    {
                        OpenRepeatGroups.Peek().Canvas.Children.Add(cell.Rectangle);
                    }
                    else
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

            return cells;
        }
    }
}
