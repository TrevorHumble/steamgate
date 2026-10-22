using SteamGate.Core.Enforcement;

namespace SteamGate.Tests;

/// <summary>
/// Tells a test whether it is running inside a Stryker.NET mutation run. Stryker 5 adds a MutantControl type to the assembly it
/// mutates (SteamGate.Core), in a namespace that starts with "Stryker" and carries a generated suffix, so the type is found by name
/// and namespace prefix. Tests that start child processes and wait on them are slow, and they cannot catch a Core mutant because
/// they exercise only the service, so they skip themselves in a mutation run, which runs the whole suite once per mutant.
/// </summary>
internal static class StrykerRun
{
    public static bool Active { get; } = Detect();

    private static bool Detect()
    {
        try
        {
            return typeof(EnforcementPlanner).Assembly.GetTypes()
                .Any(type => type.Name == "MutantControl" && (type.Namespace ?? string.Empty).StartsWith("Stryker", StringComparison.Ordinal));
        }
        catch (System.Reflection.ReflectionTypeLoadException)
        {
            return false;
        }
    }
}
