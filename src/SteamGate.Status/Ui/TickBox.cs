using System.Drawing.Drawing2D;
using SteamGate.Core.Status;

namespace SteamGate.Status.Ui;

/// <summary>
/// A check box drawn in both app modes like the one a browser draws for the previews (the standard one stays light in a dark app mode). A rounded
/// square with a border, filled and ticked when checked; Space toggles it; it shows the focus ring. Its name and checked state are
/// available to screen readers. Its label is a separate control that toggles it.
/// </summary>
internal sealed class TickBox : Pressable, IMeasured
{
    private bool _checked;

    public TickBox(Look look)
        : base(look)
    {
        AccessibleRole = AccessibleRole.CheckButton;

        // The margins the previews give the box, kept as padding so the control is as big as the box with its margins.
        Padding = new Padding(look.Px(StatusStyle.CheckMarginLeft), look.Px(StatusStyle.CheckMarginY), look.Px(StatusStyle.CheckMarginRight), look.Px(StatusStyle.CheckMarginY));
    }

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int HeightFor(int width) => Look.Px(StatusStyle.CheckSize) + Padding.Vertical;

    public int NaturalWidth() => Look.Px(StatusStyle.CheckSize) + Padding.Horizontal;

    protected override AccessibleObject CreateAccessibilityInstance() => new TickBoxAccessibleObject(this);

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space)
        {
            e.Handled = true;
            Checked = !Checked;
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData == Keys.Space || base.IsInputKey(keyData);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var outer = Body;
        var body = new Rectangle(outer.X + Padding.Left, outer.Y + Padding.Top, Look.Px(StatusStyle.CheckSize), Look.Px(StatusStyle.CheckSize));
        var width = BorderPx(Look);
        var half = width / 2f;
        var radius = Look.Px(StatusStyle.CheckRadius);
        var line = _checked ? (Hover ? Look.CheckedHover : Look.CheckedFill) : (Hover ? Look.CheckBorderHover : Look.CheckBorder);
        using (var path = Surface.Rounded(new RectangleF(body.X + half, body.Y + half, body.Width - width, body.Height - width), radius))
        {
            using var brush = new SolidBrush(_checked ? line : Look.CheckFill);
            g.FillPath(brush, path);
            using var pen = new Pen(line, width);
            g.DrawPath(pen, path);
        }

        if (_checked)
        {
            using var tick = new Pen(Look.CheckTick, Math.Max(1, Look.Px(StatusStyle.CheckTickStroke))) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLines(tick, [Point(body, StatusStyle.CheckTickStartX, StatusStyle.CheckTickStartY), Point(body, StatusStyle.CheckTickMiddleX, StatusStyle.CheckTickMiddleY), Point(body, StatusStyle.CheckTickEndX, StatusStyle.CheckTickEndY)]);
        }

        if (ShowsFocus)
        {
            FocusRing.Draw(g, Look, body, radius);
        }
    }

    private static PointF Point(Rectangle body, double shareX, double shareY) =>
        new((float)(body.X + (body.Width * shareX)), (float)(body.Y + (body.Height * shareY)));

    private sealed class TickBoxAccessibleObject : ControlAccessibleObject
    {
        private readonly TickBox _owner;

        public TickBoxAccessibleObject(TickBox owner)
            : base(owner)
        {
            _owner = owner;
        }

        public override AccessibleRole Role => AccessibleRole.CheckButton;

        public override AccessibleStates State => base.State | (_owner.Checked ? AccessibleStates.Checked : AccessibleStates.None);

        public override string DefaultAction => "Toggle";

        public override void DoDefaultAction() => _owner.Checked = !_owner.Checked;
    }
}
