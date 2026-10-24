using System.Drawing.Drawing2D;
using SteamGate.Core.Status;

namespace SteamGate.Status.Ui;

/// <summary>
/// A container that places its own children. Windows' layout is not used: each box answers HeightFor(width) and NaturalWidth(),
/// and sets its children's bounds from those answers, so the approved layout comes out the same at every size and scale.
/// </summary>
internal abstract class Box : Piece, IMeasured
{
    public abstract int HeightFor(int width);

    public abstract int NaturalWidth();

    /// <summary>Lays out this box and every box inside it, top down. Call after content or width changed.</summary>
    public void RelayoutDeep()
    {
        PerformLayout();
        foreach (var child in Items())
        {
            if (child is Box box)
            {
                box.RelayoutDeep();
            }
        }
    }

    /// <summary>
    /// How far this box reaches outside its layout rectangle on every side: as far as the most overhanging child (a focus ring) reaches past
    /// the box's own padding, or what the box itself draws outside (OwnOverhang). A box that holds such a child grows by this much, as
    /// every parent does for it, so nothing is cut off and no caller has to remember. Padding is the design padding; the layout adds the overhang.
    /// </summary>
    public override int Overhang => Math.Max(OwnOverhang, ChildOverhang());

    /// <summary>What this box draws outside its own rectangle, apart from its children.</summary>
    protected virtual int OwnOverhang => 0;

    /// <summary>Where the first child's content area starts, inside the room left for the overhang.</summary>
    protected int InnerLeft => Padding.Left + Overhang;

    protected int InnerTop => Padding.Top + Overhang;

    /// <summary>The width the layout has to work with: this box's width less its padding and the overhang on both sides.</summary>
    protected int InnerWidth => Math.Max(0, Width - Padding.Horizontal - (2 * Overhang));

    private int ChildOverhang()
    {
        var room = Math.Min(Math.Min(Padding.Left, Padding.Top), Math.Min(Padding.Right, Padding.Bottom));
        return Math.Max(0, Items().Select(child => child is Piece piece ? piece.Overhang : 0).DefaultIfEmpty(0).Max() - room);
    }

    // Sets a child's rectangle, grown by its overhang so what it draws outside still fits in this box.
    protected static void Place(Control child, int x, int y, int width, int height)
    {
        var overhang = child is Piece piece ? piece.Overhang : 0;
        child.SetBounds(x - overhang, y - overhang, width + (2 * overhang), height + (2 * overhang));
    }

    protected IEnumerable<Control> Items() => Controls.Cast<Control>().Where(Metrics.IsShown);

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        PerformLayout();
    }
}

/// <summary>Children one under another, each as wide as the box, in the order they were added. A child's margin counts; margins do not collapse.</summary>
internal class VBox : Box
{
    public int Gap { get; set; }

    /// <summary>When set, the width this box takes when nothing squeezes it, instead of its widest child's.</summary>
    public int? FixedNaturalWidth { get; set; }

    public override int HeightFor(int width)
    {
        var inner = Math.Max(0, width - Padding.Horizontal);
        var total = Padding.Vertical;
        var first = true;
        foreach (var child in Items())
        {
            total += (first ? 0 : Gap) + child.Margin.Vertical + Metrics.HeightFor(child, Math.Max(0, inner - child.Margin.Horizontal));
            first = false;
        }

        return total;
    }

    public override int NaturalWidth() =>
        FixedNaturalWidth ?? (Padding.Horizontal + Items().Select(child => child.Margin.Horizontal + Metrics.NaturalWidth(child)).DefaultIfEmpty(0).Max());

    protected override void OnLayout(LayoutEventArgs levent)
    {
        var inner = InnerWidth;
        var y = InnerTop;
        var first = true;
        foreach (var child in Items())
        {
            y += first ? 0 : Gap;
            first = false;
            var width = Math.Max(0, inner - child.Margin.Horizontal);
            var height = Metrics.HeightFor(child, width);
            Place(child, InnerLeft + child.Margin.Left, y + child.Margin.Top, width, height);
            y += child.Margin.Vertical + height;
        }
    }
}

/// <summary>How a line of a FlowBox lines its items up.</summary>
internal enum FlowAlign
{
    Center,
    Baseline,
}

/// <summary>
/// Children left to right at their natural width, wrapping onto a new line when the next one does not fit. With SpaceBetween the
/// items of each line spread to both edges (a line of one item stays at the left). The CSS flex row of the approved previews.
/// </summary>
internal sealed class FlowBox : Box, IBaseline
{
    public int Gap { get; set; }

    public int RowGap { get; set; }

    public FlowAlign Align { get; set; } = FlowAlign.Center;

    public bool SpaceBetween { get; set; }

    public override int HeightFor(int width) => Lay(Math.Max(0, width - Padding.Horizontal), apply: false) + Padding.Vertical;

    public override int NaturalWidth()
    {
        var items = Items().ToList();
        return Padding.Horizontal + items.Sum(child => child.Margin.Horizontal + Metrics.NaturalWidth(child)) + (Gap * Math.Max(0, items.Count - 1));
    }

    /// <summary>The baseline of the first line, from the top of the box.</summary>
    public int Baseline()
    {
        var lines = Arrange(Math.Max(0, NaturalWidth() - Padding.Horizontal));
        return lines.Count == 0 ? Height : Padding.Top + lines[0].Baseline;
    }

    protected override void OnLayout(LayoutEventArgs levent) => Lay(InnerWidth, apply: true);

    private sealed record Slot(Control Control, int Width, int Height, int Ascent);

    private sealed record Line(List<Slot> Slots, int Height, int Baseline);

    // Greedy line breaking at natural widths. An item wider than the box is squeezed to the box and measured again at that width.
    private List<Line> Arrange(int innerWidth)
    {
        var lines = new List<Line>();
        var current = new List<Slot>();
        var used = 0;
        foreach (var child in Items())
        {
            var natural = Math.Min(Metrics.NaturalWidth(child), Math.Max(0, innerWidth - child.Margin.Horizontal));
            var height = Metrics.HeightFor(child, natural);
            var slot = new Slot(child, natural, height, Metrics.Baseline(child, height));
            var needed = child.Margin.Horizontal + natural;
            if (current.Count > 0 && used + Gap + needed > innerWidth)
            {
                lines.Add(Finish(current));
                current = [];
                used = 0;
            }

            used += (current.Count > 0 ? Gap : 0) + needed;
            current.Add(slot);
        }

        if (current.Count > 0)
        {
            lines.Add(Finish(current));
        }

        return lines;
    }

    private Line Finish(List<Slot> slots)
    {
        // Vertical margins play no part in a flow: items only have horizontal margins.
        if (Align == FlowAlign.Baseline)
        {
            var ascent = slots.Max(slot => slot.Ascent);
            return new Line(slots, slots.Max(slot => ascent - slot.Ascent + slot.Height), ascent);
        }

        var tallest = slots.Max(slot => slot.Height);
        return new Line(slots, tallest, ((tallest - slots[0].Height) / 2) + slots[0].Ascent);
    }

    // Returns the content height; with apply, also sets every child's bounds.
    private int Lay(int innerWidth, bool apply)
    {
        var lines = Arrange(innerWidth);
        var y = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (apply)
            {
                var widths = line.Slots.Sum(slot => slot.Control.Margin.Horizontal + slot.Width);
                var extra = SpaceBetween && line.Slots.Count > 1 ? Math.Max(0, innerWidth - widths - (Gap * (line.Slots.Count - 1))) / (line.Slots.Count - 1) : 0;
                var x = 0;
                foreach (var slot in line.Slots)
                {
                    var top = Align == FlowAlign.Baseline ? line.Baseline - slot.Ascent : (line.Height - slot.Height) / 2;
                    Place(slot.Control, InnerLeft + x + slot.Control.Margin.Left, InnerTop + y + top, slot.Width, slot.Height);
                    x += slot.Control.Margin.Horizontal + slot.Width + Gap + extra;
                }
            }

            y += line.Height + (index < lines.Count - 1 ? RowGap : 0);
        }

        return y;
    }
}

/// <summary>
/// One row of a list: the first child takes the space left, the others take their natural width (the CSS grid "1fr auto auto"),
/// all centered on the row. A Below control, when shown, spans the whole row under it. A TopBorder draws the line between rows.
/// </summary>
internal sealed class RowBox : Box
{
    private readonly Look _look;

    public RowBox(Look look)
    {
        _look = look;
        Padding = new Padding(look.Px(StatusStyle.RowPaddingX), look.Px(StatusStyle.RowPaddingY), look.Px(StatusStyle.RowPaddingX), look.Px(StatusStyle.RowPaddingY));
    }

    /// <summary>The editor that opens under the row, or null.</summary>
    public Control? Below { get; set; }

    public bool TopBorder { get; set; }

    private int Border => TopBorder ? BorderPx(_look) : 0;

    private int ColumnGap => _look.Px(StatusStyle.RowGap);

    public override int HeightFor(int width) => Measure(Math.Max(0, width - Padding.Horizontal), apply: false) + Padding.Vertical + Border;

    public override int NaturalWidth()
    {
        var cells = Cells();
        return Padding.Horizontal + cells.Sum(Metrics.NaturalWidth) + (ColumnGap * Math.Max(0, cells.Count - 1));
    }

    protected override void OnLayout(LayoutEventArgs levent) => Measure(InnerWidth, apply: true);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (TopBorder)
        {
            using var pen = new Pen(_look.Line, Border);
            e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }
    }

    private List<Control> Cells() => [.. Items().Where(child => !ReferenceEquals(child, Below))];

    private int Measure(int innerWidth, bool apply)
    {
        var cells = Cells();
        if (cells.Count == 0)
        {
            return 0;
        }

        var autos = cells.Skip(1).Select(Metrics.NaturalWidth).ToList();
        var firstWidth = Math.Max(0, innerWidth - autos.Sum() - (ColumnGap * autos.Count));
        var widths = new List<int> { firstWidth };
        widths.AddRange(autos);
        var heights = cells.Select((cell, index) => Metrics.HeightFor(cell, widths[index])).ToList();
        var rowHeight = heights.Max();
        if (apply)
        {
            var x = InnerLeft;
            for (var index = 0; index < cells.Count; index++)
            {
                Place(cells[index], x, InnerTop + Border + ((rowHeight - heights[index]) / 2), widths[index], heights[index]);
                x += widths[index] + ColumnGap;
            }
        }

        if (Below is { } below && Metrics.IsShown(below))
        {
            var belowHeight = Metrics.HeightFor(below, innerWidth);
            if (apply)
            {
                Place(below, InnerLeft, InnerTop + Border + rowHeight + ColumnGap, innerWidth, belowHeight);
            }

            rowHeight += ColumnGap + belowHeight;
        }

        return rowHeight;
    }
}

/// <summary>Children in equal columns (the two meters side by side, or one column when the window is narrow).</summary>
internal sealed class ColumnsBox : Box
{
    public int Columns { get; set; } = 2;

    public int Gap { get; set; }

    public override int HeightFor(int width) => Lay(Math.Max(0, width - Padding.Horizontal), apply: false) + Padding.Vertical;

    public override int NaturalWidth() => Padding.Horizontal + Items().Select(Metrics.NaturalWidth).DefaultIfEmpty(0).Max();

    protected override void OnLayout(LayoutEventArgs levent) => Lay(InnerWidth, apply: true);

    private int Lay(int innerWidth, bool apply)
    {
        var items = Items().ToList();
        var columns = Math.Max(1, Columns);
        var cellWidth = Math.Max(0, (innerWidth - (Gap * (columns - 1))) / columns);
        var y = 0;
        for (var start = 0; start < items.Count; start += columns)
        {
            var row = items.Skip(start).Take(columns).ToList();
            var height = row.Max(item => Metrics.HeightFor(item, cellWidth));
            if (apply)
            {
                for (var index = 0; index < row.Count; index++)
                {
                    Place(row[index], InnerLeft + (index * (cellWidth + Gap)), InnerTop + y, cellWidth, height);
                }
            }

            y += height + (start + columns < items.Count ? Gap : 0);
        }

        return y;
    }
}

/// <summary>
/// A box with a rounded fill and border, around its children laid out as a VBox. The corners show the parent's color.
/// Padding is the space inside the border.
/// </summary>
internal class Surface : VBox
{
    private readonly Look _look;
    private readonly Color? _border;
    private readonly int _radius;
    private readonly int _borderWidth;

    public Surface(Look look, Color? fill, Color? border, int radius, int borderWidth, int padY, int padX)
    {
        _look = look;
        _border = border;
        _radius = look.Px(radius);
        _borderWidth = borderWidth > 0 ? Math.Max(1, look.Px(borderWidth)) : 0;
        Padding = new Padding(look.Px(padX) + _borderWidth, look.Px(padY) + _borderWidth, look.Px(padX) + _borderWidth, look.Px(padY) + _borderWidth);
        if (fill is { } color)
        {
            BackColor = color;
        }
    }

    protected Look Look => _look;

    /// <summary>Changes the fill (children inherit it as their backdrop).</summary>
    public void SetFill(Color fill)
    {
        BackColor = fill;
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        // Everything is drawn in OnPaint, so the corners can show the parent's color without a flash of this box's own.
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? _look.Win);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // The border is drawn on the pixel centers just inside the edge, so it is not clipped.
        var half = _borderWidth / 2f;
        var outside = Overhang;
        using var path = Rounded(new RectangleF(outside + half, outside + half, Width - (2 * outside) - _borderWidth, Height - (2 * outside) - _borderWidth), _radius);
        using (var brush = new SolidBrush(BackColor))
        {
            g.FillPath(brush, path);
        }

        if (_border is { } border && _borderWidth > 0)
        {
            using var pen = new Pen(border, _borderWidth);
            g.DrawPath(pen, path);
        }
    }

    internal static GraphicsPath Rounded(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(rect);
            return path;
        }

        path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
