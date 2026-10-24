using System.Drawing.Drawing2D;
using SteamGate.Core.Status;

namespace SteamGate.Status.Ui;

/// <summary>
/// A drop-down list drawn in the screen's colors (the standard one ignores them in a dark app mode): a bordered box showing the chosen
/// name and an arrow, and a list that opens under it. Choosing a name raises SelectedIndexChanged; setting SelectedIndex in code does not.
/// </summary>
internal sealed class SelectBox : Pressable, IMeasured
{
    private readonly List<string> _items = [];
    private readonly ListBox _list;
    private readonly ToolStripDropDown _drop;
    private int _selected = -1;
    private int _width;

    public SelectBox(Look look)
        : base(look)
    {
        Font = look.Body(StatusStyle.InputFont);
        _list = new ListBox
        {
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            Font = Font,
            BackColor = look.Win,
            ForeColor = look.Fg,
            IntegralHeight = false,
            ItemHeight = TextMetrics.GlyphHeight(Font) + (2 * look.Px(StatusStyle.InputPaddingY)),
            Margin = Padding.Empty,
        };
        _list.DrawItem += DrawItem;
        _list.MouseMove += (_, e) =>
        {
            var index = _list.IndexFromPoint(e.Location);
            if (index >= 0 && index != _list.SelectedIndex)
            {
                _list.SelectedIndex = index;
            }
        };
        _list.MouseUp += (_, e) =>
        {
            var index = _list.IndexFromPoint(e.Location);
            if (index >= 0)
            {
                Choose(index);
            }
        };
        _list.KeyDown += (_, e) =>
        {
            if (e.KeyCode is Keys.Enter && _list.SelectedIndex >= 0)
            {
                Choose(_list.SelectedIndex);
            }
            else if (e.KeyCode is Keys.Escape)
            {
                _drop!.Close();
            }
        };
        if (look.IsDark)
        {
            // A long list scrolls; its scroll bar takes the dark form.
            _list.HandleCreated += (_, _) => Native.UseDarkScrollBars(_list.Handle);
        }

        _drop = new ToolStripDropDown { AutoSize = false, BackColor = look.Win, Padding = Padding.Empty, Margin = Padding.Empty, Renderer = new PaletteRenderer(look) };
        _drop.Items.Add(new ToolStripControlHost(_list) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty });
    }

    public event EventHandler? SelectedIndexChanged;

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected != value)
            {
                _selected = value;
                Invalidate();
            }
        }
    }

    /// <summary>Replaces the names and sets the width the box takes. The chosen index is left to the caller to set.</summary>
    public void SetItems(IEnumerable<string> names, int width)
    {
        _items.Clear();
        _items.AddRange(names);
        _list.Items.Clear();
        _list.Items.AddRange([.. _items]);
        _width = width;
        _selected = Math.Min(_selected, _items.Count - 1);
        Invalidate();
        Parent?.PerformLayout();
    }

    public int HeightFor(int width) => TextMetrics.GlyphHeight(Font) + (2 * (Look.Px(StatusStyle.InputPaddingY) + BorderPx(Look)));

    public int NaturalWidth() => _width;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _drop.Dispose();
            _list.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        ShowList();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter or Keys.Down or Keys.F4)
        {
            e.Handled = true;
            ShowList();
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Down or Keys.Up || base.IsInputKey(keyData);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var body = Body;
        var width = BorderPx(Look);
        var half = width / 2f;
        var radius = Look.Px(StatusStyle.InputRadius);
        using (var path = Surface.Rounded(new RectangleF(body.X + half, body.Y + half, body.Width - width, body.Height - width), radius))
        {
            using var brush = new SolidBrush(Look.Win);
            g.FillPath(brush, path);
            using var pen = new Pen(Look.Line, width);
            g.DrawPath(pen, path);
        }

        var pad = Look.Px(StatusStyle.InputPaddingX);
        var arrow = Look.Px(StatusStyle.SelectArrow);
        var text = _selected >= 0 && _selected < _items.Count ? _items[_selected] : string.Empty;
        TextRenderer.DrawText(g, text, Font, new Rectangle(body.X + pad + width, body.Y, Math.Max(0, body.Width - (2 * (pad + width)) - arrow), body.Height), Look.Fg, Look.Win, TextMetrics.ChoiceFlags);

        // The arrow: a small chevron pointing down.
        var cx = body.Right - pad - width - (arrow / 2f);
        var cy = body.Y + (body.Height / 2f);
        var span = (float)(arrow * StatusStyle.SelectChevronSpanShare);
        using (var chevron = new Pen(Look.Muted, Math.Max((float)StatusStyle.SelectChevronMinStroke, width)))
        {
            g.DrawLines(chevron, [new PointF(cx - span, cy - (span / 2)), new PointF(cx, cy + (span / 2)), new PointF(cx + span, cy - (span / 2))]);
        }

        if (ShowsFocus)
        {
            FocusRing.Draw(g, Look, body, radius);
        }
    }

    private void ShowList()
    {
        if (_items.Count == 0)
        {
            return;
        }

        var rows = Math.Min(_items.Count, StatusStyle.SelectMaxRows);
        var size = new Size(Math.Max(Body.Width, 1), (rows * _list.ItemHeight) + (2 * Look.Px(StatusStyle.SelectListBorder)));
        _list.Size = size;
        _drop.Size = size;
        if (_selected >= 0)
        {
            _list.SelectedIndex = _selected;
        }

        _drop.Show(this, new Point(Body.X, Body.Bottom));
        _list.Focus();
    }

    private void Choose(int index)
    {
        _drop.Close();
        if (index != _selected)
        {
            _selected = index;
            Invalidate();
            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _items.Count)
        {
            return;
        }

        var active = (e.State & DrawItemState.Selected) != 0;
        var back = active ? Look.AccentSoft : Look.Win;
        using (var brush = new SolidBrush(back))
        {
            e.Graphics.FillRectangle(brush, e.Bounds);
        }

        var pad = Look.Px(StatusStyle.InputPaddingX);
        TextRenderer.DrawText(e.Graphics, _items[e.Index], Font, new Rectangle(e.Bounds.X + pad, e.Bounds.Y, Math.Max(0, e.Bounds.Width - (2 * pad)), e.Bounds.Height), Look.Fg, back, TextMetrics.ChoiceFlags);
    }
}

/// <summary>Draws the drop-down's frame in the screen's colors, so no light system border shows around the list in a dark app mode.</summary>
internal sealed class PaletteRenderer : ToolStripProfessionalRenderer
{
    private readonly Look _look;

    public PaletteRenderer(Look look)
    {
        _look = look;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(_look.Win);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_look.Line, BorderWidth());
        var half = BorderWidth() / 2f;
        e.Graphics.DrawRectangle(pen, half, half, e.ToolStrip.Width - BorderWidth(), e.ToolStrip.Height - BorderWidth());
    }

    private int BorderWidth() => Math.Max(1, _look.Px(StatusStyle.SelectListBorder));
}
