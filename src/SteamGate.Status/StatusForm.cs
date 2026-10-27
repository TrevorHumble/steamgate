using SteamGate.Core.Pipe;
using SteamGate.Core.Status;

namespace SteamGate.Status;

/// <summary>
/// The status window: a normal window (title bar caption SteamGate, taskbar button, the default Windows program icon) that scrolls when its
/// content is taller than it. It owns the connection to the service, the colors and scale (rebuilt when the monitor's DPI changes), and
/// the rule that after 10 seconds without a connection the page says so. Everything else is the page's.
/// </summary>
internal sealed class StatusForm : Form
{
    private readonly bool _dark = Look.AppsAreDark();
    private readonly Panel _scroll = new() { AutoScroll = true, Dock = DockStyle.Fill };
    private readonly StatusConnection _connection;
    private readonly RequestLog _requests = new();
    private readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };

    private Look? _look;
    private StatusPage? _page;
    private bool _connected;
    private long _withoutConnectionSince = Environment.TickCount64;

    public StatusForm()
    {
        Text = "SteamGate";
        Icon = SystemIcons.Application;
        AutoScaleMode = AutoScaleMode.None;
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_scroll);
        _connection = new StatusConnection(OnMessage, OnConnected);
        _clock.Tick += (_, _) => CheckReachable();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetDarkTitleBar(Handle, _dark);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var scale = DeviceDpi / 96.0;
        var work = Screen.FromControl(this).WorkingArea;
        ClientSize = new Size(
            Math.Min((int)Math.Round(StatusStyle.StartWidth * scale), (int)(work.Width * StatusStyle.StartMaxShare)),
            Math.Min((int)Math.Round(StatusStyle.StartHeight * scale), (int)(work.Height * StatusStyle.StartMaxShare)));
        MinimumSize = new Size((int)Math.Round(StatusStyle.MinWidth * scale), (int)Math.Round(StatusStyle.MinHeight * scale));
        CenterToScreen();
        Rebuild();
        _clock.Start();
        _connection.Start();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutPage();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        Rebuild();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _clock.Stop();
        _connection.Dispose();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
            _connection.Dispose();
            _page?.Dispose();
            _look?.Dispose();
            _scroll.Dispose();
        }

        base.Dispose(disposing);
    }

    // Builds the page again at the monitor's scale, keeping what the user had done and the last state.
    private void Rebuild()
    {
        var editor = _page?.Editor ?? EditorState.Empty;
        var state = _page?.State;
        var reachable = IsReachable();
        if (_page is not null)
        {
            _scroll.Controls.Remove(_page);
            _page.Dispose();
        }

        _look?.Dispose();
        _look = new Look(_dark ? StatusStyle.Dark : StatusStyle.Light, DeviceDpi / 96.0);
        BackColor = _look.Win;
        _scroll.BackColor = _look.Win;
        if (_look.IsDark)
        {
            Native.UseDarkScrollBars(_scroll.Handle);
        }

        _page = new StatusPage(_look, _connection.TrySend, _requests);
        _page.LayoutNeeded += (_, _) => LayoutPage();
        _scroll.Controls.Add(_page);
        _page.Restore(editor, state, reachable);
        LayoutPage();
    }

    // Sizes the page to the window, centered up to the content width, and lets the panel scroll when the page is taller. The scroll position is kept.
    private void LayoutPage()
    {
        if (_page is null || _look is null)
        {
            return;
        }

        var scrolledTo = -_scroll.AutoScrollPosition.Y;
        var width = _scroll.ClientSize.Width;
        var pageWidth = Math.Min(width, _look.Px(StatusStyle.ContentWidth));
        _page.SetNarrow(width <= _look.Px(StatusStyle.NarrowWidth));
        var height = _page.HeightFor(pageWidth);
        if (_page.Height != height || _page.Width != pageWidth)
        {
            // The scrollbar appearing narrows the panel, so the page is sized twice.
            for (var pass = 0; pass < 2; pass++)
            {
                _scroll.AutoScrollPosition = Point.Empty;
                width = _scroll.ClientSize.Width;
                pageWidth = Math.Min(width, _look.Px(StatusStyle.ContentWidth));
                _page.SetNarrow(width <= _look.Px(StatusStyle.NarrowWidth));
                height = _page.HeightFor(pageWidth);
                _page.SetBounds((width - pageWidth) / 2, 0, pageWidth, height);
                _scroll.AutoScrollMinSize = new Size(0, height);
            }

            _scroll.AutoScrollPosition = new Point(0, scrolledTo);
        }

        _page.RelayoutDeep();
    }

    private bool IsReachable() => _connected || !StatusView.Unreachable(TimeSpan.FromMilliseconds(Environment.TickCount64 - _withoutConnectionSince));

    private void CheckReachable() => _page?.SetReachable(IsReachable());

    // Called on the connection's task: the window handles it on its own thread.
    private void OnMessage(PipeMessage message)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        try
        {
            BeginInvoke(() => Dispatch(message));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // The window is closing.
        }
    }

    private void OnConnected(bool connected)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        try
        {
            BeginInvoke(() =>
            {
                _connected = connected;
                if (!connected)
                {
                    _withoutConnectionSince = Environment.TickCount64;
                    _page?.ConnectionLost();
                }

                CheckReachable();
            });
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException)
        {
            // The window is closing.
        }
    }

    private void Dispatch(PipeMessage message)
    {
        switch (message)
        {
            case StateMessage state:
                _page?.ShowState(state);
                break;
            case ReplyMessage reply:
                _page?.ShowReply(reply);
                break;
        }
    }
}
