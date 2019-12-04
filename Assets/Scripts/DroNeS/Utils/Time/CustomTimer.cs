using System;
using System.Diagnostics;

namespace DroNeS.Utils.Time
{
    public struct CustomTimer
    {
        public const long Frequency = 10000000;

        private long _elapsed;
        private long _started;

        private TimeSpan Elapsed => TimeSpan.FromTicks(ElapsedTicks / (Frequency / TimeSpan.TicksPerSecond));

        public long ElapsedMilliseconds => ElapsedTicks /(Frequency / 1000);

        public float ElapsedSeconds => ElapsedMilliseconds * 0.001f;
        private long ElapsedTicks => IsRunning ? Stopwatch.GetTimestamp() - _started + _elapsed : _elapsed;
        public bool IsRunning { get; private set; }

        public void Reset()
        {
            _elapsed = 0;
            IsRunning = false;
        }

        public static CustomTimer Get()
        {
            return new CustomTimer().Start();
        }

        public CustomTimer Start()
        {
            if (IsRunning) return this;
            _started = Stopwatch.GetTimestamp();
            IsRunning = true;
            return this;
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _elapsed += Stopwatch.GetTimestamp() - _started;
            if (_elapsed < 0) _elapsed = 0;
            IsRunning = false;
        }

        public void Restart()
        {
            _started = Stopwatch.GetTimestamp();
            _elapsed = 0;
            IsRunning = true;
        }
    }
}
