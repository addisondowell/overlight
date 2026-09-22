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
    public required string Label { get; init; }
    public string? Note { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}
