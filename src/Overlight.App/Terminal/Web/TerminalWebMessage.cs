namespace Overlight.App.Terminal.Web;

/// <summary>
/// Shape of every message xterm.js posts up to the host over WebView2's
/// message channel. "input" carries a keystroke to forward into the
/// pty, "resize" carries the terminal's current column/row count,
/// "ready" signals the page finished loading and the shell can start.
/// </summary>
public sealed record TerminalWebMessage(string Type, string? Data, int? Cols, int? Rows);
