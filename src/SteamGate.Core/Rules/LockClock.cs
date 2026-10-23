namespace SteamGate.Core.Rules;

/// <summary>
/// The countdown's bookkeeping, kept apart from Trevor's limits: when settings were last saved (clock and system uptime),
/// and which Windows boot saved them. Updating it alone is never a change worth a save.
/// </summary>
public sealed record LockClock(DateTime? LastSavedUtc, long? LastSavedUptimeMs, Guid? BootId);
