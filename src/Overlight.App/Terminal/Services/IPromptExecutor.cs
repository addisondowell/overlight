namespace Overlight.App.Terminal.Services;

/// <summary>
/// Whatever actually sends a triggered loop's prompt somewhere and gets
/// a response back. Kept behind an interface because that "somewhere"
/// hasn't been decided yet — the Claude Code CLI, the Messages API
/// directly, a claude.ai session — each has different auth and account
/// implications. Swap the implementation registered in
/// TerminalServices.cs once that's settled.
/// </summary>
public interface IPromptExecutor
{
    Task<string> ExecuteAsync(string prompt, CancellationToken cancellationToken);
}
