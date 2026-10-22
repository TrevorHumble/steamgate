using System.Security.Principal;

namespace SteamGate.Helper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // One helper per user: a second user's helper in the same session is not blocked.
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? "unknown";
        using var singleInstance = new Mutex(true, @"Local\SteamGate.Helper." + sid, out var isFirst);
        if (!isFirst)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        using var context = new HelperContext();
        Application.Run(context);
        GC.KeepAlive(singleInstance);
    }
}
