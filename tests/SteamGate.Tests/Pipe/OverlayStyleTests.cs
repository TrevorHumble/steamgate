using SteamGate.Core.Pipe;
using Xunit;

namespace SteamGate.Tests.Pipe;

public sealed class OverlayStyleTests
{
    [Fact]
    public void MatchesApprovedPreview()
    {
        Assert.Equal("#141A20", OverlayStyle.BackgroundHex);
        Assert.Equal(0.94, OverlayStyle.Opacity);
        Assert.Equal("#F0B357", OverlayStyle.DotHex);
        Assert.Equal(10, OverlayStyle.DotSize);
        Assert.Equal("#F2F5F8", OverlayStyle.TextHex);
        Assert.Equal(15, OverlayStyle.FontSize);
        Assert.Equal(16, OverlayStyle.Margin);
        Assert.Equal(6, OverlayStyle.Radius);
        Assert.Equal(18, OverlayStyle.PaddingX);
        Assert.Equal(12, OverlayStyle.PaddingY);
        Assert.Equal(12, OverlayStyle.DotGap);
        Assert.Equal(16, OverlayStyle.ShadowBlur);
        Assert.Equal(4, OverlayStyle.ShadowOffsetY);
        Assert.Equal(0.4, OverlayStyle.ShadowStrength);
        Assert.Equal(0.55, OverlayStyle.ShadowFalloff);
        Assert.Equal(2, OverlayStyle.ShadowLayerDivisor);
        Assert.Equal("Segoe UI Semibold", OverlayStyle.FontFamily);
    }
}
