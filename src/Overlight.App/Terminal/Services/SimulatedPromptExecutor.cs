namespace Overlight.App.Terminal.Services;

/// <summary>
/// Placeholder IPromptExecutor: does not call any real Claude surface.
/// Exists so the loop scheduler, countdown UI, and transcript can be
/// built and exercised end-to-end before real execution is wired up.
/// Every response is prefixed "[simulated]" so it can never be mistaken
/// for a real model reply in the terminal transcript.
/// </summary>
public sealed class SimulatedPromptExecutor : IPromptExecutor
{
    public Task<string> ExecuteAsync(string prompt, CancellationToken cancellationToken)
    {
        string truncated = prompt.Length > 60 ? prompt[..60] + "…" : prompt;
        return Task.FromResult($"[simulated] would send to Claude: \"{truncated}\"");
    }
}
