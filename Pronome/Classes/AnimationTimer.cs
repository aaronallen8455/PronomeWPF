﻿using System.Diagnostics;

namespace Pronome
{
    public class AnimationTimer
    {
        static protected Stopwatch _stopwatch;

        public static void Init()
        {
            _stopwatch = Stopwatch.StartNew();
        }

        public static void Stop()
        {
            _stopwatch.Reset();
        }

        public static void Start()
        {
            _stopwatch.Start();
        }

        protected double lastTime;

        public AnimationTimer()
        {
            if (_stopwatch == null)
            {
                _stopwatch = new Stopwatch();
            }

            if (_stopwatch.IsRunning)
            {
                lastTime = _stopwatch.ElapsedMilliseconds;
            }
            else
            {
                lastTime = 0;
            }
        }

        public double GetElapsedTime()
        {
            double curTime = _stopwatch.ElapsedMilliseconds;

            double result = curTime - lastTime;

            lastTime = curTime;

            return result / 1000;
        }

        public void Reset()
        {
            lastTime = _stopwatch.ElapsedMilliseconds;
        }
    }
}
