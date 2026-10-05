namespace SharpCAT2.Core.Radio.Contracts;

public enum SessionState
{
    Disconnected,
    Connecting,
    Ready,
    Recovering,
    Faulted,
    Stopping,
    Disposed
}

/// <summary>Pure allowed-transition contract, not a session or runtime state machine.</summary>
/// <remarks>The sole session owner must actually synchronize the connection before declaring
/// Ready. Faulted requires an explicit reconnect request; no operation is automatically replayed.
/// Stopping can finish at Disconnected for a later explicit connect, or at terminal Disposed.</remarks>
public static class SessionTransitions
{
    public static bool IsAllowed(
        SessionState from,
        SessionState to,
        bool synchronizationCompleted = false,
        bool explicitReconnect = false)
    {
        if (!Enum.IsDefined(from) || !Enum.IsDefined(to) || from == to || from == SessionState.Disposed)
            return false;
        if (to == SessionState.Stopping)
            return from != SessionState.Stopping;

        return (from, to) switch
        {
            (SessionState.Disconnected, SessionState.Connecting) => true,
            (SessionState.Connecting, SessionState.Ready) => synchronizationCompleted,
            (SessionState.Connecting, SessionState.Faulted) => true,
            (SessionState.Ready, SessionState.Recovering) => true,
            (SessionState.Ready, SessionState.Faulted) => true,
            (SessionState.Recovering, SessionState.Ready) => synchronizationCompleted,
            (SessionState.Recovering, SessionState.Faulted) => true,
            (SessionState.Faulted, SessionState.Connecting) => explicitReconnect,
            (SessionState.Stopping, SessionState.Disconnected) => true,
            (SessionState.Stopping, SessionState.Disposed) => true,
            _ => false
        };
    }
}
