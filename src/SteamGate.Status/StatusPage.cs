using SteamGate.Core.Accounting;
using SteamGate.Core.Extend;
using SteamGate.Core.Pipe;
using SteamGate.Core.Rules;
using SteamGate.Core.Status;
using SteamGate.Status.Ui;

namespace SteamGate.Status;

/// <summary>
/// The status screen's content: the heading, the two meters, Extend tonight, the Rules, Waiting to apply, Not counted, and Turn off or remove sections,
/// built to the approved previews from StatusStyle and the texts of StatusText. It keeps what the user has done on the screen (the
/// open editor, what is typed, the chosen program, the answers to requests) in an EditorState, and sends requests through the callback it was given.
/// All of it runs on the window's thread.
/// </summary>
internal sealed class StatusPage : VBox
{
    private readonly Look _look;
    private readonly Func<PipeMessage, bool> _send;
    private readonly List<(FlatButton Button, Func<bool> Allowed)> _buttons = [];
    private readonly Dictionary<ReplySection, Verdict> _replies = [];

    private readonly TextBlock _countingName;
    private readonly FlowBox _counting;
    private readonly Verdict _unreachable;
    private readonly VBox _headStack;
    private readonly FlowBox _offLine;
    private readonly FlowBox _removalLine;
    private readonly ColumnsBox _meters;
    private readonly MeterView _tonight;
    private readonly MeterView _week;
    private readonly List<VBox> _sections = [];
    private readonly List<RuleView> _rules = [];
    private readonly Surface _waitingRows;
    private readonly VBox _nothingWaiting;
    private readonly List<PendingView> _pendingRows = [];
    private readonly Surface _notCountedRows;
    private readonly List<NotCountedView> _notCountedViews = [];
    private readonly SelectBox _addSelect;
    private readonly FlatButton _addRequest;
    private readonly Verdict _addPanelLine;
    private readonly TextBlock _turnOffNote;
    private readonly Verdict _extendReplyTop;
    private readonly Surface _extendBox;
    private readonly List<(int Minutes, FlatButton Button)> _extendLengths = [];
    private readonly Verdict _extendExplain;
    private readonly FlowBox _extendActionLine;
    private readonly Verdict _extendReplyBelow;
    private readonly TextBlock _extendNote;
    private ExtendSectionView? _extendView;

    private StateMessage? _state;
    private EditorState _editor = EditorState.Empty;
    private bool _reachable = true;
    private readonly RequestLog _requests;
    private bool _syncing;
    private IReadOnlyList<string> _pendingKeys = [];
    private IReadOnlyList<string> _notCountedKeys = [];
    private IReadOnlyList<string> _addKeys = [];
    private IReadOnlyList<string> _addNames = [];

    public StatusPage(Look look, Func<PipeMessage, bool> send, RequestLog requests)
    {
        _look = look;
        _send = send;
        _requests = requests;
        BackColor = look.Win;
        Gap = look.Px(StatusStyle.SectionGap);
        Padding = new Padding(look.Px(StatusStyle.ContentPaddingX), look.Px(StatusStyle.ContentPaddingTop), look.Px(StatusStyle.ContentPaddingX), look.Px(StatusStyle.ContentPaddingBottom));

        // The heading and, at the right, the game being counted.
        var title = new TextBlock(look.Display(StatusStyle.TitleFont, StatusStyle.TitleSemibold), look.Fg) { Value = "SteamGate" };
        _countingName = new TextBlock(look.Body(StatusStyle.CountingFont, StatusStyle.CountingNameSemibold), look.Fg);
        _counting = new FlowBox { Align = FlowAlign.Baseline };
        _counting.Controls.Add(new Dot(look) { Margin = new Padding(0, 0, look.Px(StatusStyle.DotGap), 0) });
        _counting.Controls.Add(new TextBlock(look.Body(StatusStyle.CountingFont), look.Muted) { Value = StatusText.CountingNow });
        _counting.Controls.Add(_countingName);
        var head = new FlowBox { Align = FlowAlign.Baseline, SpaceBetween = true, Gap = look.Px(StatusStyle.HeadGap), RowGap = look.Px(StatusStyle.HeadGap) };
        head.Controls.Add(title);
        head.Controls.Add(_counting);
        Controls.Add(head);

        _unreachable = new Verdict(look) { Shown = false };
        _unreachable.Set(StatusText.NotConnected, VerdictKind.Wait);
        Controls.Add(_unreachable);

        // Card 12: SteamGate is off, removal is allowed, and the buttons that take those back.
        _headStack = new VBox { Gap = look.Px(StatusStyle.HeadLinesGap), Shown = false };
        _offLine = OffLine(StatusText.SteamGateOff, StatusText.RequestTurnOn, () => SendFlag(ChangeFields.Enabled, true, ReplySection.Head));
        _removalLine = OffLine(StatusText.RemovalAllowed, StatusText.StopAllowingRemoval, () => SendFlag(ChangeFields.AllowUninstall, false, ReplySection.Head));
        _headStack.Controls.Add(_offLine);
        _headStack.Controls.Add(_removalLine);
        _headStack.Controls.Add(Reply(ReplySection.Head, gapAbove: false));
        Controls.Add(_headStack);

        _tonight = new MeterView(look);
        _week = new MeterView(look);
        _meters = new ColumnsBox { Gap = look.Px(StatusStyle.MeterGap) };
        _meters.Controls.Add(_tonight);
        _meters.Controls.Add(_week);
        Controls.Add(_meters);

        // Extend tonight. Its answer line is not one of the generic replies: where it sits and whether it shows come from StatusView.ExtendSection.
        var extend = Section(ExtendText.Heading);
        _extendReplyTop = new Verdict(look) { Shown = false };
        _extendBox = new Surface(look, look.AccentSoft, null, StatusStyle.EditorRadius, 0, StatusStyle.EditorPaddingY, StatusStyle.EditorPaddingX)
        {
            Gap = look.Px(StatusStyle.EditorGap),
        };
        var extendLine = new FlowBox { Gap = look.Px(StatusStyle.EditorLineGap), RowGap = look.Px(StatusStyle.EditorLineGap) };
        extendLine.Controls.Add(new TextBlock(look.Body(StatusStyle.BodyFont), look.Fg) { Value = ExtendText.RoundLabel });
        foreach (var length in NightExtension.Lengths)
        {
            var minutes = length;
            var button = Button(ExtendText.Length(minutes), () => PickExtend(minutes), () => ExtendEnabled(minutes));
            _extendLengths.Add((minutes, button));
            extendLine.Controls.Add(button);
        }

        _extendExplain = new Verdict(look) { Shown = false };
        _extendActionLine = new FlowBox { Gap = look.Px(StatusStyle.EditorLineGap), RowGap = look.Px(StatusStyle.EditorLineGap), Shown = false };
        _extendActionLine.Controls.Add(Button(ExtendText.ExtendButton, SendExtend, primary: true));
        _extendBox.Controls.Add(extendLine);
        _extendBox.Controls.Add(_extendExplain);
        _extendBox.Controls.Add(_extendActionLine);
        _extendReplyBelow = new Verdict(look) { Shown = false, Margin = new Padding(0, look.Px(StatusStyle.ReplyGapAbove), 0, 0) };
        _extendNote = new TextBlock(look.Body(StatusStyle.NoteFont), look.Muted)
        {
            Wrap = true,
            Margin = new Padding(0, look.Px(StatusStyle.NoteParagraphMargin), 0, look.Px(StatusStyle.NoteParagraphMargin)),
        };
        extend.Controls.Add(_extendReplyTop);
        extend.Controls.Add(_extendBox);
        extend.Controls.Add(_extendReplyBelow);
        extend.Controls.Add(_extendNote);

        // Rules.
        var rules = Section("Rules");
        var ruleRows = RowsSurface();
        foreach (var row in StatusView.RuleRows)
        {
            var view = BuildRule(row, first: _rules.Count == 0);
            _rules.Add(view);
            ruleRows.Controls.Add(view.Box);
        }

        rules.Controls.Add(ruleRows);
        var note = new TextBlock(look.Body(StatusStyle.NoteFont), look.Muted)
        {
            Value = StatusText.RulesNote,
            Wrap = true,
            Margin = new Padding(0, look.Px(StatusStyle.NoteParagraphMargin), 0, look.Px(StatusStyle.NoteParagraphMargin)),
        };
        rules.Controls.Add(note);
        rules.Controls.Add(Reply(ReplySection.Rules, gapAbove: false));

        // Waiting to apply.
        var waiting = Section("Waiting to apply");
        _waitingRows = RowsSurface();
        _nothingWaiting = new VBox { Padding = new Padding(look.Px(StatusStyle.EmptyPaddingX), look.Px(StatusStyle.EmptyPaddingY), look.Px(StatusStyle.EmptyPaddingX), look.Px(StatusStyle.EmptyPaddingY)) };
        _nothingWaiting.Controls.Add(new TextBlock(look.Body(StatusStyle.EmptyFont), look.Muted) { Value = StatusText.NothingWaiting, Wrap = true });
        _waitingRows.Controls.Add(_nothingWaiting);
        waiting.Controls.Add(_waitingRows);
        waiting.Controls.Add(Reply(ReplySection.Waiting));

        // Not counted.
        var notCounted = Section("Not counted");
        _notCountedRows = RowsSurface();
        notCounted.Controls.Add(_notCountedRows);
        var addBox = new Surface(look, look.AccentSoft, null, StatusStyle.EditorRadius, 0, StatusStyle.EditorPaddingY, StatusStyle.EditorPaddingX)
        {
            Gap = look.Px(StatusStyle.EditorGap),
            Margin = new Padding(0, look.Px(StatusStyle.AddBoxMarginTop), 0, 0),
        };
        var addLine = new FlowBox { Gap = look.Px(StatusStyle.EditorLineGap), RowGap = look.Px(StatusStyle.EditorLineGap) };
        addLine.Controls.Add(new TextBlock(look.Body(StatusStyle.BodyFont), look.Fg) { Value = "Add a Steam program:" });
        _addSelect = new SelectBox(look);
        _addSelect.SelectedIndexChanged += (_, _) => OnAddChosen();
        addLine.Controls.Add(_addSelect);
        _addRequest = Button("Request", SendAdd, () => _editor.AddFolder is not null);
        addLine.Controls.Add(_addRequest);
        _addPanelLine = new Verdict(look);
        addBox.Controls.Add(addLine);
        addBox.Controls.Add(_addPanelLine);
        notCounted.Controls.Add(addBox);
        notCounted.Controls.Add(Reply(ReplySection.NotCounted));

        // Turn off or remove.
        var turnOff = Section("Turn off or remove");
        var danger = new FlowBox { Gap = look.Px(StatusStyle.DangerGap), RowGap = look.Px(StatusStyle.DangerGap) };
        danger.Controls.Add(Button("Request turn off", () => SendFlag(ChangeFields.Enabled, false, ReplySection.TurnOff)));
        danger.Controls.Add(Button("Request removal", () => SendFlag(ChangeFields.AllowUninstall, true, ReplySection.TurnOff)));
        _turnOffNote = new TextBlock(look.Body(StatusStyle.NoteFont), look.Muted);
        danger.Controls.Add(_turnOffNote);
        turnOff.Controls.Add(danger);
        turnOff.Controls.Add(Reply(ReplySection.TurnOff));

        foreach (var section in new[] { extend, rules, waiting, notCounted, turnOff })
        {
            section.Shown = false;
            _sections.Add(section);
            Controls.Add(section);
        }

        _meters.Shown = false;
        _counting.Shown = false;
    }

    /// <summary>Raised when something the page shows changed size; the window lays it out again.</summary>
    public event EventHandler? LayoutNeeded;

    /// <summary>What the user has done on the screen, for a window that rebuilds the page (the monitor's DPI changed).</summary>
    public EditorState Editor => _editor;

    /// <summary>The last state shown, for the same.</summary>
    public StateMessage? State => _state;

    /// <summary>Brings a rebuilt page to where the old one was.</summary>
    public void Restore(EditorState editor, StateMessage? state, bool reachable)
    {
        _editor = editor;
        _reachable = reachable;
        if (state is not null)
        {
            _state = state;
            _pendingKeys = [];
            _notCountedKeys = [];
            _addKeys = [];
            _addNames = [];
            SyncAllInputs();
        }

        Render();
    }

    /// <summary>Wide windows use the full padding and two meter columns; a narrow one uses less padding and one column.</summary>
    public void SetNarrow(bool narrow)
    {
        var x = _look.Px(narrow ? StatusStyle.NarrowContentPadding : StatusStyle.ContentPaddingX);
        var top = _look.Px(narrow ? StatusStyle.NarrowContentPadding : StatusStyle.ContentPaddingTop);
        var bottom = _look.Px(narrow ? StatusStyle.NarrowContentPadding : StatusStyle.ContentPaddingBottom);
        Padding = new Padding(x, top, x, bottom);
        _meters.Columns = narrow ? 1 : 2;
    }

    /// <summary>A new state from the service. An open editor, what is typed, the chosen program, and every answer stay as they were.</summary>
    public void ShowState(StateMessage state)
    {
        _state = state;
        _editor = StatusView.Merge(_editor, state, NightClock.NightOf(DateTime.Now));
        Render();
    }

    /// <summary>The connection ended: an Extend request still waiting will get no answer, so the wait ends.</summary>
    public void ConnectionLost()
    {
        _editor = StatusView.ConnectionLost(_editor);
        Render();
    }

    /// <summary>The service's answer to a request this page sent. An answer to anything else is ignored.</summary>
    public void ShowReply(ReplyMessage reply)
    {
        if (_requests.TryComplete(reply.RequestId, out var section))
        {
            _editor = section == ReplySection.Extend
                ? StatusView.ExtendAnswered(_editor, reply.Message, reply.Ok, NightClock.NightOf(DateTime.Now))
                : _editor.WithReply(section, reply.Message);
            Render();
        }
    }

    /// <summary>Whether the screen has a connection. Without one it says so and every button is greyed out.</summary>
    public void SetReachable(bool reachable)
    {
        if (_reachable != reachable)
        {
            _reachable = reachable;
            Render();
        }
    }

    // ------------------------------------------------------------------ building

    private VBox Section(string heading)
    {
        var section = new VBox();
        section.Controls.Add(new TextBlock(_look.Body(StatusStyle.SectionFont, StatusStyle.SectionSemibold), _look.Muted)
        {
            Value = heading.ToUpperInvariant(),
            LetterSpacing = Math.Max(1, (int)Math.Round(StatusStyle.SectionFont * StatusStyle.SectionLetterSpacing * _look.Scale)),
            Margin = new Padding(0, 0, 0, _look.Px(StatusStyle.SectionGapBelow)),
        });
        return section;
    }

    private Surface RowsSurface() => new(_look, null, _look.Line, StatusStyle.RowsRadius, StatusStyle.BorderWidth, 0, 0);

    private Verdict Reply(ReplySection section, bool gapAbove = true)
    {
        var verdict = new Verdict(_look) { Shown = false };
        if (gapAbove)
        {
            verdict.Margin = new Padding(0, _look.Px(StatusStyle.ReplyGapAbove), 0, 0);
        }

        _replies[section] = verdict;
        return verdict;
    }

    private FlatButton Button(string label, Action onClick, Func<bool>? allowed = null, bool primary = false)
    {
        var button = new FlatButton(_look, label, primary);
        button.Click += (_, _) => onClick();
        _buttons.Add((button, allowed ?? (() => true)));
        return button;
    }

    private FlowBox OffLine(string text, string buttonLabel, Action onClick)
    {
        var line = new FlowBox { Gap = _look.Px(StatusStyle.DangerGap), RowGap = _look.Px(StatusStyle.DangerGap), Shown = false };
        var verdict = new Verdict(_look);
        verdict.Set(text, VerdictKind.Wait);
        line.Controls.Add(verdict);
        line.Controls.Add(Button(buttonLabel, onClick));
        return line;
    }

    private RuleView BuildRule(RuleRow row, bool first)
    {
        var box = new RowBox(_look) { TopBorder = !first };
        var value = new TextBlock(_look.Body(StatusStyle.BodyFont, StatusStyle.RowValueSemibold), _look.Fg) { TabularDigits = StatusStyle.TabularNumbers };
        var editor = new Surface(_look, _look.AccentSoft, null, StatusStyle.EditorRadius, 0, StatusStyle.EditorPaddingY, StatusStyle.EditorPaddingX)
        {
            Gap = _look.Px(StatusStyle.EditorGap),
            Shown = false,
        };
        var line = new FlowBox { Gap = _look.Px(StatusStyle.EditorLineGap), RowGap = _look.Px(StatusStyle.EditorLineGap) };

        // The number box (the three limits and the cooldown) or the day check boxes (the weekend nights): one or the other.
        TextBox? number = null;
        var days = new List<TickBox>();
        if (!row.IsDays)
        {
            number = new TextBox
            {
                BorderStyle = BorderStyle.None,
                Font = _look.Body(StatusStyle.InputFont),
                BackColor = _look.Win,
                ForeColor = _look.Fg,
                Margin = Padding.Empty,

                // The standard right-click menu is drawn light in a dark app mode; Ctrl+V and Ctrl+C still work.
                ContextMenuStrip = new ContextMenuStrip(),
                MaxLength = StatusView.RuleRows.Max(r => r.BoxMax).ToString(System.Globalization.CultureInfo.InvariantCulture).Length,
            };
            line.Controls.Add(new InputFrame(_look, number));
            line.Controls.Add(new TextBlock(_look.Body(StatusStyle.NoteFont), _look.Muted) { Value = row.UnitNote ?? string.Empty });
        }
        else
        {
            var dayFlow = new FlowBox { Gap = _look.Px(StatusStyle.DaysGap), RowGap = _look.Px(StatusStyle.DaysGap) };
            foreach (var day in StatusView.CheckBoxDays)
            {
                // The box keeps the preview's margins (4 px left, 3 px on the other sides) and its name follows after the 4 px label gap, so 7 px lie between the box and the name, as in the preview.
                var abbreviation = day.ToString()[..3];
                var check = new TickBox(_look) { AccessibleName = abbreviation, Tag = day };
                var name = new TextBlock(_look.Body(StatusStyle.DayLabelFont), _look.Fg) { Value = abbreviation };
                name.Click += (_, _) => check.Checked = !check.Checked;
                var cell = new FlowBox { Gap = _look.Px(StatusStyle.DayLabelGap) };
                cell.Controls.Add(check);
                cell.Controls.Add(name);
                days.Add(check);
                dayFlow.Controls.Add(cell);
            }

            line.Controls.Add(dayFlow);
        }

        var view = new RuleView(
            row,
            box,
            value,
            editor,
            number,
            days,
            new Verdict(_look),
            self => Button("Change", () => ToggleRule(self)),
            self => Button("Request change", () => SendRule(self), () => self.CanRequest, primary: true));

        if (number is not null)
        {
            // Typing, and pasting, can leave anything in the box; the page settles it to digits within the limit.
            number.KeyPress += (_, e) => e.Handled = !char.IsControl(e.KeyChar) && !char.IsAsciiDigit(e.KeyChar);

            // Enter and Escape do nothing here, and without this Windows beeps.
            number.KeyDown += (_, e) =>
            {
                if (e.KeyCode is Keys.Enter or Keys.Escape)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            number.TextChanged += (_, _) => OnNumberTyped(view);
        }

        foreach (var check in days)
        {
            check.CheckedChanged += (_, _) => OnDayToggled(view);
        }

        line.Controls.Add(view.Request);
        editor.Controls.Add(line);
        editor.Controls.Add(view.Verdict);
        box.Controls.Add(new TextBlock(_look.Body(StatusStyle.BodyFont), _look.Fg) { Value = row.Name, Wrap = true });
        box.Controls.Add(value);
        box.Controls.Add(view.Change);
        box.Controls.Add(editor);
        box.Below = editor;
        return view;
    }

    private PendingView BuildPending(PendingChange entry, bool first)
    {
        var box = new RowBox(_look) { TopBorder = !first };
        var text = new VBox();
        var name = new TextBlock(_look.Body(StatusStyle.BodyFont), _look.Fg) { Wrap = true };
        var note = new TextBlock(_look.Body(StatusStyle.NoteFont), _look.Muted) { Wrap = true };
        text.Controls.Add(name);
        text.Controls.Add(note);
        var pill = new Surface(_look, _look.WaitSoft, null, StatusStyle.PillRadius, 0, StatusStyle.PillPaddingY, StatusStyle.PillPaddingX);
        var pillText = new TextBlock(_look.Body(StatusStyle.PillFont), _look.Wait) { TabularDigits = StatusStyle.TabularNumbers };
        pill.Controls.Add(pillText);
        var id = entry.Id;
        box.Controls.Add(text);
        box.Controls.Add(pill);
        box.Controls.Add(Button("Cancel", () => Send(requestId => StatusRequests.Cancel(requestId, id), ReplySection.Waiting)));
        return new PendingView(id, box, name, note, pillText);
    }

    private NotCountedView BuildNotCounted(NamedFolder entry, bool first)
    {
        var box = new RowBox(_look) { TopBorder = !first };
        var name = new TextBlock(_look.Body(StatusStyle.BodyFont), _look.Fg) { Wrap = true };
        box.Controls.Add(name);
        box.Controls.Add(new TextBlock(_look.Body(StatusStyle.NoteFont), _look.Muted) { Value = StatusText.RemovingAppliesNow });
        var folder = entry.Folder;
        box.Controls.Add(Button("Remove", () => Send(requestId => StatusRequests.Folder(requestId, ChangeFields.NotCountedRemove, folder), ReplySection.NotCounted)));
        return new NotCountedView(folder, box, name);
    }

    // ------------------------------------------------------------------ requests

    private string NextId() => _requests.PeekId();

    // Builds the message with the next request id and sends it, noting which section its answer belongs to. A screen with no connection
    // sends nothing and shows nothing new. A section's last answer stays on screen until that section sends its next request.
    private bool Send(Func<string, PipeMessage> build, ReplySection section)
    {
        if (!_send(build(NextId())))
        {
            return false;
        }

        _requests.Begin(section);
        _editor = _editor.WithReply(section, null);
        Render();
        return true;
    }

    // A click on a length picks it, unless an Extend request is still waiting for its answer.
    private void PickExtend(int minutes)
    {
        if (StatusView.ExtendClickAllowed(_editor))
        {
            _editor = StatusView.ExtendPick(_editor, minutes);
            Render();
        }
    }

    // Sends the picked length. The editor changes only after the message was sent; a send that fails leaves everything as it was.
    private void SendExtend()
    {
        if (!StatusView.ExtendClickAllowed(_editor) || _extendView?.Buttons.FirstOrDefault(button => button.Picked) is not { } picked)
        {
            return;
        }

        if (_send(StatusRequests.Extend(NextId(), picked.Minutes)))
        {
            _requests.Begin(ReplySection.Extend);
            _editor = StatusView.ExtendSent(_editor, picked.Minutes);
            Render();
        }
    }

    private bool ExtendEnabled(int minutes) => _extendView?.Buttons.FirstOrDefault(button => button.Minutes == minutes)?.Enabled ?? false;

    private void SendFlag(string field, bool value, ReplySection section) => Send(id => StatusRequests.Flag(id, field, value), section);

    private void SendAdd()
    {
        if (_editor.AddFolder is { } folder)
        {
            Send(id => StatusRequests.Folder(id, ChangeFields.NotCountedAdd, folder), ReplySection.NotCounted);
        }
    }

    // The same message the editor's verdict was worked out from.
    private RequestMessage RuleMessage(RuleView view, string requestId) =>
        view.Row.IsDays
            ? StatusRequests.WeekendNights(requestId, _editor.Days)
            : StatusRequests.Number(requestId, view.Row.Field, _editor.Number);

    private void SendRule(RuleView view)
    {
        if (Send(id => RuleMessage(view, id), ReplySection.Rules))
        {
            _editor = _editor with { OpenField = null };
            Render();
        }
    }

    // ------------------------------------------------------------------ editing

    private void ToggleRule(RuleView view)
    {
        if (_state is null)
        {
            return;
        }

        _editor = StatusView.Toggle(_editor, view.Row, _state);
        if (_editor.OpenField == view.Row.Field)
        {
            SyncInputs(view);
        }

        Render();
    }

    // Whatever is in the box (typed, or pasted with letters in it), it is brought to digits within the limit, so the verdict always matches the box.
    private void OnNumberTyped(RuleView view)
    {
        if (_syncing || view.Number is not { } box)
        {
            return;
        }

        var digits = new string([.. box.Text.Where(char.IsAsciiDigit)]);
        var typed = digits.Length == 0 ? 0 : (int)Math.Min(long.Parse(digits, System.Globalization.CultureInfo.InvariantCulture), view.Row.BoxMax);
        var shown = digits.Length == 0 ? string.Empty : typed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (shown != box.Text)
        {
            _syncing = true;
            box.Text = shown;
            box.SelectionStart = shown.Length;
            _syncing = false;
        }

        _editor = _editor with { Number = typed };
        Render();
    }

    private void OnDayToggled(RuleView view)
    {
        if (_syncing)
        {
            return;
        }

        _editor = _editor with { Days = [.. view.Days.Where(check => check.Checked).Select(check => (DayOfWeek)check.Tag!)] };
        Render();
    }

    private void OnAddChosen()
    {
        var index = _addSelect.SelectedIndex;
        _editor = _editor with { AddFolder = index >= 0 && index < _addKeys.Count ? _addKeys[index] : null };
        Render();
    }

    // Puts the editor state into the open row's inputs. Only when the row opens: the inputs belong to the user after that.
    private void SyncInputs(RuleView view)
    {
        _syncing = true;
        view.Number?.Text = _editor.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (var check in view.Days)
        {
            check.Checked = _editor.Days.Contains((DayOfWeek)check.Tag!);
        }

        _syncing = false;
    }

    private void SyncAllInputs()
    {
        foreach (var view in _rules.Where(view => view.Row.Field == _editor.OpenField))
        {
            SyncInputs(view);
        }
    }

    // ------------------------------------------------------------------ drawing the state

    // Brings every part of the page in line with the state, the editor state, and the connection. Cheap, and safe to repeat.
    private void Render()
    {
        _unreachable.Shown = !_reachable;
        var state = _state;
        var ready = state is not null;
        _meters.Shown = ready;
        foreach (var section in _sections)
        {
            section.Shown = ready;
        }

        foreach (var (section, verdict) in _replies)
        {
            var text = _editor.ReplyOf(section);
            verdict.Shown = text is not null;
            if (text is not null)
            {
                verdict.Set(text, StatusText.ReplyIsNow(text) ? VerdictKind.Now : VerdictKind.Wait);
            }
        }

        if (state is null)
        {
            _counting.Shown = false;
            _headStack.Shown = _editor.ReplyOf(ReplySection.Head) is not null;
            RefreshEnabled();
            LayoutNeeded?.Invoke(this, EventArgs.Empty);
            return;
        }

        _counting.Shown = state.Counting is not null;
        _countingName.Value = state.Counting ?? string.Empty;

        var headLines = StatusView.HeadLines(state);
        _offLine.Shown = headLines.Contains(StatusText.SteamGateOff);
        _removalLine.Shown = headLines.Contains(StatusText.RemovalAllowed);
        _headStack.Shown = _offLine.Shown || _removalLine.Shown || _editor.ReplyOf(ReplySection.Head) is not null;

        var tonightLimit = StatusText.TonightLimit(state);
        _tonight.Set(StatusText.TonightLabel(state.TonightWeekend), StatusText.TonightLeft(state), StatusText.FillPercent(state.NightSeconds, tonightLimit), StatusText.TonightUsed(state));
        var cap = state.Settings.WeeklyCapMinutes;
        _week.Set(StatusText.WeekLabel, StatusText.WeekLeft(state), StatusText.FillPercent(state.WeekSeconds, cap), StatusText.WeekUsed(state));

        RenderExtend(state);
        RenderRules(state);
        RenderPending(state);
        RenderNotCounted(state);
        _addPanelLine.Set(VerdictLine.NotCountedAdd(state.Settings.CooldownHours), VerdictKind.Wait);
        _turnOffNote.Value = VerdictLine.TurnOffOrRemove(state.Settings.CooldownHours);

        RefreshEnabled();
        LayoutNeeded?.Invoke(this, EventArgs.Empty);
    }

    private void RenderExtend(StateMessage state)
    {
        var view = StatusView.ExtendSection(state, _editor);
        _extendView = view;
        var done = view.Layout == ExtendLayout.Done;
        foreach (var line in new[] { _extendReplyTop, _extendReplyBelow })
        {
            if (view.Reply is { } reply)
            {
                line.Set(reply, StatusText.ReplyIsNow(reply) ? VerdictKind.Now : VerdictKind.Wait);
            }
        }

        _extendReplyTop.Shown = done && view.Reply is not null;
        _extendReplyBelow.Shown = !done && view.Reply is not null;
        _extendBox.Shown = !done;
        foreach (var (minutes, button) in _extendLengths)
        {
            button.Primary = view.Buttons.Any(item => item.Minutes == minutes && item.Picked);
        }

        _extendExplain.Shown = view.Explanation is not null;
        if (view.Explanation is { } explanation)
        {
            _extendExplain.Set(explanation, VerdictKind.Wait);
        }

        _extendActionLine.Shown = view.Explanation is not null;
        _extendNote.Value = view.Note;
    }

    private void RenderRules(StateMessage state)
    {
        foreach (var view in _rules)
        {
            var open = _editor.OpenField == view.Row.Field;
            view.Value.Value = StatusView.RuleValue(view.Row, state);
            view.Change.Label = open ? "Close" : "Change";
            view.Editor.Shown = open;
            if (open)
            {
                var (text, kind, canRequest) = VerdictLine.ForMessage(StatusState.ToSettings(state), RuleMessage(view, NextId()), DateTime.UtcNow, TimeZoneInfo.Local);
                view.Verdict.Set(text, kind);
                view.CanRequest = canRequest;
            }
        }
    }

    private void RenderPending(StateMessage state)
    {
        var entries = state.Settings.Pending;
        var keys = StatusView.PendingKeys(state);
        if (StatusView.RowsChanged(_pendingKeys, keys))
        {
            foreach (var row in _pendingRows)
            {
                row.Box.Dispose();
            }

            _pendingRows.Clear();
            for (var index = 0; index < entries.Count; index++)
            {
                var view = BuildPending(entries[index], first: index == 0);
                _pendingRows.Add(view);
                _waitingRows.Controls.Add(view.Box);
            }

            _pendingKeys = keys;
            _buttons.RemoveAll(pair => pair.Button.IsDisposed);
        }

        var now = DateTime.UtcNow;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var view = _pendingRows[index];
            view.Name.Value = PendingText.For(entry, state);
            view.Note.Value = StatusText.AppliesAt(now.AddSeconds(entry.RemainingWaitSeconds).ToLocalTime());
            view.Pill.Value = StatusText.In(entry.RemainingWaitSeconds);
        }

        _nothingWaiting.Shown = entries.Count == 0;
    }

    private void RenderNotCounted(StateMessage state)
    {
        var entries = state.Settings.NotCounted;
        var keys = StatusView.NotCountedKeys(state);
        if (StatusView.RowsChanged(_notCountedKeys, keys))
        {
            foreach (var row in _notCountedViews)
            {
                row.Box.Dispose();
            }

            _notCountedViews.Clear();
            for (var index = 0; index < entries.Count; index++)
            {
                var view = BuildNotCounted(entries[index], first: index == 0);
                _notCountedViews.Add(view);
                _notCountedRows.Controls.Add(view.Box);
            }

            _notCountedKeys = keys;
            _buttons.RemoveAll(pair => pair.Button.IsDisposed);
        }

        for (var index = 0; index < entries.Count; index++)
        {
            _notCountedViews[index].Name.Value = entries[index].DisplayName;
        }

        // The list is set again when a program comes or goes, or when one keeps its folder and shows a new name.
        var addKeys = StatusView.AddableKeys(state);
        var addNames = StatusView.AddableNames(state);
        if (StatusView.RowsChanged(_addKeys, addKeys) || StatusView.RowsChanged(_addNames, addNames))
        {
            _addSelect.SetItems(addNames, SelectWidth(state.Addable));
            _addKeys = addKeys;
            _addNames = addNames;
        }

        _addSelect.SelectedIndex = _editor.AddFolder is { } folder ? IndexOfKey(_addKeys, folder) : -1;
    }

    private static int IndexOfKey(IReadOnlyList<string> keys, string key)
    {
        for (var index = 0; index < keys.Count; index++)
        {
            if (keys[index] == key)
            {
                return index;
            }
        }

        return -1;
    }

    // As wide as the longest program name plus the drop-down arrow, within sensible limits.
    private int SelectWidth(IReadOnlyList<NamedFolder> entries)
    {
        var widest = entries.Select(entry => TextRenderer.MeasureText(entry.DisplayName, _addSelect.Font).Width).DefaultIfEmpty(0).Max();
        return Math.Clamp(widest + _look.Px(StatusStyle.SelectExtra), _look.Px(StatusStyle.SelectMinWidth), _look.Px(StatusStyle.SelectMaxWidth));
    }

    private void RefreshEnabled()
    {
        _buttons.RemoveAll(pair => pair.Button.IsDisposed);
        foreach (var (button, allowed) in _buttons)
        {
            button.Enabled = _reachable && allowed();
        }
    }

    // ------------------------------------------------------------------ parts

    private sealed class RuleView
    {
        public RuleView(
            RuleRow row,
            RowBox box,
            TextBlock value,
            Surface editor,
            TextBox? number,
            IReadOnlyList<TickBox> days,
            Verdict verdict,
            Func<RuleView, FlatButton> makeChange,
            Func<RuleView, FlatButton> makeRequest)
        {
            Row = row;
            Box = box;
            Value = value;
            Editor = editor;
            Number = number;
            Days = days;
            Verdict = verdict;
            Change = makeChange(this);
            Request = makeRequest(this);
        }

        public RuleRow Row { get; }

        public RowBox Box { get; }

        public TextBlock Value { get; }

        public Surface Editor { get; }

        /// <summary>The number box; null on the weekend-nights row, which has check boxes instead.</summary>
        public TextBox? Number { get; }

        /// <summary>The day check boxes; empty on the rows that have a number box.</summary>
        public IReadOnlyList<TickBox> Days { get; }

        public Verdict Verdict { get; }

        public FlatButton Change { get; }

        public FlatButton Request { get; }

        public bool CanRequest { get; set; }
    }

    private sealed record PendingView(Guid Id, RowBox Box, TextBlock Name, TextBlock Note, TextBlock Pill);

    private sealed record NotCountedView(string Folder, RowBox Box, TextBlock Name);
}

/// <summary>One of the two meters: what it measures, the time left, a bar, and how much is used.</summary>
internal sealed class MeterView : Surface
{
    private readonly TextBlock _label;
    private readonly TextBlock _big;
    private readonly ProgressTrack _track;
    private readonly TextBlock _sub;

    public MeterView(Look look)
        : base(look, null, look.Line, StatusStyle.MeterRadius, StatusStyle.BorderWidth, StatusStyle.MeterPaddingY, StatusStyle.MeterPaddingX)
    {
        Gap = look.Px(StatusStyle.MeterInnerGap);
        _label = new TextBlock(look.Body(StatusStyle.LabelFont), look.Muted) { Wrap = true };
        _big = new TextBlock(look.Display(StatusStyle.BigFont, StatusStyle.BigSemibold), look.Fg)
        {
            LineHeight = (int)Math.Round(look.Px(StatusStyle.BigFont) * StatusStyle.BigLineHeight),
            TabularDigits = StatusStyle.TabularNumbers,
        };
        var big = new FlowBox { Align = FlowAlign.Baseline, Gap = look.Px(StatusStyle.BigLeftGap) };
        big.Controls.Add(_big);
        big.Controls.Add(new TextBlock(look.Body(StatusStyle.BigSmallFont), look.Muted) { Value = StatusText.LeftWord });
        _track = new ProgressTrack(look);
        _sub = new TextBlock(look.Body(StatusStyle.SubFont), look.Muted) { TabularDigits = StatusStyle.TabularNumbers };
        Controls.Add(_label);
        Controls.Add(big);
        Controls.Add(_track);
        Controls.Add(_sub);
    }

    public void Set(string label, string left, int percent, string used)
    {
        _label.Value = label;
        _big.Value = left;
        _track.Percent = percent;
        _sub.Value = used;
    }
}
