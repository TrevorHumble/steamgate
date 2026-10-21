using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SteamGate.Core;

/// <summary>
/// Loads and saves one JSON file so that a missing, corrupt, or locked file never loses data
/// and a save never leaves a half-written file.
/// </summary>
public sealed class JsonFileStore<T>
    where T : class
{
    private const int RetryCount = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>How long a locked file is retried before the store gives up. Tests measure against it.</summary>
    internal static readonly TimeSpan GiveUpAfter = RetryCount * RetryDelay;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private readonly object _lock = new();
    private readonly string _path;
    private readonly T _defaults;
    private readonly Func<T, bool> _isValid;

    /// <param name="folder">Must already exist; the store never creates it.</param>
    /// <param name="fileName">File inside the folder.</param>
    /// <param name="defaults">Returned, and written, when the file is missing or corrupt.</param>
    /// <param name="isValid">Returns false for content that parses but must be treated as corrupt (for example a null item or a negative limit).</param>
    public JsonFileStore(string folder, string fileName, T defaults, Func<T, bool> isValid)
    {
        _path = Path.Combine(folder, fileName);
        _defaults = defaults;
        _isValid = isValid;
    }

    /// <summary>
    /// Missing file: returns the defaults and writes them. Invalid JSON: keeps the bad file under a
    /// corrupt-stamped name, writes the defaults, and returns them with Corrupt set. Unreadable file
    /// (locked, folder missing), or a corrupt file that cannot be moved aside: retries, then throws
    /// IOException and overwrites nothing.
    /// </summary>
    public LoadResult<T> Load()
    {
        lock (_lock)
        {
            string text;
            try
            {
                text = WithRetry(() => File.ReadAllText(_path));
            }
            catch (FileNotFoundException)
            {
                // A failed write leaves the file missing; the next Load tries again, so the result is not needed here.
                _ = SaveCore(_defaults);
                return new LoadResult<T>(_defaults, Corrupt: false);
            }

            var value = TryParse(text);
            if (value is not null && _isValid(value))
            {
                return new LoadResult<T>(value, Corrupt: false);
            }

            var corruptPath = CorruptPath();
            WithRetry(() => File.Move(_path, corruptPath));

            // The bad file is already kept aside; if this write fails the file is missing and the next Load rewrites it.
            _ = SaveCore(_defaults);
            return new LoadResult<T>(_defaults, Corrupt: true);
        }
    }

    /// <summary>
    /// Writes atomically. Returns false, and leaves the existing file unchanged, if the disk write keeps
    /// failing, or for a value Load would call corrupt. Never throws.
    /// </summary>
    public bool Save(T value)
    {
        if (value is null || !_isValid(value))
        {
            return false;
        }

        lock (_lock)
        {
            return SaveCore(value);
        }
    }

    private static T? TryParse(string text)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(text, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Runs the action, retrying a locked or briefly unavailable file. A missing file is not retried.
    // A file that stays denied surfaces as IOException, so callers handle one failure type.
    private static TResult WithRetry<TResult>(Func<TResult> action)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException)
                                       && ex is not FileNotFoundException
                                       && attempt < RetryCount)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException(ex.Message, ex);
            }
        }
    }

    private static void WithRetry(Action action) =>
        WithRetry(() =>
        {
            action();
            return true;
        });

    private string CorruptPath()
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var fileName = Path.GetFileName(_path);
        var name = $"{Path.GetFileNameWithoutExtension(fileName)}.corrupt-{stamp}{Path.GetExtension(fileName)}";
        return Path.Combine(Path.GetDirectoryName(_path) ?? string.Empty, name);
    }

    private bool SaveCore(T value)
    {
        var tempPath = _path + ".tmp";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Options));
            return WithRetry(() =>
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes);
                    stream.Flush(true);
                }

                File.Move(tempPath, _path, overwrite: true);
                return true;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            DeleteQuietly(tempPath);
            return false;
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The leftover temp file is harmless; the next save replaces it.
        }
    }
}
