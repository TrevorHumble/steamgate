using Microsoft.Win32;

namespace SteamGate.Service;

/// <summary>
/// Tells one Windows boot from another. The volatile key HKLM\SOFTWARE\SteamGate\Boot is deleted by Windows at every restart, so a
/// Boot\Id value that is still there was written in this boot, whatever the clock says. The lock uses it to count a restart of the
/// service by uptime (which a clock change cannot stretch) and a restart of Windows by the clock.
/// </summary>
internal static class BootMarker
{
    private const string ParentPath = @"SOFTWARE\SteamGate";
    private const string ChildName = "Boot";
    private const string ValueName = "Id";

    /// <summary>This boot's id: the value in the key, or a new one written there. Null when the registry cannot be used.</summary>
    public static Guid? Open()
    {
        try
        {
            // The parent stays after a restart of Windows; only the child is volatile.
            using var parent = Registry.LocalMachine.CreateSubKey(ParentPath, writable: true);
            using var boot = parent.CreateSubKey(ChildName, writable: true, RegistryOptions.Volatile);
            if (boot.GetValue(ValueName) is string text && Guid.TryParse(text, out var existing))
            {
                return existing;
            }

            var id = Guid.NewGuid();
            boot.SetValue(ValueName, id.ToString(), RegistryValueKind.String);
            return id;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return null;
        }
    }
}
