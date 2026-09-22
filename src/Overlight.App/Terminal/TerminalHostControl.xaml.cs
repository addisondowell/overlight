using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Overlight.App.Terminal.Models;
using Overlight.App.Terminal.Pty;
using Overlight.App.Terminal.Web;

namespace Overlight.App.Terminal;

/// <summary>
/// Hosts xterm.js (Terminal/WebAssets/vendor/xterm) inside a WebView2
/// control and bridges it to a real shell over ConPTY
/// (Terminal/Pty/PseudoConsoleSession.cs). This is the actual terminal
/// emulator: xterm.js owns VT/ANSI parsing and rendering — the same
/// library VS Code's integrated terminal and Hyper use — and this class
/// only shuttles bytes between it and the pty.
///
/// The shell is restarted (killed and respawned with a fresh
/// environment) whenever the active account changes in the Accounts
/// panel — that's what actually makes account switching do something
/// real instead of being a label; see ClaudeAccount.EnvironmentOverrides.
///
/// Unverified on a real Windows install, same caveat as the rest of the
/// scaffold: WebView2 Runtime must be present, and the message-bridging
/// shape here (SetVirtualHostNameToFolderMapping, PostWebMessageAsString,
/// WebMessageAsJson) follows the documented WebView2 APIs but hasn't
/// been exercised against a live build.
/// </summary>
public sealed partial class TerminalHostControl : UserControl
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private PseudoConsoleSession _session = new();
    private bool _loaded;
    private bool _ptyStarted;
    private int _pendingCols = 80;
    private int _pendingRows = 24;

    public TerminalHostControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // WinUI can raise Loaded more than once for the same control
        // instance (e.g. theme changes). Without this guard, a second
        // firing would double-subscribe WebMessageReceived — every
        // keystroke handled twice — and re-navigate the WebView2,
        // discarding whatever shell session was already running.
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        await Browser.EnsureCoreWebView2Async();

        string webAssetsPath = Path.Combine(AppContext.BaseDirectory, "Terminal", "WebAssets");
        Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "overlight.local", webAssetsPath, CoreWebView2HostResourceAccessKind.Allow);

        Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        TerminalServices.AccountManager.ActiveAccountChanged += OnActiveAccountChanged;

        WireSessionEvents();

        Browser.Source = new Uri("https://overlight.local/index.html");
    }

    private void WireSessionEvents()
    {
        _session.OutputReceived += OnPtyOutput;
        _session.ProcessExited += _ =>
            DispatcherQueue.TryEnqueue(() => PostToPage("\r\n\u001b[90m[shell exited]\u001b[0m\r\n"));
    }

    private void OnActiveAccountChanged(ClaudeAccount? account)
    {
        // Only a real shell restart actually applies new environment
        // overrides — an already-running cmd.exe can't have its
        // environment changed out from under it. Runs even before the
        // first shell has started (RestartShell handles that case too),
        // so switching accounts before ever typing anything still picks
        // up the right environment on first start.
        DispatcherQueue.TryEnqueue(() => RestartShell(account));
    }

    private void RestartShell(ClaudeAccount? account)
    {
        bool hadSession = _ptyStarted;

        // Dispose the old session off the UI thread: it blocks briefly
        // waiting for its read loop to unwind, and this method runs on
        // the UI thread (see OnActiveAccountChanged) — doing that wait
        // inline would freeze the window on every account switch. A few
        // trailing bytes from the old shell's output could in principle
        // still land after the new shell starts (its read loop isn't
        // guaranteed to be fully cancelled before we proceed) — a minor
        // cosmetic race, not a correctness issue worth more machinery
        // for right now.
        PseudoConsoleSession oldSession = _session;
        _ = Task.Run(oldSession.Dispose);

        _session = new PseudoConsoleSession();
        WireSessionEvents();
        _ptyStarted = false;

        if (hadSession)
        {
            PostToPage($"\r\n\u001b[90m[switched to account '{account?.Label ?? "(none)"}', restarting shell]\u001b[0m\r\n");
        }

        StartShell(account);
    }

    private void OnWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        TerminalWebMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<TerminalWebMessage>(args.WebMessageAsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return;
        }

        if (message is null)
        {
            return;
        }

        switch (message.Type)
        {
            case "ready":
                StartShell(TerminalServices.AccountManager.ActiveAccount);
                break;

            case "input":
                if (message.Data is not null)
                {
                    _session.WriteInput(message.Data);
                }
                break;

            case "resize":
                if (message.Cols is int cols and > 0 && message.Rows is int rows and > 0)
                {
                    _pendingCols = cols;
                    _pendingRows = rows;
                    if (_ptyStarted)
                    {
                        _session.Resize(cols, rows);
                    }
                    else
                    {
                        StartShell(TerminalServices.AccountManager.ActiveAccount);
                    }
                }
                break;
        }
    }

    private void StartShell(ClaudeAccount? account)
    {
        if (_ptyStarted)
        {
            return;
        }

        _ptyStarted = true;

        // cmd.exe via %COMSPEC% is the one shell guaranteed present on
        // every Windows install. Swap for a PowerShell/WSL picker once
        // there's a UI for choosing a shell per account/profile.
        string shell = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";

        bool started = _session.Start(
            shell, _pendingCols, _pendingRows, account?.EnvironmentOverrides);

        if (!started)
        {
            _ptyStarted = false;
            PostToPage($"\u001b[91mfailed to start shell: {_session.LastError ?? "unknown error"}\u001b[0m\r\n");
        }
    }

    private void OnPtyOutput(ReadOnlyMemory<byte> data)
    {
        string text = Encoding.UTF8.GetString(data.Span);
        DispatcherQueue.TryEnqueue(() => PostToPage(text));
    }

    private void PostToPage(string text) => Browser.CoreWebView2?.PostWebMessageAsString(text);

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TerminalServices.AccountManager.ActiveAccountChanged -= OnActiveAccountChanged;
        if (Browser.CoreWebView2 is not null)
        {
            Browser.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        }

        _session.Dispose();
        _loaded = false;
        _ptyStarted = false;
    }
}
