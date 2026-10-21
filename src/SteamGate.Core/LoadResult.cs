namespace SteamGate.Core;

/// <summary>What a store load produced. Corrupt is true when the file held invalid content, was kept aside, and the defaults replaced it.</summary>
public sealed record LoadResult<T>(T Value, bool Corrupt);
