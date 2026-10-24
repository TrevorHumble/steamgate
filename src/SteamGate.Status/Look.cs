using Microsoft.Win32;
using SteamGate.Core.Status;

namespace SteamGate.Status;

/// <summary>
/// The status screen's colors, fonts, and scale for one window: the palette of the Windows app mode, every size from StatusStyle scaled by
/// the monitor's DPI factor, and the fonts at those sizes. The window has no number or color of its own; it reads them here.
/// </summary>
internal sealed class Look : IDisposable
{
    private static readonly HashSet<string> InstalledFamilies = [.. FontFamily.Families.Select(family => family.Name)];

    private readonly Dictionary<(bool Display, int Px, bool Semibold), Font> _fonts = [];

    public Look(StatusPalette palette, double scale)
    {
        Palette = palette;
        Scale = scale;
        Win = Parse(palette.Win);
        Line = Parse(palette.Line);
        Fg = Parse(palette.Fg);
        Muted = Parse(palette.Muted);
        Accent = Parse(palette.Accent);
        AccentSoft = Parse(palette.AccentSoft);
        Wait = Parse(palette.Wait);
        WaitSoft = Parse(palette.WaitSoft);
        Now = Parse(palette.Now);
        NowSoft = Parse(palette.NowSoft);
        Bar = Parse(palette.Bar);
        CheckFill = Parse(palette.CheckFill);
        CheckBorder = Parse(palette.CheckBorder);
        CheckBorderHover = Parse(palette.CheckBorderHover);
        CheckedFill = Parse(palette.CheckedFill);
        CheckedHover = Parse(palette.CheckedHover);
        CheckTick = Parse(palette.CheckTick);
    }

    public StatusPalette Palette { get; }

    /// <summary>True when this look uses the dark colors.</summary>
    public bool IsDark => Palette == StatusStyle.Dark;

    public double Scale { get; }

    public Color Win { get; }

    public Color Line { get; }

    public Color Fg { get; }

    public Color Muted { get; }

    public Color Accent { get; }

    public Color AccentSoft { get; }

    public Color Wait { get; }

    public Color WaitSoft { get; }

    public Color Now { get; }

    public Color NowSoft { get; }

    public Color Bar { get; }

    public Color CheckFill { get; }

    public Color CheckBorder { get; }

    public Color CheckBorderHover { get; }

    public Color CheckedFill { get; }

    public Color CheckedHover { get; }

    public Color CheckTick { get; }

    /// <summary>True when Windows' app mode is dark: the Windows counterpart of the preview's prefers-color-scheme.</summary>
    public static bool AppsAreDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>A 96-DPI size in device pixels.</summary>
    public int Px(int value) => (int)Math.Round(value * Scale);

    /// <summary>The body font at this size (96-DPI pixels), scaled.</summary>
    public Font Body(int px, bool semibold = false) => FontOf(display: false, px, semibold);

    /// <summary>The display font (title and big numbers) at this size, scaled.</summary>
    public Font Display(int px, bool semibold = false) => FontOf(display: true, px, semibold);

    /// <summary>The color with this share of it over the backdrop: a half see-through control.</summary>
    public static Color Blend(Color color, Color backdrop, double opacity) => Color.FromArgb(
        (int)Math.Round((color.R * opacity) + (backdrop.R * (1 - opacity))),
        (int)Math.Round((color.G * opacity) + (backdrop.G * (1 - opacity))),
        (int)Math.Round((color.B * opacity) + (backdrop.B * (1 - opacity))));

    public void Dispose()
    {
        foreach (var font in _fonts.Values)
        {
            font.Dispose();
        }

        _fonts.Clear();
    }

    private static Color Parse(string hex) => ColorTranslator.FromHtml(hex);

    private Font FontOf(bool display, int px, bool semibold)
    {
        if (_fonts.TryGetValue((display, px, semibold), out var cached))
        {
            return cached;
        }

        var preferred = display ? StatusStyle.DisplayFontFamily : StatusStyle.BodyFontFamily;
        var family = InstalledFamilies.Contains(preferred) ? preferred : StatusStyle.FallbackFontFamily;
        var style = FontStyle.Regular;
        if (semibold)
        {
            // Windows lists a semibold cut under a name that GDI cuts short ("Segoe UI Variable Display Semib"), so it is found by its start.
            var cut = InstalledFamilies.FirstOrDefault(name => name.StartsWith(family + " Semib", StringComparison.Ordinal));
            if (cut is not null)
            {
                family = cut;
            }
            else
            {
                style = FontStyle.Bold;
            }
        }

        var font = new Font(family, (float)(px * Scale), style, GraphicsUnit.Pixel);
        _fonts[(display, px, semibold)] = font;
        return font;
    }
}
