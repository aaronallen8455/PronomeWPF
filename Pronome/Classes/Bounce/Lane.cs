using System.Collections.Generic;
using System.Windows.Media;
using System.Windows;

namespace Pronome.Bounce
{
    public class Lane
    {
        /// <summary>
        /// Thickness of the top tick
        /// </summary>
        const double topTickWidth = 3.5;

        /// <summary>
        /// Thickness of non-top tick
        /// </summary>
        const double normTickWidth = 2;

        /// <summary>
        /// Width of a lane at the base.
        /// </summary>
        protected double LaneWidth;

        protected Helper Helper;

        protected Layer Layer; // the layer represented by this lane
        protected SolidColorBrush Brush;

        protected double LeftXDiff; // difference in X coords of the two left end points
        protected double RightXDiff;
        protected int Index;

        protected Queue<Tick> Ticks;

        protected double CurInterval; // when this is zero, it's time to cue a new tick
        protected int beatIndex = 0;

        public Lane(Layer layer, Color color, double leftXDiff, double rightXDiff, int index, DrawingContext dc, Helper helper)
        {
            Layer = layer;
            Brush = new SolidColorBrush(color);
            LeftXDiff = leftXDiff;
            RightXDiff = rightXDiff;
            Index = index;
            Helper = helper;

            LaneWidth = (Helper.widthPad * 2 + Helper.width) / Helper.layerCount;

            Ticks = new Queue<Tick>();

            // generate initial ticks and find current interval
            InitTicks(Layer.Offset, dc);
        }

        /// <summary>
        /// Fill the queue with the first batch of ticks.
        /// </summary>
        /// <param name="offset">Amount of bpm before first tick</param>
        protected void InitTicks(double offset, DrawingContext dc)
        {
            Tick.InitConstants(); // calculate the constants used for tick positioning

            double accumulator = offset;
            bool isFirst = true;
            while (accumulator <= Helper.TickQueueSize)
            {
                if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                if (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                {
                    while (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                    {
                        accumulator += Layer.Beat[beatIndex].Bpm;
                        beatIndex++;
                    }
                    continue;
                }
                // create tick
                double factor = Tick.Ease((Helper.TickQueueSize - accumulator) / Helper.TickQueueSize);
                double leftStart = LaneWidth * Index + factor * LeftXDiff;
                double rightStart = LaneWidth * (Index + 1) + factor * RightXDiff;
                var startPoint = new Point(leftStart * Helper.imageRatio + Helper.imageWidthPad, 
                    (Helper.height - Helper.divisionLine * factor) * Helper.imageRatio + Helper.imageHeightPad);
                var endPoint = new Point(rightStart * Helper.imageRatio + Helper.imageWidthPad, 
                    (Helper.height - Helper.divisionLine * factor) * Helper.imageRatio + Helper.imageHeightPad);

                dc.DrawLine(new Pen(Brush, isFirst ? topTickWidth : normTickWidth), startPoint, endPoint);

                isFirst = false;

                var tick = new Tick(LeftXDiff, RightXDiff, leftStart, rightStart, this, 
                    new Pen(Brush, isFirst ? topTickWidth : normTickWidth), Helper.TickQueueSize - accumulator);

                Ticks.Enqueue(tick);

                accumulator += Layer.Beat[beatIndex].Bpm;

                beatIndex++;
                if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                if (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                {
                    while (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
                    {
                        accumulator += Layer.Beat[beatIndex].Bpm;
                        beatIndex++;
                        if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                    }

                }
            }

            // get current interval.
            accumulator -= Layer.Beat[beatIndex == Layer.Beat.Count ? 0 : beatIndex].Bpm;
            CurInterval = Layer.Beat[beatIndex == Layer.Beat.Count ? 0 : beatIndex].Bpm - (Helper.TickQueueSize - accumulator);
        }

        /// <summary>
        /// Remove the expired tick and shift highlight to new top beat.
        /// </summary>
        public void DequeueTick()
        {
            Ticks.Dequeue();

            if (Ticks.Count != 0)
            {
                Tick tick = Ticks.Peek();

                if (tick != null)
                {
                    tick.Pen.Thickness = topTickWidth;
                }
            }

        }

        /// <summary>
        /// Move ticks in queue, decrement the current interval, and add any new ticks.
        /// </summary>
        /// <param name="elapsedTime">Amount of time since last frame</param>
        public void ProcFrame(double elapsedTime, DrawingContext dc)
        {
            // animate ticks
            foreach (Tick tick in Ticks.ToArray())
            {
                tick.Move(elapsedTime, dc);
            }

            CurInterval -= elapsedTime * (Metronome.GetInstance().Tempo / 60);

            // queue new tick if its time
            if (CurInterval <= 0)
            {
                //if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                double factor = Tick.Ease(-CurInterval) / Helper.TickQueueSize;
                var startPoint = new Point(LaneWidth * Index + factor * LeftXDiff, Helper.height);
                var endPoint = new Point(LaneWidth * (Index + 1) + factor * RightXDiff, Helper.height);
                
                Pen pen = new Pen(Brush, Ticks.Count == 0 ? topTickWidth : normTickWidth);

                Ticks.Enqueue(new Tick(LeftXDiff, RightXDiff, startPoint.X, endPoint.X, this, pen, -CurInterval));

                if (beatIndex == Layer.Beat.Count) beatIndex = 0;

                do
                {
                    CurInterval += Layer.Beat[beatIndex].Bpm;
                    beatIndex++;
                    if (beatIndex == Layer.Beat.Count) beatIndex = 0;
                }
                while (Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName);
            }
        }
        /// <summary>
        /// Set state of the drawing to match the position of the playing/paused beat.
        /// </summary>
        /// <param name="elapsedBpm">Number of Bpm that have elapsed.</param>
        public void Sync(double elapsedBpm, DrawingContext dc)
        {
            elapsedBpm -= Layer.Offset;
            elapsedBpm = elapsedBpm % Layer.GetTotalBpmValue();
            beatIndex = 0;

            Ticks.Clear();

            // sync up to elapsed
            double bpm = 0;
            while (bpm <= elapsedBpm || Layer.Beat[beatIndex].SourceName == WavFileStream.SilentSourceName)
            {
                bpm += Layer.Beat[beatIndex].Bpm;
                beatIndex++;
                if (beatIndex == Layer.Beat.Count) beatIndex = 0;
            };

            // fill in initial ticks
            InitTicks(bpm - elapsedBpm, dc);
        }
    }
}
