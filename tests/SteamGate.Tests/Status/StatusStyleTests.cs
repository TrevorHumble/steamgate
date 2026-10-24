using SteamGate.Core.Status;
using Xunit;

namespace SteamGate.Tests.Status;

public sealed class StatusStyleTests
{
    [Fact]
    public void MatchesApprovedPreview()
    {
        Assert.Equal(new StatusPalette("#fbfcfd", "#d8dee5", "#1b232c", "#5d6b79", "#2c6a8a", "#e2eef4", "#9a5b00", "#fbf0dd", "#2d7a3e", "#e3f2e6", "#d3dbe3", "#ffffff", "#767676", "#4f4f4f", "#0075ff", "#0060df", "#ffffff"), StatusStyle.Light);
        Assert.Equal(new StatusPalette("#1a2027", "#2c353f", "#e6ebf0", "#98a6b4", "#6db3d6", "#1f3340", "#f0b357", "#3a2c16", "#74c88a", "#1b3323", "#2c353f", "#3b3b3b", "#858585", "#a0a0a0", "#99c8ff", "#b3d5ff", "#1a1a1a"), StatusStyle.Dark);

        Assert.Equal("Segoe UI Variable Text", StatusStyle.BodyFontFamily);
        Assert.Equal("Segoe UI Variable Display", StatusStyle.DisplayFontFamily);
        Assert.Equal("Segoe UI", StatusStyle.FallbackFontFamily);

        Assert.Equal(14, StatusStyle.BodyFont);
        Assert.Equal(22, StatusStyle.TitleFont);
        Assert.True(StatusStyle.TitleSemibold);
        Assert.Equal(12, StatusStyle.SectionFont);
        Assert.True(StatusStyle.SectionSemibold);
        Assert.Equal(0.06, StatusStyle.SectionLetterSpacing);
        Assert.Equal(8, StatusStyle.SectionGapBelow);
        Assert.Equal(13, StatusStyle.CountingFont);
        Assert.True(StatusStyle.CountingNameSemibold);
        Assert.Equal(13, StatusStyle.LabelFont);
        Assert.Equal(30, StatusStyle.BigFont);
        Assert.True(StatusStyle.BigSemibold);
        Assert.Equal(1.1, StatusStyle.BigLineHeight);
        Assert.Equal(14, StatusStyle.BigSmallFont);
        Assert.Equal(12, StatusStyle.SubFont);
        Assert.Equal(12, StatusStyle.NoteFont);
        Assert.Equal(13, StatusStyle.ButtonFont);
        Assert.Equal(13, StatusStyle.InputFont);
        Assert.Equal(13, StatusStyle.VerdictFont);
        Assert.Equal(12, StatusStyle.PillFont);
        Assert.Equal(13, StatusStyle.DayLabelFont);
        Assert.Equal(13, StatusStyle.EmptyFont);
        Assert.True(StatusStyle.RowValueSemibold);
        Assert.True(StatusStyle.TabularNumbers);
        Assert.Equal(0.5, StatusStyle.DisabledOpacity);

        Assert.Equal(760, StatusStyle.ContentWidth);
        Assert.Equal(560, StatusStyle.NarrowWidth);
        Assert.Equal(20, StatusStyle.ContentPaddingTop);
        Assert.Equal(24, StatusStyle.ContentPaddingX);
        Assert.Equal(24, StatusStyle.ContentPaddingBottom);
        Assert.Equal(16, StatusStyle.NarrowContentPadding);
        Assert.Equal(24, StatusStyle.SectionGap);
        Assert.Equal(12, StatusStyle.HeadGap);
        Assert.Equal(8, StatusStyle.DotSize);
        Assert.Equal(6, StatusStyle.DotGap);
        Assert.Equal(16, StatusStyle.MeterGap);
        Assert.Equal(14, StatusStyle.MeterPaddingY);
        Assert.Equal(16, StatusStyle.MeterPaddingX);
        Assert.Equal(8, StatusStyle.MeterInnerGap);
        Assert.Equal(6, StatusStyle.MeterRadius);
        Assert.Equal(6, StatusStyle.TrackHeight);
        Assert.Equal(3, StatusStyle.TrackRadius);
        Assert.Equal(6, StatusStyle.RowsRadius);
        Assert.Equal(12, StatusStyle.RowGap);
        Assert.Equal(10, StatusStyle.RowPaddingY);
        Assert.Equal(14, StatusStyle.RowPaddingX);
        Assert.Equal(1, StatusStyle.BorderWidth);
        Assert.Equal(5, StatusStyle.ButtonPaddingY);
        Assert.Equal(12, StatusStyle.ButtonPaddingX);
        Assert.Equal(4, StatusStyle.ButtonRadius);
        Assert.Equal(2, StatusStyle.FocusOutline);
        Assert.Equal(1, StatusStyle.FocusOffset);
        Assert.Equal(10, StatusStyle.EditorPaddingY);
        Assert.Equal(12, StatusStyle.EditorPaddingX);
        Assert.Equal(4, StatusStyle.EditorRadius);
        Assert.Equal(8, StatusStyle.EditorGap);
        Assert.Equal(8, StatusStyle.EditorLineGap);
        Assert.Equal(8, StatusStyle.AddBoxMarginTop);
        Assert.Equal(8, StatusStyle.HeadLinesGap);
        Assert.Equal(8, StatusStyle.ReplyGapAbove);
        Assert.Equal(4, StatusStyle.InputPaddingY);
        Assert.Equal(8, StatusStyle.InputPaddingX);
        Assert.Equal(4, StatusStyle.InputRadius);
        Assert.Equal(80, StatusStyle.NumberInputWidth);
        Assert.Equal(6, StatusStyle.VerdictPaddingY);
        Assert.Equal(10, StatusStyle.VerdictPaddingX);
        Assert.Equal(4, StatusStyle.VerdictRadius);
        Assert.Equal(2, StatusStyle.PillPaddingY);
        Assert.Equal(8, StatusStyle.PillPaddingX);
        Assert.Equal(10, StatusStyle.PillRadius);
        Assert.Equal(6, StatusStyle.DaysGap);
        Assert.Equal(4, StatusStyle.DayLabelGap);
        Assert.Equal(8, StatusStyle.DangerGap);
        Assert.Equal(12, StatusStyle.EmptyPaddingY);
        Assert.Equal(14, StatusStyle.EmptyPaddingX);
        Assert.Equal(12, StatusStyle.NoteParagraphMargin);

        Assert.Equal(760, StatusStyle.StartWidth);
        Assert.Equal(860, StatusStyle.StartHeight);
        Assert.Equal(0.9, StatusStyle.StartMaxShare);
        Assert.Equal(420, StatusStyle.MinWidth);
        Assert.Equal(240, StatusStyle.MinHeight);
        Assert.Equal(40, StatusStyle.SelectExtra);
        Assert.Equal(160, StatusStyle.SelectMinWidth);
        Assert.Equal(420, StatusStyle.SelectMaxWidth);
        Assert.Equal(10, StatusStyle.SelectArrow);
        Assert.Equal(10, StatusStyle.SelectMaxRows);
        Assert.Equal(1.0 / 3, StatusStyle.SelectChevronSpanShare);
        Assert.Equal(1.5, StatusStyle.SelectChevronMinStroke);
        Assert.Equal(1, StatusStyle.SelectListBorder);
        Assert.Equal(13, StatusStyle.CheckSize);
        Assert.Equal(2, StatusStyle.CheckRadius);
        Assert.Equal(4, StatusStyle.CheckMarginLeft);
        Assert.Equal(3, StatusStyle.CheckMarginRight);
        Assert.Equal(3, StatusStyle.CheckMarginY);
        Assert.Equal(2, StatusStyle.CheckTickStroke);
        Assert.Equal(0.24, StatusStyle.CheckTickStartX);
        Assert.Equal(0.52, StatusStyle.CheckTickStartY);
        Assert.Equal(0.43, StatusStyle.CheckTickMiddleX);
        Assert.Equal(0.72, StatusStyle.CheckTickMiddleY);
        Assert.Equal(0.77, StatusStyle.CheckTickEndX);
        Assert.Equal(0.30, StatusStyle.CheckTickEndY);
        Assert.Equal(8, StatusStyle.BigLeftGap);
    }
}
