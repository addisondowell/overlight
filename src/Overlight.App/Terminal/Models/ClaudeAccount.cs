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
    // Plain get/set on all four, not `init`: AccountRow (which wraps
    // this) is bound via x:Bind, and the generated XamlTypeInfo.g.cs
    // assigns properties as separate statements after construction —
    // `init` rejects that (CS8852) regardless of `required` — see the
    // comment in AccountRow.cs. Every call site still only ever sets
    // these once, via an object initializer right after `new`.
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
