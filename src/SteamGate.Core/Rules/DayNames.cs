namespace SteamGate.Core.Rules;

/// <summary>Day names as the pipe and the replies write them: English DayOfWeek names, Monday first.</summary>
public static class DayNames
{
    /// <summary>
    /// True when every item equals a DayOfWeek name, ignoring case. A number such as "6" is never a day. The days keep their order and repeats.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> names, out IReadOnlyList<DayOfWeek> days)
    {
        var parsed = new List<DayOfWeek>();
        days = [];
        var valid = Enum.GetNames<DayOfWeek>();
        foreach (var name in names)
        {
            var index = Array.FindIndex(valid, candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return false;
            }

            parsed.Add(Enum.Parse<DayOfWeek>(valid[index]));
        }

        days = parsed;
        return true;
    }

    /// <summary>The days sorted Monday first (Sunday last), without repeats.</summary>
    public static List<DayOfWeek> MondayFirst(IEnumerable<DayOfWeek> days) =>
        [.. days.Distinct().OrderBy(day => day == DayOfWeek.Sunday ? 7 : (int)day)];

    /// <summary>The days as "Friday, Sunday": Monday first, joined with a comma and a space.</summary>
    public static string Format(IEnumerable<DayOfWeek> days) => string.Join(", ", MondayFirst(days).Select(day => day.ToString()));
}
