namespace SteamGate.Core.Steam;

/// <summary>The most manifest bytes one refresh may read in all, so a flood of large files cannot stall the service.</summary>
public sealed class ManifestBudget
{
    /// <summary>16 MB.</summary>
    public const long DefaultBytes = 16L * 1024 * 1024;

    private long _remaining;

    public ManifestBudget(long bytes = DefaultBytes)
    {
        _remaining = bytes;
    }

    /// <summary>The bytes still available to read.</summary>
    public long Remaining => _remaining;

    /// <summary>Charges bytes that were actually read; the budget never goes below zero.</summary>
    public void Charge(long bytes)
    {
        _remaining = Math.Max(0, _remaining - bytes);
    }
}
