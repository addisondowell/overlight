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
    // Not `required`: this type is bound via x:Bind in TerminalWindow.xaml,
    // and the WinUI XAML compiler's generated XamlTypeInfo.g.cs constructs
    // instances via a parameterless constructor + separate property sets,
    // which `required` members reject (CS9035/CS8852). Every call site
    // already sets both via an object initializer regardless.
    public ClaudeAccount Account { get; init; } = null!;
    public bool IsActive { get; init; }
    public string Marker => IsActive ? "●" : " ";
    public string Label => Account.Label;
}
