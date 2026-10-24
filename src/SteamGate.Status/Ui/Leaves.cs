using System.Drawing.Drawing2D;
using SteamGate.Core.Status;

namespace SteamGate.Status.Ui;

/// <summary>
/// Text in one font and color, on one line or wrapped to the width it is given. Drawn with Windows' own text renderer, so it looks like any
/// Windows text. With LetterSpacing or TabularDigits it is drawn one character at a time (and never wraps).
/// </summary>
internal sealed class TextBlock : Piece, IMeasured, IBaseline
{
    private string _value = string.Empty;
    private Color _color;
    private int _digitCell = -1;

    public TextBlock(Font font, Color color)
    {
        Font = font;
        _color = color;
    }

    /// <summary>Wrap onto more lines at the width given, instead of one line.</summary>
    public bool Wrap { get; set; }

    /// <summary>The height of the line box in pixels, when it is not the font's own; the text sits in the middle of it.</summary>
    public int LineHeight { get; set; }

    /// <summary>Space added after each character, in pixels.</summary>
    public int LetterSpacing { get; set; }

    /// <summary>Every digit takes a cell as wide as the widest of 0 to 9, so numbers that change line up (the preview's tabular figures).</summary>
    public bool TabularDigits { get; set; }

    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                Invalidate();
                Parent?.PerformLayout();
            }
        }
    }

    public Color TextColor
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                Invalidate();
            }
        }
    }

    public int HeightFor(int width)
    {
        if (!Wrap || _value.Length == 0)
        {
            return LineHeight > 0 ? LineHeight : TextMetrics.GlyphHeight(Font);
        }

        var wrapped = TextRenderer.MeasureText(_value, Font, new Size(Math.Max(1, width), TextMetrics.Unbounded.Height), TextMetrics.LabelFlags | TextFormatFlags.WordBreak);
        return Math.Max(LineHeight, wrapped.Height);
    }

    public int NaturalWidth() => WidthOf(_value);

    public int Baseline()
    {
        var box = LineHeight > 0 ? LineHeight : TextMetrics.GlyphHeight(Font);
        return ((box - TextMetrics.GlyphHeight(Font)) / 2) + TextMetrics.Ascent(Font);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var top = !Wrap && LineHeight > 0 ? (LineHeight - TextMetrics.GlyphHeight(Font)) / 2 : 0;
        if (PerCharacter)
        {
            var x = 0;
            foreach (var character in _value)
            {
                var text = character.ToString();
                var advance = Advance(character);
                var cell = char.IsAsciiDigit(character) && TabularDigits ? (advance - TextMetrics.Width(text, Font)) / 2 : 0;
                if (character != ' ')
                {
                    // No background is filled: the next character's box would cut into this one.
                    TextRenderer.DrawText(e.Graphics, text, Font, new Point(x + cell, top), _color, TextMetrics.LabelFlags);
                }

                x += advance + LetterSpacing;
            }

            return;
        }

        var flags = TextMetrics.LabelFlags | (Wrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine);
        TextRenderer.DrawText(e.Graphics, _value, Font, new Rectangle(0, top, Width, Math.Max(0, Height - top)), _color, BackColor, flags);
    }

    private bool PerCharacter => LetterSpacing > 0 || TabularDigits;

    // The width of one character: a digit takes the widest digit's cell when digits are tabular. A space is measured with a mark after it,
    // because Windows leaves trailing spaces out of a measurement.
    private int Advance(char character)
    {
        if (TabularDigits && char.IsAsciiDigit(character))
        {
            if (_digitCell < 0)
            {
                _digitCell = Enumerable.Range(0, 10).Max(digit => TextMetrics.Width(((char)('0' + digit)).ToString(), Font));
            }

            return _digitCell;
        }

        return character == ' ' ? TextMetrics.Width(" .", Font) - TextMetrics.Width(".", Font) : TextMetrics.Width(character.ToString(), Font);
    }

    private int WidthOf(string text)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        if (PerCharacter)
        {
            return text.Sum(Advance) + (LetterSpacing * text.Length);
        }

        return text.EndsWith(' ') ? TextMetrics.Width(text + ".", Font) - TextMetrics.Width(".", Font) : TextMetrics.Width(text, Font);
    }
}

/// <summary>A button in the approved look: border, rounded corners, an accent border on hover, a filled primary form, half see-through when disabled, and a focus ring outside the border.</summary>
internal sealed class FlatButton : Pressable, IMeasured, IBaseline
{
    private readonly bool _primary;
    private string _label;

    public FlatButton(Look look, string label, bool primary = false)
        : base(look)
    {
        _primary = primary;
        _label = label;
        Font = look.Body(StatusStyle.ButtonFont);
    }

    public string Label
    {
        get => _label;
        set
        {
            if (_label != value)
            {
                _label = value;
                Invalidate();
                Parent?.PerformLayout();
            }
        }
    }

    public int HeightFor(int width) => TextMetrics.GlyphHeight(Font) + (2 * (Look.Px(StatusStyle.ButtonPaddingY) + BorderPx(Look)));

    public int NaturalWidth() => TextMetrics.Width(_label, Font) + (2 * (Look.Px(StatusStyle.ButtonPaddingX) + BorderPx(Look)));

    public int Baseline() => Look.Px(StatusStyle.ButtonPaddingY) + BorderPx(Look) + TextMetrics.Ascent(Font);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
        }
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var backdrop = BackColor;
        var fill = _primary ? Look.Accent : Look.Win;
        var border = _primary || (Hover && Enabled) ? Look.Accent : Look.Line;
        var text = _primary ? Look.Win : Look.Fg;
        if (!Enabled)
        {
            fill = Look.Blend(fill, backdrop, StatusStyle.DisabledOpacity);
            border = Look.Blend(border, backdrop, StatusStyle.DisabledOpacity);
            text = Look.Blend(text, backdrop, StatusStyle.DisabledOpacity);
        }

        var body = Body;
        var width = BorderPx(Look);
        var half = width / 2f;
        var radius = Look.Px(StatusStyle.ButtonRadius);
        using (var path = Surface.Rounded(new RectangleF(body.X + half, body.Y + half, body.Width - width, body.Height - width), radius))
        {
            using var brush = new SolidBrush(fill);
            g.FillPath(brush, path);
            using var pen = new Pen(border, width);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(g, _label, Font, body, text, fill, TextMetrics.SingleLineFlags | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (ShowsFocus)
        {
            FocusRing.Draw(g, Look, body, radius);
        }
    }
}

/// <summary>The green dot before the name of the game being counted.</summary>
internal sealed class Dot : Piece, IMeasured
{
    private readonly Look _look;

    public Dot(Look look)
    {
        _look = look;
    }

    public int HeightFor(int width) => _look.Px(StatusStyle.DotSize);

    public int NaturalWidth() => _look.Px(StatusStyle.DotSize);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(_look.Now);
        e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>The bar under a meter: a rounded track, filled from the left to a percentage.</summary>
internal sealed class ProgressTrack : Piece, IMeasured
{
    private readonly Look _look;
    private int _percent;

    public ProgressTrack(Look look)
    {
        _look = look;
    }

    public int Percent
    {
        get => _percent;
        set
        {
            if (_percent != value)
            {
                _percent = value;
                Invalidate();
            }
        }
    }

    public int HeightFor(int width) => _look.Px(StatusStyle.TrackHeight);

    public int NaturalWidth() => 0;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var track = Surface.Rounded(new RectangleF(0, 0, Width, Height), _look.Px(StatusStyle.TrackRadius));
        using (var brush = new SolidBrush(_look.Bar))
        {
            g.FillPath(brush, track);
        }

        g.SetClip(track);
        using var fill = new SolidBrush(_look.Accent);
        g.FillRectangle(fill, 0, 0, (float)(Width * _percent / 100.0), Height);
        g.ResetClip();
    }
}

/// <summary>A line in one of the two verdict colors: green for what applies now, amber for the rest.</summary>
internal sealed class Verdict : Surface
{
    private readonly TextBlock _text;

    public Verdict(Look look)
        : base(look, look.WaitSoft, null, StatusStyle.VerdictRadius, 0, StatusStyle.VerdictPaddingY, StatusStyle.VerdictPaddingX)
    {
        _text = new TextBlock(look.Body(StatusStyle.VerdictFont), look.Wait) { Wrap = true };
        Controls.Add(_text);
    }

    public void Set(string text, VerdictKind kind)
    {
        var now = kind == VerdictKind.Now;
        SetFill(now ? Look.NowSoft : Look.WaitSoft);
        _text.TextColor = now ? Look.Now : Look.Wait;
        _text.Value = text;
    }
}

/// <summary>A number box in the approved look: a bordered, rounded frame around a plain text box, with the focus ring outside the border while the text box has focus.</summary>
internal sealed class InputFrame : Surface
{
    private readonly TextBox _box;

    public InputFrame(Look look, TextBox box)
        : base(look, look.Win, look.Line, StatusStyle.InputRadius, StatusStyle.BorderWidth, StatusStyle.InputPaddingY, StatusStyle.InputPaddingX)
    {
        _box = box;
        FixedNaturalWidth = look.Px(StatusStyle.NumberInputWidth + (2 * StatusStyle.InputPaddingX) + (2 * StatusStyle.BorderWidth));
        Controls.Add(box);
        box.GotFocus += (_, _) => Invalidate();
        box.LostFocus += (_, _) => Invalidate();
    }

    protected override int OwnOverhang => FocusRing.Extent(Look);

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_box.Focused)
        {
            var o = Overhang;
            FocusRing.Draw(e.Graphics, Look, new Rectangle(o, o, Width - (2 * o), Height - (2 * o)), Look.Px(StatusStyle.InputRadius));
        }
    }
}
