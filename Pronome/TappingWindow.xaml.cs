using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Pronome.Editor;

namespace Pronome
{
    /// <summary>
    /// Interaction logic for TappingWindow.xaml
    /// </summary>
    public partial class TappingWindow : Window
    {
        protected bool IsListening;

        /// <summary>
        /// Stack of tuples of beatCode, offset holding the state before changes
        /// </summary>
        public static Stack<(string beatCode, string offset, Layer layer)> UndoStack = new Stack<(string beatCode, string offset, Layer layer)>(20);

        /// <summary>
        /// Stack of (beatCode, offset) tuples holding an undo'ed change.
        /// </summary>
        public static Stack<(string beatCode, string offset, Layer layer)> RedoStack = new Stack<(string beatCode, string offset, Layer layer)>(20);

        /// <summary>
        /// The BPM position of each tap captured
        /// </summary>
        protected LinkedList<double> Taps = new LinkedList<double>();

        // TODO: should be a user setting
        /// <summary>
        /// The intervals to check against when quantizing
        /// </summary>
        protected LinkedList<string> QuantizeIntervals = new LinkedList<string>();

        protected Layer Layer;

        const Byte MODE_OVERWRITE = 0;
        const Byte MODE_INSERT = 1;

        public TappingWindow()
        {
            InitializeComponent();
        }

        private void StartCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (targetLayerComboBox == null)
            {
                e.CanExecute = false;
                return;
            }

            e.CanExecute = targetLayerComboBox.SelectedItem != null &&
                           modeComboBox.SelectedItem != null &&
                           QuantizeIntervals.Any() &&
                           !IsListening;
        }

        private void StartCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            IsListening = true;
            listeningMessage.Visibility = Visibility.Visible;

            Layer = Metronome.GetInstance().Layers[targetLayerComboBox.SelectedIndex];
            
            // setup the countdown
            if (countOffCheckBox.IsChecked == true)
            {
                Metronome.GetInstance().SetupCountoff();
            }

            if (Metronome.GetInstance().PlayState != Metronome.State.Playing)
            {
                MainWindow.Instance.playButton_Click(null, null);
            }
        }

        /// <summary>
        /// Tapping complete, perform operations on the beat
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoneCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (IsListening)
            {
                string beatCode = "";
                string offset = "";

                if (modeComboBox.SelectedIndex == MODE_OVERWRITE)
                {
                    if (Taps.Count <= 1)
                    {
                        // need at least 2 to define a cell
                        Close();
                        return;
                    }

                    LinkedList<string> cellDurs = new LinkedList<string>();
                    string last = "0";

                    // get the quantized values
                    foreach (string t in Taps.Select(x => Quantize(x)))
                    {
                        if (t == last) continue;
                        cellDurs.AddLast(BeatCell.Subtract(t, last));
                        last = t;
                    }

                    // determine the offset
                    string length = BeatCell.Subtract(last, cellDurs.First.Value);
                    long cycles = (long)(BeatCell.Parse(cellDurs.First()) / BeatCell.Parse(length));
                    // remove extraneous cycles
                    cellDurs.First.Value = BeatCell.Subtract(cellDurs.First(), BeatCell.MultiplyTerms(length, cycles));

                    // rotate until offset is found
                    offset = cellDurs.First();
                    cellDurs.RemoveFirst();

                    while (BeatCell.Parse(offset) >= BeatCell.Parse(cellDurs.Last.Value))
                    {
                        offset = BeatCell.Subtract(offset, cellDurs.Last.Value);
                        // rotate
                        cellDurs.AddFirst(cellDurs.Last.Value);
                        cellDurs.RemoveLast();
                    }

                    // modify the layer
                    beatCode = string.Join(",", cellDurs);
                    // don't allow empty cells
                    beatCode = beatCode.Replace(",,", ",").Trim(',');
                }
                else if (modeComboBox.SelectedIndex == MODE_INSERT)
                {
                    // how to deal with inserting cells into a rep group?
                    // easy way is to just flatten it out, use raw values from .Beat
                    // ideally we want to insert the cell and retain the original beatCode aspects

                    // retain a int for the current cell index in the beatcode
                    // so when we run a rep group, the index will backtrack as needed

                    // will also need to deal with mult groups
                    // when accumulating cell values, we 'expand' out the multgroups
                    // so that we are dealing with actual values
                    // after all work is done, we 'contract' the multgroups

                    // could use the modeling objects from the editor?

                    // could reuse the editor insert cell operation
                    // only difference is that if we insert a cell into a rep group,
                    // theres some special cases:
                    // 1) if inserting into first or last group of a rep with >2 times,
                    // we break off the cycle being inserted into and decrement
                    // the times of the group
                    // 2) if the group has times == 2 then the group is de-sugared.
                    // this entails adding the LTM to the last cell
                    // 3) if the same insertion is made on each repeat, we can use the
                    // default behaviour of the editor insert action.

                    // if a tap occurs during a reference, we desugar the reference

                    // get the objects representing the beatcode
                    Row row = new Row(Layer, true)
                    {
                        Offset = Layer.Offset,
                        OffsetValue = Layer.ParsedOffset
                    };

                    // get the layers total length
                    double bpmLength = Layer.GetTotalBpmValue();

                    //string offset = "";

                    foreach (double t in Taps)
                    {
                        // check if tap was done in the offset area
                        if (t <= Layer.Offset)
                        {
                            offset = Quantize(t);

                            string cellValue = BeatCell.SimplifyValue(BeatCell.Subtract(Layer.ParsedOffset, offset));

                            if (cellValue != "" && cellValue != "0")
                            {
                                // insert cell at start of beat and change the offset
                                Cell cell = new Cell(row)
                                {
                                    Value = cellValue,
                                    Duration = BeatCell.Parse(cellValue),
                                    Position = 0
                                };

                                // reposition all other cells
                                foreach (Cell c in row.Cells)
                                {
                                    c.Position += cell.Duration;
                                }

                                if (row.Cells.InsertSorted(cell) > -1)
                                {
                                    Layer.ParsedOffset = BeatCell.SimplifyValue(offset);
                                    Layer.Offset = BeatCell.Parse(offset);
                                }
                            }

                            continue;
                        }

                        // get the number of elapsed cycles
                        int cycles = (int)((t - Layer.Offset) / bpmLength);

                        // subtract the elapsed cycles
                        double pos = (t - Layer.Offset) - cycles * bpmLength;
                        string belowValue = Quantize(pos);
                        double qPos = BeatCell.Parse(belowValue); // quantized BPM position double
                        double newCellPosition = qPos;

                        // rep groups that have been traversed and should'nt be touched again
                        HashSet<RepeatGroup> touchedReps = new HashSet<RepeatGroup>();

                        RepeatGroup repWithLtmToInsertInto = null;

                        // the nested repeat groups paired with the number of the repeat in which to insert
                        Dictionary<RepeatGroup, int> repToInsertInto = new Dictionary<RepeatGroup, int>();

                        LinkedList<RepeatGroup> openRepGroups = new LinkedList<RepeatGroup>();

                        int completeReps = 0; // the times run due to values being subtracted at each step

                        Cell belowCell = null;

                        foreach (Cell c in row.Cells)
                        {
                            belowCell = c;
                            // will need to desugar if it's a ref

                            if (c.RepeatGroups.Any())
                            {
                                foreach (RepeatGroup rep in c.RepeatGroups.Where(x => !touchedReps.Contains(x)))
                                {

                                    // see if the total duration of this rep group is shorter than tap position
                                    // then we know that we will be inserting into this rep group at one of it's times. need to know which one.
                                    // rep.Length does not include the times, it's only one cycle
                                    if (qPos < rep.Position + rep.FullDuration * (completeReps + 1))
                                    {
                                        // find the cycle on which the tap is placed
                                        int times = (int)((qPos - rep.Times * rep.Duration * completeReps) / rep.Duration);

                                        repToInsertInto.Add(rep, times);

                                        completeReps *= rep.Times;
                                        completeReps += times;
                                    }
                                    else
                                    {
                                        completeReps *= rep.Times;
                                    }

                                    int reps = (repToInsertInto.ContainsKey(rep) ? completeReps : completeReps - 1);// - collateralRuns.Peek();

                                    // subtract out all the complete reps of this group, except for very last time, which is covered by the cell iteration
                                    bool breakFound = false;
                                    int breakCorrection = 0;
                                    foreach (Cell ce in rep.ExclusiveCells)
                                    {
                                        if (!breakFound) breakFound = ce == rep.BreakCell;
                                        else if (breakCorrection == 0)
                                        {
                                            if (openRepGroups.Any())
                                            {
                                                breakCorrection = openRepGroups.Select(x => x.Times).Aggregate((a, b) => a * b);
                                            }
                                            else breakCorrection = 1;
                                        }

                                        // account for break cells
                                        qPos -= ce.Duration * (reps - (breakFound ? breakCorrection : 0));
                                        belowValue = BeatCell.Subtract(belowValue, BeatCell.MultiplyTerms(ce.GetValueWithMultFactors(), reps));
                                    }

                                    openRepGroups.AddLast(rep);
                                    touchedReps.Add(rep);
                                }

                                // close any open groups that have ended
                                while (openRepGroups.Any() && c.GroupActions.Contains((false, openRepGroups.Last.Value)))
                                {
                                    RepeatGroup last = openRepGroups.Last();
                                    openRepGroups.RemoveLast();
                                    // factor out from global reps times
                                    if (openRepGroups.Any())
                                    {
                                        completeReps /= last.Times;
                                    }
                                    else
                                    {
                                        completeReps = 0;
                                    }

                                    if (qPos < BeatCell.Parse(last.GetLtmWithMultFactor(true)))
                                    {
                                        // should be done with the tap at this point.
                                        repWithLtmToInsertInto = last;
                                        break;

                                    }
                                    else
                                    {
                                        // subtract the ltm
                                        string ltm = last.GetLtmWithMultFactor(true);
                                        qPos -= BeatCell.Parse(ltm);
                                        belowValue = BeatCell.Subtract(belowValue, ltm);
                                    }
                                }

                            }

                            // check if this is the cell that will be above the tap
                            if (qPos < c.Duration)
                            {
                                break;
                            }
                            else if (repWithLtmToInsertInto != null)
                            {
                                break;
                            }

                            // subtract the cell value
                            qPos -= c.Duration;
                            belowValue = BeatCell.Subtract(belowValue, c.GetValueWithMultFactors());
                        }

                        if (!string.IsNullOrEmpty(belowValue))
                        {
                            foreach (var pair in repToInsertInto)
                            {
                                // make the two copies of the first nested rep
                                // we then recurse into the next nested rep
                                // until we reach the rep where the new cell will
                                // exist, then we're done
                                RepeatGroup actual = pair.Key;
                                RepeatGroup before = actual.DeepCopy() as RepeatGroup;
                                RepeatGroup after = actual.DeepCopy() as RepeatGroup;

                                //before.Length *= (double)pair.Value / before.Times;
                                before.Times = pair.Value;

                                //after.Length *= (double)(actual.Times - pair.Value - 1) / after.Times;
                                after.Times = actual.Times - pair.Value - 1;

                                //actual.Length *= 1d / actual.Times;
                                actual.Times = 1;

                                // only the after-copy should have the LTM, or the
                                // actual one if the after copy has 0 times
                                before.LastTermModifier = "";
                                if (after.Times > 0)
                                {
                                    actual.LastTermModifier = "";
                                }

                                // get rid of actual group
                                actual.Cells.First.Value.GroupActions.Remove((true, actual));
                                actual.Cells.Last.Value.GroupActions.Remove((false, actual));
                                foreach (Cell c in actual.Cells)
                                {
                                    c.RepeatGroups.Remove(actual);
                                }

                                // get rid of group if the times is 1
                                if (before.Times == 1)
                                {
                                    before.Cells.First().GroupActions.RemoveFirst();
                                    before.Cells.Last().GroupActions.RemoveLast();
                                    foreach (Cell c in before.Cells)
                                    {
                                        c.RepeatGroups.Remove(before);
                                    }
                                }
                                if (after.Times == 1)
                                {
                                    after.Cells.First().GroupActions.RemoveFirst();
                                    after.Cells.Last().GroupActions.RemoveLast();
                                    foreach (Cell c in after.Cells)
                                    {
                                        c.RepeatGroups.Remove(after);
                                    }
                                }

                                double curOffset = before.Duration * before.Times;
                                // if the before-copy isn't nulled, and the first cell of
                                // this inner nested rep group is also the first cell of it's
                                // containing group, then we need to transfer ownership of the
                                // groupAction to the before-copy. And likewise with the 
                                // after-copy
                                if (before.Times > 0)
                                {
                                    var fstCell = actual.Cells.First();
                                    var gAction = fstCell.GroupActions.First;
                                    var actionsToPrepend = new LinkedList<(bool, Group)>();
                                    // grab all groups that need to be transfered
                                    while (gAction != null && gAction.Value.Item2 != actual)
                                    {
                                        actionsToPrepend.AddLast(gAction.Value);
                                        gAction = gAction.Next;
                                        fstCell.GroupActions.RemoveFirst();
                                    }
                                    // transfer the groups
                                    before.Cells.First().GroupActions = new LinkedList<(bool, Group)>(actionsToPrepend.Concat(before.Cells.First().GroupActions));

                                    // reposition the actual group
                                    foreach (Cell c in actual.Cells)
                                    {
                                        c.Position += curOffset;
                                        foreach (var action in c.GroupActions)
                                        {
                                            if (action.Item1)
                                                action.Item2.Position += curOffset;
                                        }
                                    }

                                    // add the copies to the row
                                    foreach (Cell c in before.Cells)
                                    {
                                        row.Cells.InsertSorted(c);
                                        // no breaks occur in before
                                        c.IsBreak = false;
                                    }
                                }

                                curOffset += actual.Duration;
                                // copy actions from last cell of actual group to the after-copy
                                if (after.Times > 0)
                                {
                                    var lstCell = actual.Cells.Last();
                                    var gAction = lstCell.GroupActions.Last;
                                    var actionsToAppend = new LinkedList<(bool, Group)>();

                                    while (gAction != null && gAction.Value.Item2 != actual)
                                    {
                                        actionsToAppend.AddFirst(gAction.Value);
                                        gAction = gAction.Previous;
                                        lstCell.GroupActions.RemoveLast();
                                    }
                                    after.Cells.Last().GroupActions = new LinkedList<(bool, Group)>(after.Cells.Last().GroupActions.Concat(actionsToAppend));

                                    // remove break from the original group
                                    foreach (Cell c in actual.Cells) c.IsBreak = false;

                                    foreach (Cell c in after.Cells)
                                    {
                                        // reposition cells
                                        c.Position += curOffset;
                                        foreach (var action in c.GroupActions)
                                        {
                                            if (action.Item1)
                                                action.Item2.Position += curOffset;
                                        }

                                        // add cell to the row
                                        row.Cells.InsertSorted(c);
                                    }
                                }
                            }

                            Cell newCell = new Cell(row)
                            {
                                Position = newCellPosition,
                                Source = row.Layer.BaseAudioSource.SoundSource
                            };

                            // add cell to row
                            if (row.Cells.InsertSorted(newCell) > -1)
                            {
                                // add cell to repeat groups
                                RepeatGroup lastRep = null;
                                foreach (var pair in repToInsertInto)
                                {
                                    pair.Key.Cells.AddFirst(newCell);
                                    lastRep = pair.Key;
                                }
                                if (lastRep != null)
                                {
                                    // transfer the group action if it's the last cell
                                    Cell lastCell = lastRep.Cells.Last.Value;
                                    if (lastCell == belowCell)
                                    {
                                        newCell.GroupActions = new LinkedList<(bool, Group)>(lastCell.GroupActions.Where(x => !x.Item1));
                                        lastCell.GroupActions = new LinkedList<(bool, Group)>(lastCell.GroupActions.Where(x => x.Item1));
                                        // move new cell to the back
                                        lastRep.Cells.RemoveFirst();
                                        lastRep.Cells.AddLast(newCell);
                                    }
                                    // it's exclusive for the last rep group
                                    lastRep.ExclusiveCells.AddLast(newCell);
                                }

                                // add cell to mult groups
                                MultGroup lastMult = null;
                                foreach (MultGroup mult in belowCell.MultGroups)
                                {
                                    mult.Cells.AddLast(newCell);
                                    lastMult = mult;
                                }
                                if (lastMult != null)
                                {
                                    // transfer group actions if it's the new last cell
                                    Cell lastCell = lastMult.Cells.Last.Value;
                                    if (lastCell == belowCell)
                                    {
                                        newCell.GroupActions = new LinkedList<(bool, Group)>(lastCell.GroupActions.Where(x => !x.Item1));
                                        lastCell.GroupActions = new LinkedList<(bool, Group)>(lastCell.GroupActions.Where(x => x.Item1));
                                        // move new cell to the back
                                        lastMult.Cells.RemoveFirst();
                                        lastMult.Cells.AddLast(newCell);
                                    }
                                    // will have the same multfactor as former last cell
                                    newCell.MultFactor = lastCell.MultFactor;
                                    lastMult.ExclusiveCells.AddLast(newCell);
                                }

                                if (repWithLtmToInsertInto == null)
                                {
                                    // insert new cell

                                    string newCellValue = BeatCell.Subtract(belowCell.GetValueWithMultFactors(true), belowValue);

                                    newCell.Duration = belowCell.Duration - qPos;

                                    newCell.Value = BeatCell.SimplifyValue(newCell.GetValueDividedByMultFactors(newCellValue, true));
                                    // modify below cell
                                    belowCell.Value = BeatCell.SimplifyValue(belowCell.GetValueDividedByMultFactors(belowValue, true));
                                    belowCell.ResetMultipliedValue();
                                    belowCell.Duration = qPos;
                                }
                                else
                                {
                                    string newCellValue = BeatCell.Subtract(repWithLtmToInsertInto.GetLtmWithMultFactor(true), belowValue);
                                    // insert into LTM
                                    newCell.Duration = BeatCell.Parse(repWithLtmToInsertInto.LastTermModifier) - qPos;

                                    newCell.Value = BeatCell.SimplifyValue(repWithLtmToInsertInto.GetValueDividedByMultFactor(newCellValue, true));
                                    // modify the rep group
                                    repWithLtmToInsertInto.LastTermModifier = BeatCell.SimplifyValue(repWithLtmToInsertInto.GetValueDividedByMultFactor(belowValue, true));
                                    repWithLtmToInsertInto.ResetMultedLtm();

                                }
                            }
                        }
                        else
                        {
                            // tap value was a duplicate, don't alter beatcode
                            continue;
                        }
                    }

                    UndoStack.Push((Layer.ParsedString, Layer.ParsedOffset, Layer));

                    beatCode = row.Stringify();
                    offset = row.OffsetValue;
                }
                // apply changes

                if (offset == string.Empty) offset = "0";

                if (beatCode != Layer.ParsedString || Layer.ParsedOffset != offset)
                {
                    Layer.UI.textEditor.Text = beatCode;
                    Layer.ParsedOffset = offset;
                    Layer.UI.SetOffsetValue(offset);
                    Layer.Offset = BeatCell.Parse(offset);
                    if (Metronome.GetInstance().PlayState == Metronome.State.Stopped)
                    {
                        Layer.ProcessBeatCode(beatCode);
                    }
                    else
                    {
                        // make change while playing
                        Layer.ParsedString = beatCode;
                        Metronome.GetInstance().ExecuteLayerChange(Layer);
                    }

                    if (Metronome.GetInstance().PlayState == Metronome.State.Stopped)
                    {
                        Metronome.GetInstance().TriggerAfterBeatParsed(); // redraw beat graph / bounce if necessary
                    }
                }
            }


            IsListening = false;

            Close();
        }

        private void ComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var layers = Metronome.GetInstance().Layers;

            (sender as ComboBox).ItemsSource = layers.Select(x => "Layer " + (layers.IndexOf(x) + 1).ToString());
        }

        /// <summary>
        /// This is where taps are registered
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsListening)
            {
                Metronome.GetInstance().UpdateElapsedQuarters();
                var x = Metronome.GetInstance().ElapsedQuarters;

                Taps.AddLast(Metronome.GetInstance().ElapsedQuarters);
            }
        }

        protected string Quantize(double value)
        {
            // use doubles to determine which interval is the match
            //double dval = BeatCell.Parse(value);

            var qs = QuantizeIntervals
                .Select(x => (x, BeatCell.Parse(x)))
                .SelectMany(x => {
                    int div = (int)(value / x.Item2);
                    return new[] { (x.Item1, div * x.Item2, div), (x.Item1, (div + 1) * x.Item2, div + 1) };
                });

            string r = "";
            double diff = double.MaxValue;

            foreach ((string interval, double total, int div) in qs)
            {
                double d = Math.Abs(value - total);
                if (d < diff)
                {
                    r = BeatCell.MultiplyTerms(interval, div);
                    diff = d;
                }
            }

            return r;
        }

        private void quantizeIntervalsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string[] chunks = (sender as TextBox).Text.Split(',');

            foreach (string chunk in chunks)
            {
                if (BeatCell.TryParse(chunk, out double bpm))
                {
                    QuantizeIntervals.AddLast(chunk);
                }
                else
                {
                    QuantizeIntervals.Clear();
                    break;
                }
            }
        }
    }
}
