namespace SteamGate.Core.Status;

/// <summary>The colors of one color scheme, as hex text.</summary>
public sealed record StatusPalette(
    string Win,
    string Line,
    string Fg,
    string Muted,
    string Accent,
    string AccentSoft,
    string Wait,
    string WaitSoft,
    string Now,
    string NowSoft,
    string Bar,
    string CheckFill,
    string CheckBorder,
    string CheckBorderHover,
    string CheckedFill,
    string CheckedHover,
    string CheckTick);

/// <summary>
/// The status screen's look, as in the previews Trevor approved (docs/preview-2026-10-21.html and docs/preview-2026-10-22-extras.html).
/// Sizes are 96-DPI pixels; the window scales them by the monitor's DPI factor. The window has no size or color of its own.
/// </summary>
public static class StatusStyle
{
    public static StatusPalette Light { get; } = new(
        Win: "#fbfcfd", Line: "#d8dee5", Fg: "#1b232c", Muted: "#5d6b79", Accent: "#2c6a8a", AccentSoft: "#e2eef4",
        Wait: "#9a5b00", WaitSoft: "#fbf0dd", Now: "#2d7a3e", NowSoft: "#e3f2e6", Bar: "#d3dbe3",
        CheckFill: "#ffffff", CheckBorder: "#767676", CheckBorderHover: "#4f4f4f", CheckedFill: "#0075ff", CheckedHover: "#0060df", CheckTick: "#ffffff");

    public static StatusPalette Dark { get; } = new(
        Win: "#1a2027", Line: "#2c353f", Fg: "#e6ebf0", Muted: "#98a6b4", Accent: "#6db3d6", AccentSoft: "#1f3340",
        Wait: "#f0b357", WaitSoft: "#3a2c16", Now: "#74c88a", NowSoft: "#1b3323", Bar: "#2c353f",
        CheckFill: "#3b3b3b", CheckBorder: "#858585", CheckBorderHover: "#a0a0a0", CheckedFill: "#99c8ff", CheckedHover: "#b3d5ff", CheckTick: "#1a1a1a");

    // Fonts: the body family and the display family (title and big numbers), each falling back to Segoe UI.
    public const string BodyFontFamily = "Segoe UI Variable Text";
    public const string DisplayFontFamily = "Segoe UI Variable Display";
    public const string FallbackFontFamily = "Segoe UI";

    // Text sizes.
    public const int BodyFont = 14;
    public const int TitleFont = 22;
    public const bool TitleSemibold = true;
    public const int SectionFont = 12;
    public const bool SectionSemibold = true;
    public const double SectionLetterSpacing = 0.06;
    public const int SectionGapBelow = 8;
    public const int CountingFont = 13;
    public const bool CountingNameSemibold = true;
    public const int LabelFont = 13;
    public const int BigFont = 30;
    public const bool BigSemibold = true;
    public const double BigLineHeight = 1.1;
    public const int BigSmallFont = 14;
    public const int SubFont = 12;
    public const int NoteFont = 12;
    public const int ButtonFont = 13;
    public const int InputFont = 13;
    public const int VerdictFont = 13;
    public const int PillFont = 12;
    public const int DayLabelFont = 13;
    public const int EmptyFont = 13;
    public const bool RowValueSemibold = true;

    // The meters' big text, the used line, the rule values, and the pill show digits of one width, so they line up.
    public const bool TabularNumbers = true;

    /// <summary>A disabled button is drawn at half opacity.</summary>
    public const double DisabledOpacity = 0.5;

    // Layout.
    public const int ContentWidth = 760;
    public const int NarrowWidth = 560;
    public const int ContentPaddingTop = 20;
    public const int ContentPaddingX = 24;
    public const int ContentPaddingBottom = 24;
    public const int NarrowContentPadding = 16;
    public const int SectionGap = 24;
    public const int HeadGap = 12;
    public const int DotSize = 8;
    public const int DotGap = 6;
    public const int MeterGap = 16;
    public const int MeterPaddingY = 14;
    public const int MeterPaddingX = 16;
    public const int MeterInnerGap = 8;
    public const int MeterRadius = 6;
    public const int TrackHeight = 6;
    public const int TrackRadius = 3;
    public const int RowsRadius = 6;
    public const int RowGap = 12;
    public const int RowPaddingY = 10;
    public const int RowPaddingX = 14;
    public const int BorderWidth = 1;
    public const int ButtonPaddingY = 5;
    public const int ButtonPaddingX = 12;
    public const int ButtonRadius = 4;
    public const int FocusOutline = 2;
    public const int FocusOffset = 1;
    public const int EditorPaddingY = 10;
    public const int EditorPaddingX = 12;
    public const int EditorRadius = 4;
    public const int EditorGap = 8;
    public const int EditorLineGap = 8;
    public const int AddBoxMarginTop = 8;

    /// <summary>The gap between the card 12 lines and between them and their reply line.</summary>
    public const int HeadLinesGap = 8;

    /// <summary>The space above the reply line of the Waiting, Not counted, and Turn off sections. The Rules reply line has none: it sits under the rules note's own 12 px margin, as extras card 5 shows.</summary>
    public const int ReplyGapAbove = 8;

    public const int InputPaddingY = 4;
    public const int InputPaddingX = 8;
    public const int InputRadius = 4;
    public const int NumberInputWidth = 80;
    public const int VerdictPaddingY = 6;
    public const int VerdictPaddingX = 10;
    public const int VerdictRadius = 4;
    public const int PillPaddingY = 2;
    public const int PillPaddingX = 8;
    public const int PillRadius = 10;
    public const int DaysGap = 6;
    public const int DayLabelGap = 4;
    public const int DangerGap = 8;
    public const int EmptyPaddingY = 12;
    public const int EmptyPaddingX = 14;

    // The rules note is a paragraph with the browser's default margin of 1 em at its 12 px size.
    public const int NoteParagraphMargin = 12;

    // The window opens with this client area, each side limited to this share of the monitor's working area.
    public const int StartWidth = 760;
    public const int StartHeight = 860;
    public const double StartMaxShare = 0.9;

    // The smallest the window can be made, frame included (96-DPI pixels).
    public const int MinWidth = 420;
    public const int MinHeight = 240;

    // The add box's drop-down: the width it takes is the longest name plus SelectExtra, kept between the minimum and the maximum; its arrow, and the rows its list shows before it scrolls.
    public const int SelectExtra = 40;
    public const int SelectMinWidth = 160;
    public const int SelectMaxWidth = 420;
    public const int SelectArrow = 10;
    public const int SelectMaxRows = 10;

    // The day check boxes are drawn by the window to look like the check box a browser draws for the previews (which set no accent color):
    // a CheckSize square with CheckRadius corners and a border, the colors in the palette, and a tick of CheckTickStroke pixels whose three points
    // are shares of the square. The previews give the box margins of CheckMarginLeft, CheckMarginRight, and CheckMarginY pixels.
    public const int CheckSize = 13;
    public const int CheckRadius = 2;
    public const int CheckMarginLeft = 4;
    public const int CheckMarginRight = 3;
    public const int CheckMarginY = 3;
    public const int CheckTickStroke = 2;
    public const double CheckTickStartX = 0.24;
    public const double CheckTickStartY = 0.52;
    public const double CheckTickMiddleX = 0.43;
    public const double CheckTickMiddleY = 0.72;
    public const double CheckTickEndX = 0.77;
    public const double CheckTickEndY = 0.30;

    // The arrow is a chevron: its half-width is this share of the arrow size, its stroke is at least this many pixels, and the list has a border this thick.
    public const double SelectChevronSpanShare = 1.0 / 3;
    public const double SelectChevronMinStroke = 1.5;
    public const int SelectListBorder = 1;

    // The gap between the big time-left number and the small word after it.
    public const int BigLeftGap = 8;
}
