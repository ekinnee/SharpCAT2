namespace SharpCAT2.ServerLibrary.Radio.Session;

public sealed record RadioSessionOptions
{
    /// <summary>Waiting operations, excluding the one active transaction.</summary>
    public int MaxQueuedOperations { get; init; } = 64;
    public long MaxQueuedBytes { get; init; } = 256 * 1024;
    public int MaxFrameBytes { get; init; } = 4096;
    public TimeSpan MaxOperationTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    internal void Validate()
    {
        if (MaxQueuedOperations <= 0) throw new ArgumentOutOfRangeException(nameof(MaxQueuedOperations));
        if (MaxQueuedBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaxQueuedBytes));
        if (MaxFrameBytes <= 0) throw new ArgumentOutOfRangeException(nameof(MaxFrameBytes));
        // CancellationTokenSource timers have a finite supported interval.
        var maximum = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
        if (MaxOperationTimeout <= TimeSpan.Zero || MaxOperationTimeout > maximum)
            throw new ArgumentOutOfRangeException(nameof(MaxOperationTimeout));
        if (ShutdownTimeout <= TimeSpan.Zero || ShutdownTimeout > maximum)
            throw new ArgumentOutOfRangeException(nameof(ShutdownTimeout));
        ArgumentNullException.ThrowIfNull(TimeProvider);
    }
}
