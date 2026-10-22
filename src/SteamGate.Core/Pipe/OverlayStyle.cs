namespace SteamGate.Core.Pipe;

/// <summary>The overlay's look, as in the preview Trevor approved (docs/preview-2026-10-21.html). Sizes are 96-DPI pixels; the helper scales them.</summary>
public static class OverlayStyle
{
    public const string BackgroundHex = "#141A20";
    /// <summary>The background box's opacity (the text and dot are opaque).</summary>
    public const double Opacity = 0.94;
    public const string DotHex = "#F0B357";
    public const int DotSize = 10;
    public const string TextHex = "#F2F5F8";
    public const int FontSize = 15;
    public const int Margin = 16;
    public const int Radius = 6;
    public const int PaddingX = 18;
    public const int PaddingY = 12;
    public const int DotGap = 12;

    // The soft shadow in the preview: box-shadow 0 4px 16px rgba(0,0,0,.4).
    public const int ShadowBlur = 16;
    public const int ShadowOffsetY = 4;
    public const double ShadowStrength = 0.4;

    // How the shadow's stacked layers share its strength: they overlap, so each carries about this share of an even split,
    // and each is drawn over the ones below it, so its alpha is divided by ShadowLayerDivisor again.
    public const double ShadowFalloff = 0.55;
    public const int ShadowLayerDivisor = 2;

    /// <summary>The overlay's font family: the semibold weight of Segoe UI, as in the preview.</summary>
    public const string FontFamily = "Segoe UI Semibold";
}
