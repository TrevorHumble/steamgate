using System.Security.Principal;

namespace SteamGate.Status;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // One status screen per user per session. A second start ends at once, shows nothing, and leaves the open window where it is.
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? "unknown";
        using var singleInstance = new Mutex(true, @"Local\SteamGate.Status." + sid, out var isFirst);
        if (!isFirst)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        using var window = new StatusForm();
        Application.Run(window);
    }
}
