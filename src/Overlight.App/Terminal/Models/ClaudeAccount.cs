namespace Overlight.App.Terminal.Models;

/// <summary>
/// A locally-defined label for a work context ("personal", "work",
/// "client-x"), not an authenticated Claude login. See
/// Terminal/Services/AccountManager.cs for why: wiring this to real
/// Claude account auth is a separate decision (which surface — claude.ai
/// session, Claude Code profile, API key — hasn't been made yet).
/// </summary>
public sealed class ClaudeAccount
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // Not `required`: AccountRow (which wraps this) is bound via x:Bind,
    // and the generated XamlTypeInfo.g.cs constructs+assigns properties
    // in a way `required` rejects (CS9035/CS8852) — see AccountRow.cs.
    public string Label { get; init; } = string.Empty;

    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
