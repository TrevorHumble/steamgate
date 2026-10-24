using System.Drawing.Drawing2D;
using SteamGate.Core.Status;

namespace SteamGate.Status.Ui;

/// <summary>The focus ring of the previews: an outline of FocusOutline pixels, FocusOffset pixels outside the control's border.</summary>
internal static class FocusRing
{
    /// <summary>How far the ring reaches outside the border, in device pixels.</summary>
    public static int Extent(Look look) => look.Px(StatusStyle.FocusOffset) + look.Px(StatusStyle.FocusOutline);

    /// <summary>Draws the ring around a control's body (its border box) with the body's corner radius.</summary>
    public static void Draw(Graphics g, Look look, Rectangle body, int radius)
    {
        var outline = Math.Max(1, look.Px(StatusStyle.FocusOutline));
        var gap = look.Px(StatusStyle.FocusOffset) + (outline / 2f);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(look.Accent, outline);
        using var path = Surface.Rounded(new RectangleF(body.X - gap, body.Y - gap, body.Width + (2 * gap), body.Height + (2 * gap)), radius + gap);
        g.DrawPath(pen, path);
    }
}

/// <summary>A control the keyboard can reach and the mouse can hover: it tracks hover and focus, and leaves room around its body for the focus ring.</summary>
internal abstract class Pressable : Piece
{
    protected Pressable(Look look)
    {
        Look = look;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(ControlStyles.Selectable, true);
    }

    public override int Overhang => FocusRing.Extent(Look);

    protected Look Look { get; }

    protected bool Hover { get; private set; }

    /// <summary>The control's own rectangle, inside the room left for the focus ring.</summary>
    protected Rectangle Body => new(Overhang, Overhang, Width - (2 * Overhang), Height - (2 * Overhang));

    protected bool ShowsFocus => Focused && ShowFocusCues;

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Hover = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }
}
