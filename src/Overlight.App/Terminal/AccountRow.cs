using Overlight.App.Terminal.Models;

namespace Overlight.App.Terminal;

/// <summary>
/// Display wrapper pairing an account with whether it's the active one.
/// ClaudeAccount itself doesn't know that — "active" is a property of
/// AccountManager's selection, not of the account — so the window
/// rebuilds a list of these whenever the account list or the active
/// selection changes, rather than the model trying to track it.
/// </summary>
public sealed class AccountRow
{
    // Plain get/set, not `init`: this type is bound via x:Bind in
    // TerminalWindow.xaml, and the WinUI XAML compiler's generated
    // XamlTypeInfo.g.cs constructs instances via a parameterless
    // constructor followed by separate property-assignment statements —
    // `init` accessors reject that regardless of `required` (CS8852).
    // Every call site already sets both via an object initializer
    // immediately after construction, so behavior is unaffected.
    public ClaudeAccount Account { get; set; } = null!;
    public bool IsActive { get; set; }
    public string Marker => IsActive ? "●" : " ";
    public string Label => Account.Label;
}
