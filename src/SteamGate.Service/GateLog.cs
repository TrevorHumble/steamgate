using System.Globalization;
using System.Text;
using SteamGate.Core;

namespace SteamGate.Service;

/// <summary>Appends lines to service.log in the data folder, each prefixed with a UTC time. A line that cannot be written is dropped.</summary>
internal sealed class GateLog
{
    private readonly object _lock = new();
    private readonly string _path;

    public GateLog(string folder)
    {
        _path = Path.Combine(folder, "service.log");
    }

    public void Write(string line)
    {
        var stamped = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + line + Environment.NewLine;
        lock (_lock)
        {
            try
            {
                LogRotation.RollIfTooLarge(_path);
                using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                stream.Write(Encoding.UTF8.GetBytes(stamped));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never take the service down; the line is dropped.
            }
        }
    }
}
