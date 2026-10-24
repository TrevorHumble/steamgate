using SteamGate.Core.Status;

namespace SteamGate.Status.Ui;

/// <summary>A control that can say how big it wants to be, so a box can place it without asking Windows to lay it out.</summary>
internal interface IMeasured
{
    /// <summary>The height it needs at this width.</summary>
    int HeightFor(int width);

    /// <summary>The width it takes when nothing squeezes it.</summary>
    int NaturalWidth();
}

/// <summary>A control with a text baseline, for rows that line their text up.</summary>
internal interface IBaseline
{
    /// <summary>Distance from the top of the control to the baseline of its first line, at its natural width.</summary>
    int Baseline();
}

/// <summary>
/// Whether a control takes part in layout. Windows reports a control as not visible while its window has not been shown yet,
/// so the boxes keep their own flag and the window can be laid out before it appears.
/// </summary>
internal interface IShown
{
    bool Shown { get; set; }
}

/// <summary>Measures any control: its own answer when it is one of ours, else what Windows says it prefers.</summary>
internal static class Metrics
{
    public static bool IsShown(Control control) => control is not IShown shown || shown.Shown;

    public static int HeightFor(Control control, int width) =>
        control is IMeasured measured ? measured.HeightFor(width) : control.GetPreferredSize(Size.Empty).Height;

    public static int NaturalWidth(Control control) =>
        control is IMeasured measured ? measured.NaturalWidth() : control.GetPreferredSize(Size.Empty).Width;

    /// <summary>The control's baseline, or its bottom edge when it has none.</summary>
    public static int Baseline(Control control, int height) => control is IBaseline baseline ? baseline.Baseline() : height;
}

/// <summary>How the screen measures and draws text with Windows' own text renderer, in one place.</summary>
internal static class TextMetrics
{
    /// <summary>A size no text is wider or taller than.</summary>
    public static readonly Size Unbounded = new(10_000_000, 10_000_000);

    /// <summary>Text that may wrap, left and top aligned.</summary>
    public const TextFormatFlags LabelFlags = TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

    /// <summary>One line, no padding.</summary>
    public const TextFormatFlags SingleLineFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine;

    /// <summary>One line centered up and down, cut with an ellipsis when it does not fit (the drop-down's chosen name and list).</summary>
    public const TextFormatFlags ChoiceFlags = SingleLineFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

    /// <summary>The height of one line of this font.</summary>
    public static int GlyphHeight(Font font) => TextRenderer.MeasureText("Ag", font, Unbounded, SingleLineFlags).Height;

    /// <summary>The distance from the top of a line of this font to its baseline.</summary>
    public static int Ascent(Font font)
    {
        var family = font.FontFamily;
        return (int)Math.Round(family.GetCellAscent(font.Style) * font.Size / family.GetEmHeight(font.Style));
    }

    /// <summary>The width of this text on one line.</summary>
    public static int Width(string text, Font font) => TextRenderer.MeasureText(text, font, Unbounded, SingleLineFlags).Width;
}

/// <summary>The parts every control of the screen shares: double-buffered painting, no outer margin, and a flag for taking part in layout.</summary>
internal abstract class Piece : Control, IShown
{
    private bool _shown = true;

    protected Piece()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        TabStop = false;
        Margin = Padding.Empty;
    }

    /// <summary>Pixels drawn outside the layout rectangle on every side (a button's focus ring). The boxes make the control that much bigger and its parent leaves room.</summary>
    public virtual int Overhang => 0;

    public bool Shown
    {
        get => _shown;
        set
        {
            _shown = value;
            Visible = value;
        }
    }

    /// <summary>The width of a 1-pixel border at this scale, never less than one device pixel.</summary>
    protected static int BorderPx(Look look) => Math.Max(1, look.Px(StatusStyle.BorderWidth));
}
