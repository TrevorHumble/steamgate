using SteamGate.Core.Pipe;

namespace SteamGate.Core.Status;

/// <summary>
/// Decides which states go to the status screens and keeps the latest one for a screen that connects. Not thread-safe:
/// the pipe server calls it under its own lock. The server offers states only while a status screen is connected, and calls
/// Forget when the last one leaves, so a screen that connects after a gap never gets an old state. A state is the same as the
/// last one when its line is the same.
/// </summary>
public sealed class StatusFeed
{
    private string? _lastText;

    /// <summary>The state to send to a status screen that has just connected, or null when there is none.</summary>
    public StateMessage? Latest { get; private set; }

    /// <summary>True when the state should be posted because its line differs from the last one accepted.</summary>
    public bool Offer(StateMessage state)
    {
        var text = state.Serialize();
        if (text == _lastText)
        {
            return false;
        }

        Latest = state;
        _lastText = text;
        return true;
    }

    /// <summary>Drops the stored state and the text it was compared by.</summary>
    public void Forget()
    {
        Latest = null;
        _lastText = null;
    }
}
