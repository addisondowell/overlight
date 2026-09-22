using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Overlight.App.Terminal.Models;

namespace Overlight.App.Terminal.Services;

/// <summary>
/// Owns every active timed loop and the single DispatcherTimer that
/// drives all of their countdowns. One shared 1Hz timer rather than one
/// per loop, so adding loops doesn't add timer overhead — this is a
/// foreground control-panel concern, not the ambient overlay, so a
/// 1-second UI tick is well within the performance doctrine's intent
/// even though it isn't compositor-driven.
/// </summary>
public sealed class LoopScheduler
{
    private readonly DispatcherTimer _timer;
    private readonly IPromptExecutor _executor;

    public ObservableCollection<TimedLoop> Loops { get; } = new();

    /// <summary>Raised on the UI thread whenever a loop fires, with its prompt and the executor's response.</summary>
    public event Action<TimedLoop, string>? LoopFired;

    public LoopScheduler(IPromptExecutor executor)
    {
        _executor = executor;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    public TimedLoop Add(string prompt, TimeSpan interval)
    {
        var loop = new TimedLoop(prompt, interval);
        Loops.Add(loop);
        return loop;
    }

    public bool Remove(string shortIdOrPrefix)
    {
        TimedLoop? match = Loops.FirstOrDefault(l =>
            l.ShortId.Equals(shortIdOrPrefix, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            return false;
        }

        Loops.Remove(match);
        return true;
    }

    private async void OnTick(object? sender, object e)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        // Snapshot before iterating — Tick() can fire, and a handler
        // reacting to LoopFired must not be able to mutate Loops out
        // from under this loop.
        foreach (TimedLoop loop in Loops.ToArray())
        {
            if (loop.Tick(now))
            {
                string response = await _executor.ExecuteAsync(loop.Prompt, CancellationToken.None);
                LoopFired?.Invoke(loop, response);
            }
        }
    }
}
