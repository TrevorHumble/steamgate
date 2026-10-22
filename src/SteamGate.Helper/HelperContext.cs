using SteamGate.Core.Pipe;

namespace SteamGate.Helper;

/// <summary>
/// The helper's whole life: no window until a banner is due, no tray icon. A background task keeps a connection to the
/// service (retrying every 5 seconds) and turns its messages into overlays and window-close requests.
/// </summary>
internal sealed class HelperContext : ApplicationContext
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly OverlayForm _overlay = new();
    private readonly CancellationTokenSource _stopping = new();

    public HelperContext()
    {
        // If this session cannot make a window, the helper still connects and the service still force-closes; only the banner is lost.
        try
        {
            _overlay.PrepareHandle();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // No overlay in this session.
        }

        // Started only now, so a message that arrives early finds the window ready.
        _ = Task.Run(() => ConnectLoopAsync(_stopping.Token));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stopping.Cancel();
            _stopping.Dispose();
            _overlay.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task ConnectLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ServeOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // Whatever went wrong, the helper stays alive and tries again in 5 seconds.
            }

            try
            {
                await Task.Delay(ReconnectDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // One connection from hello to disconnect. Any failure ends it quietly; the loop reconnects.
    private async Task ServeOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var client = PipeClient.TryConnect();
            if (client is null)
            {
                return;
            }

            await client.WriteAsync(new HelloMessage(HelloMessage.HelperRole).ToLine(), stoppingToken).ConfigureAwait(false);
            await client.FlushAsync(stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                var line = await PipeLineReader.ReadLineAsync(client, PipeLineReader.MaxLineBytes, stoppingToken).ConfigureAwait(false);
                if (PipeMessage.Parse(line) is not { } message)
                {
                    return;
                }

                Handle(message);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or UnauthorizedAccessException)
        {
            // The service closed the connection or went away.
        }
    }

    private void Handle(PipeMessage message)
    {
        switch (message)
        {
            case WarnMessage warn:
                Show(BannerText.ForWarn(warn.MinutesLeft));
                break;
            case CloseMessage close:
                WindowCloser.PostClose([.. close.ProcessIds]);
                Show(BannerText.ForClose(close.DisplayName));
                break;
        }
    }

    private void Show(string text)
    {
        if (_overlay.IsHandleCreated && !_overlay.IsDisposed)
        {
            _overlay.BeginInvoke(() => _overlay.ShowBanner(text));
        }
    }
}
