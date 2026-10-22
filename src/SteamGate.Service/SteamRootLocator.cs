using System.Security;
using Microsoft.Win32;
using SteamGate.Core.Steam;

namespace SteamGate.Service;

/// <summary>Finds the Steam install folder.</summary>
internal static class SteamRootLocator
{
    private const string FallbackRoot = @"C:\Program Files (x86)\Steam";
    private const string KeyPath = @"SOFTWARE\WOW6432Node\Valve\Steam";

    /// <summary>
    /// The registry InstallPath, or the default folder when the value is missing, unreadable, or not a usable local path.
    /// Standard users can write that value, so it is held to the same rule as a library path.
    /// </summary>
    public static string Locate()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(KeyPath);
            if (key?.GetValue("InstallPath") is string path && LocalPathPolicy.Usable(path, LocalPathPolicy.IsLocal) is { } root)
            {
                return root;
            }
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // An unreadable registry value uses the fallback.
        }

        return FallbackRoot;
    }
}
