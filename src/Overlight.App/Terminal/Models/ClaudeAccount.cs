namespace Overlight.App.Terminal.Models;

/// <summary>
/// A locally-defined label for a work context ("personal", "work",
/// "client-x"), not an authenticated Claude login. See
/// Terminal/Services/AccountManager.cs for why: wiring this to real
/// Claude account auth is a separate decision (which surface — claude.ai
/// session, Claude Code profile, API key — hasn't been made yet).
///
/// EnvironmentOverrides is the one piece of real isolation this class
/// provides today: without it, a shell spawned in the terminal pane
/// inherits Overlight's own process environment verbatim, which means
/// whatever Claude Code credentials/config were already in scope on the
/// machine that launched Overlight leak straight into every account's
/// shell regardless of which one is "active" — set overrides here (via
/// `account setenv`, see CommandInterpreter.cs) to actually change what
/// a given account's shell sees.
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

    /// <summary>
    /// Environment variables to override on top of the inherited
    /// environment when a shell is spawned for this account (e.g.
    /// ANTHROPIC_API_KEY, or whatever variable your Claude Code setup
    /// actually keys credentials/config off — that's specific to your
    /// machine and not something this codebase can know in advance).
    ///
    /// SECURITY NOTE: this dictionary is persisted to
    /// %LOCALAPPDATA%\Overlight\accounts.json in plain text (see
    /// AccountManager.Save). If you put a real API key in here, it sits
    /// on disk unencrypted. That's a real trade-off, not an oversight —
    /// flagging it rather than quietly shipping a plaintext secrets
    /// store without saying so.
    /// </summary>
    public Dictionary<string, string> EnvironmentOverrides { get; set; } = new();
}
