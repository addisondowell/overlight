using Overlight.App.Terminal.Services;

namespace Overlight.App.Terminal;

/// <summary>
/// Process-lifetime singletons for the terminal module, so loop state
/// and the active account survive closing and reopening the terminal
/// window. Deliberately not a DI container — two services don't need one.
/// </summary>
public static class TerminalServices
{
    public static LoopScheduler LoopScheduler { get; } = new(new SimulatedPromptExecutor());
    public static AccountManager AccountManager { get; } = new();

    static TerminalServices()
    {
        // Loops are a background concern, not tied to the terminal
        // window being open — start the scheduler once for the app's
        // lifetime so a loop keeps firing even after its window is
        // closed. Relies on this class first being touched from the UI
        // thread (true today: MainWindow is the only place that does).
        LoopScheduler.Start();
    }
}
