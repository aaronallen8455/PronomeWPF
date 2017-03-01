using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;

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
            Canvas = Editor.Instance.Resources["rowCanvas"] as Canvas;
            Cells = ParseBeat(layer.ParsedString);
        }

        protected List<Cell> ParseBeat(string beat)
        {
            List<Cell> result = new List<Cell>();

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
                }

                // check for opening repeat group
                if (chunk.IndexOf('[') > -1)
                {
                    OpenRepeatGroups.Push(new RepeatGroup() { Row = this });
                    cell.RepeatGroups.AddFirst(OpenRepeatGroups.Peek());
                    OpenRepeatGroups.Peek().Cells.Add(cell);
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
                }

                // check for closing repeat group, getting times and last term modifier
                if (chunk.IndexOf(']') > -1)
                {
                    RepeatGroup rg = OpenRepeatGroups.Pop();
                    Match m = Regex.Match(chunk, @"](\d+)");
                    if (m.Length == 0)
                    {
                        m = Regex.Match(chunk, @"]\((\d+)\)([\d+\-/*.]*)");
                        rg.Times = int.Parse(m.Groups[1].Value);
                        rg.LastTermModifier = BeatCell.Parse(m.Groups[2].Value);
                    }
                    else
                    {
                        rg.Times = int.Parse(m.Groups[1].Value);
                    }
                }

                // check if its a break, |
                if (chunk.Last() == '|')
                {
                    cell.IsBreak = true;
                }
            }

            return result;
        }
    }
}
