using System.Diagnostics.CodeAnalysis;
using SteamGate.Core;

namespace SteamGate.Service;

/// <summary>Hands the worker's latest snapshot to other components. The worker publishes; everything else reads Current.</summary>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Created by the service container.")]
internal sealed class SnapshotHolder
{
    private GateSnapshot _current = GateSnapshot.Empty;

    public GateSnapshot Current => Volatile.Read(ref _current);

    public void Publish(GateSnapshot snapshot) => Volatile.Write(ref _current, snapshot);
}
