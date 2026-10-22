namespace SteamGate.Core.Steam;

/// <summary>Tells which skipped manifests are new, so a bad file is logged once and not on every refresh.</summary>
internal sealed class SkipTracker
{
    private HashSet<string> _previous = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _current = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Records a path skipped in this refresh. True when it was not skipped in the previous refresh.</summary>
    public bool Add(string path) => _current.Add(path) && !_previous.Contains(path);

    /// <summary>Call when a refresh is complete: this refresh's paths become the ones to compare against next time.</summary>
    public void EndRefresh()
    {
        _previous = _current;
        _current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
