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
    public required ClaudeAccount Account { get; init; }
    public required bool IsActive { get; init; }
    public string Marker => IsActive ? "●" : " ";
    public string Label => Account.Label;
}
