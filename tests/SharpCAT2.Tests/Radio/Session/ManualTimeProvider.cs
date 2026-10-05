namespace SharpCAT2.Tests.Radio.Session;

internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private readonly List<Timer> _timers = [];
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (_gate) return _ticks; }
    public override DateTimeOffset GetUtcNow() { lock (_gate) return DateTimeOffset.UnixEpoch.AddTicks(_ticks); }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (_gate)
        {
            var timer = new Timer(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }
    }

    public void Advance(TimeSpan duration)
    {
        List<(TimerCallback, object?)> callbacks = [];
        lock (_gate)
        {
            _ticks += duration.Ticks;
            foreach (var timer in _timers.ToArray())
                if (timer.Due <= _ticks)
                {
                    timer.Due = timer.Period == Timeout.InfiniteTimeSpan
                        ? long.MaxValue : _ticks + timer.Period.Ticks;
                    callbacks.Add((timer.Callback, timer.State));
                }
        }
        foreach (var (callback, state) in callbacks) callback(state);
    }

    private sealed class Timer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public long Due { get; set; } = long.MaxValue;
        public TimeSpan Period { get; private set; } = Timeout.InfiniteTimeSpan;
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (owner._gate)
            {
                if (!owner._timers.Contains(this)) return false;
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner._ticks + dueTime.Ticks;
                Period = period;
                return true;
            }
        }
        public void Dispose() { lock (owner._gate) owner._timers.Remove(this); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
