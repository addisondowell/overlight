using System.Text;
using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
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
/// Unverified on a real Windows install, same caveat as the rest of the
/// scaffold: WebView2 Runtime must be present, and the message-bridging
/// shape here (SetVirtualHostNameToFolderMapping, PostWebMessageAsString,
/// WebMessageAsJson) follows the documented WebView2 APIs but hasn't
/// been exercised against a live build.
/// </summary>
public sealed partial class TerminalHostControl : UserControl
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly PseudoConsoleSession _session = new();
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
        await Browser.EnsureCoreWebView2Async();

        string webAssetsPath = Path.Combine(AppContext.BaseDirectory, "Terminal", "WebAssets");
        Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "overlight.local", webAssetsPath, CoreWebView2HostResourceAccessKind.Allow);

        Browser.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        _session.OutputReceived += OnPtyOutput;
        _session.ProcessExited += _ =>
            DispatcherQueue.TryEnqueue(() => PostToPage("\r\n\u001b[90m[shell exited]\u001b[0m\r\n"));

        Browser.Source = new Uri("https://overlight.local/index.html");
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
                StartShellIfNeeded();
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
                        StartShellIfNeeded();
                    }
                }
                break;
        }
    }

    private void StartShellIfNeeded()
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

        if (!_session.Start(shell, _pendingCols, _pendingRows))
        {
            PostToPage("\u001b[91mfailed to start pseudo console session\u001b[0m\r\n");
        }
    }

    private void OnPtyOutput(ReadOnlyMemory<byte> data)
    {
        string text = Encoding.UTF8.GetString(data.Span);
        DispatcherQueue.TryEnqueue(() => PostToPage(text));
    }

    private void PostToPage(string text) => Browser.CoreWebView2?.PostWebMessageAsString(text);

    private void OnUnloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => _session.Dispose();
}
