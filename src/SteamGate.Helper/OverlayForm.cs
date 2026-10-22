using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SteamGate.Core.Pipe;

namespace SteamGate.Helper;

/// <summary>
/// The banner drawn over the game. It is a layered window painted with per-pixel alpha, so it has rounded corners and a
/// soft shadow and fades by lowering one opacity value. It is never active, never in Alt-Tab, never in the taskbar, and
/// mouse clicks pass through it to the game. It makes no sound. It is invisible except while a banner is showing.
/// </summary>
internal sealed class OverlayForm : Form
{
    // The upper-case names are the Win32 names, kept so they can be looked up.
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const byte AC_SRC_OVER = 0;
    private const byte AC_SRC_ALPHA = 1;
    private const int ULW_ALPHA = 2;

    private const int WmMouseActivate = 0x0021;
    private const int MaNoActivate = 3;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private static readonly IntPtr HwndTopmost = new(-1);

    // Used when Windows reports no primary screen.
    private static readonly Rectangle FallbackScreen = new(0, 0, 1920, 1080);

    private static readonly TimeSpan FadeStep = TimeSpan.FromMilliseconds(33);

    private readonly System.Windows.Forms.Timer _fadeTimer = new() { Interval = (int)FadeStep.TotalMilliseconds };
    private readonly Stopwatch _sinceShown = new();
    private Bitmap? _banner;
    private Point _topLeft;

    public OverlayForm()
    {
        ShowInTaskbar = false;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Location = PrimaryBounds().Location;
        _fadeTimer.Tick += (_, _) => Advance();
    }

    // Never take focus from the game.
    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED | WS_EX_TRANSPARENT;
            return parameters;
        }
    }

    /// <summary>Creates the window handle without showing anything, so messages can be sent to this form from other threads.</summary>
    public void PrepareHandle() => _ = Handle;

    /// <summary>Draws the text in the banner at the bottom right of the primary screen and starts its hold and fade. A banner already showing is replaced.</summary>
    public void ShowBanner(string text)
    {
        var scale = DeviceDpi / 96.0;
        var screen = PrimaryBounds();
        var margin = (int)Math.Round(OverlayStyle.Margin * scale);
        _banner?.Dispose();
        var rendered = Render(text, scale, screen.Width - (2 * margin));
        _banner = rendered.Image;

        // The box sits at the bottom right with the margin; the bitmap is larger than the box by the shadow's room.
        _topLeft = new Point(screen.Right - margin - rendered.Box.Right, screen.Bottom - margin - rendered.Box.Bottom);

        _sinceShown.Restart();
        Present(1.0);
        SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        _fadeTimer.Start();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = MaNoActivate;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fadeTimer.Dispose();
            _banner?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Rectangle PrimaryBounds() => Screen.PrimaryScreen?.Bounds ?? FallbackScreen;

    private void Advance()
    {
        var opacity = OverlayFade.OpacityAt(_sinceShown.Elapsed);
        if (opacity <= 0.0)
        {
            _fadeTimer.Stop();

            // The window was never shown through Form.Show, so Hide would not hide it.
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_HIDEWINDOW);
            return;
        }

        // Games can take the top spot; ask for it again without taking focus.
        SetWindowPos(Handle, HwndTopmost, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        Present(opacity);
    }

    // The painted bitmap and where the box sits inside it (the rest is shadow room).
    private sealed record Rendered(Bitmap Image, Rectangle Box);

    // Paints the banner and shadow on a transparent bitmap. Sizes are 96-DPI values multiplied by scale; the text is cut to maxWidth.
    private static Rendered Render(string text, double scale, int maxWidth)
    {
        var pad = (int)Math.Ceiling((OverlayStyle.ShadowBlur + OverlayStyle.ShadowOffsetY) * scale);
        var dot = (int)Math.Round(OverlayStyle.DotSize * scale);
        var gap = (int)Math.Round(OverlayStyle.DotGap * scale);
        var paddingX = (int)Math.Round(OverlayStyle.PaddingX * scale);
        var paddingY = (int)Math.Round(OverlayStyle.PaddingY * scale);
        var radius = (int)Math.Round(OverlayStyle.Radius * scale);

        using var font = new Font(OverlayStyle.FontFamily, (float)(OverlayStyle.FontSize * scale), FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
            LineAlignment = StringAlignment.Center,
        };

        using var probe = new Bitmap(1, 1);
        using var probeGraphics = Graphics.FromImage(probe);
        var measured = probeGraphics.MeasureString(text, font, int.MaxValue, format);
        var maxText = maxWidth - (2 * paddingX) - dot - gap;
        var textWidth = Math.Min((int)Math.Ceiling(measured.Width), Math.Max(maxText, 1));
        var textHeight = (int)Math.Ceiling(measured.Height);

        var boxSize = new Size((2 * paddingX) + dot + gap + textWidth, (2 * paddingY) + Math.Max(dot, textHeight));
        var bitmap = new Bitmap(boxSize.Width + (2 * pad), boxSize.Height + (2 * pad), PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
        g.Clear(Color.Transparent);

        var box = new Rectangle(pad, pad, boxSize.Width, boxSize.Height);

        // Soft shadow: stacked, slightly growing translucent rounded rectangles, moved down a little.
        var layers = (int)Math.Ceiling(OverlayStyle.ShadowBlur * scale);
        var layerAlpha = Math.Max(1, (int)Math.Round(255 * OverlayStyle.ShadowStrength / (layers * OverlayStyle.ShadowFalloff)) / OverlayStyle.ShadowLayerDivisor);
        for (var grow = layers; grow >= 1; grow--)
        {
            var shadow = Rectangle.Inflate(box, grow, grow);
            shadow.Offset(0, (int)Math.Round(OverlayStyle.ShadowOffsetY * scale));
            using var path = RoundedRectangle(shadow, radius + grow);
            using var brush = new SolidBrush(Color.FromArgb(layerAlpha, 0, 0, 0));
            g.FillPath(brush, path);
        }

        // The box replaces the shadow under it, as in the preview, then takes its translucent background.
        g.CompositingMode = CompositingMode.SourceCopy;
        using (var path = RoundedRectangle(box, radius))
        using (var fill = new SolidBrush(Color.FromArgb((int)Math.Round(255 * OverlayStyle.Opacity), ColorTranslator.FromHtml(OverlayStyle.BackgroundHex))))
        {
            g.FillPath(fill, path);
        }

        g.CompositingMode = CompositingMode.SourceOver;
        using (var dotBrush = new SolidBrush(ColorTranslator.FromHtml(OverlayStyle.DotHex)))
        {
            g.FillEllipse(dotBrush, box.Left + paddingX, box.Top + ((box.Height - dot) / 2), dot, dot);
        }

        using var textBrush = new SolidBrush(ColorTranslator.FromHtml(OverlayStyle.TextHex));
        var textBox = new RectangleF(box.Left + paddingX + dot + gap, box.Top, textWidth, box.Height);
        g.DrawString(text, font, textBrush, textBox, format);
        return new Rendered(bitmap, box);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(1, radius * 2);
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseAllFigures();
        return path;
    }

    // Hands the bitmap to Windows with the current opacity. Windows does the compositing, so no paint event is involved.
    private void Present(double opacity)
    {
        if (_banner is null)
        {
            return;
        }

        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmapHandle = _banner.GetHbitmap(Color.FromArgb(0));
        var previous = SelectObject(memoryDc, bitmapHandle);
        try
        {
            var size = new NativeSize { Width = _banner.Width, Height = _banner.Height };
            var source = new NativePoint { X = 0, Y = 0 };
            var target = new NativePoint { X = _topLeft.X, Y = _topLeft.Y };
            var blend = new BlendFunction { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = (byte)Math.Round(255 * opacity), AlphaFormat = AC_SRC_ALPHA };
            _ = UpdateLayeredWindow(Handle, screenDc, ref target, ref size, memoryDc, ref source, 0, ref blend, ULW_ALPHA);
        }
        finally
        {
            _ = SelectObject(memoryDc, previous);
            _ = DeleteObject(bitmapHandle);
            _ = DeleteDC(memoryDc);
            _ = ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct BlendFunction
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        IntPtr window,
        IntPtr destinationDc,
        ref NativePoint destinationPosition,
        ref NativeSize size,
        IntPtr sourceDc,
        ref NativePoint sourcePosition,
        int colorKey,
        ref BlendFunction blend,
        int flags);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr CreateCompatibleDC(IntPtr dc);

    [DllImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr gdiObject);

    [DllImport("gdi32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr dc);
}
