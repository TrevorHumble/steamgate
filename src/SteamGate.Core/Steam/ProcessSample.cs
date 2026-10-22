namespace SteamGate.Core.Steam;

/// <summary>A running process: its id, when it started (so a reused id can be told apart), and its full exe path.</summary>
public sealed record ProcessSample(int Pid, DateTime StartUtc, string ExePath);
