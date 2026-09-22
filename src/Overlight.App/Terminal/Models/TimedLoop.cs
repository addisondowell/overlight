using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Overlight.App.Terminal.Models;

/// <summary>
/// A user-defined "run this prompt every N" schedule. Owns its own
/// countdown state so the UI can bind directly to Remaining/Progress
/// without the view needing to know how ticking works.
/// </summary>
public sealed class TimedLoop : INotifyPropertyChanged
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Prompt { get; }
    public TimeSpan Interval { get; }
    public int TriggerCount { get; private set; }

    private DateTimeOffset _nextTriggerAt;
    public DateTimeOffset NextTriggerAt
    {
        get => _nextTriggerAt;
        private set => SetField(ref _nextTriggerAt, value);
    }

    private TimeSpan _remaining;
    public TimeSpan Remaining
    {
        get => _remaining;
        private set => SetField(ref _remaining, value);
    }

    /// <summary>0.0 (just fired) to 1.0 (about to fire) for a progress-bar-style countdown.</summary>
    private double _progress;
    public double Progress
    {
        get => _progress;
        private set => SetField(ref _progress, value);
    }

    public string ShortId => Id.ToString("N")[..8];

    /// <summary>mm:ss display string. Not its own backing field — Tick()
    /// raises PropertyChanged for this name alongside Remaining/Progress
    /// so x:Bind can target it directly.</summary>
    public string RemainingLabel => Remaining.ToString(@"mm\:ss");

    /// <summary>Single composed line for the countdown card footer, kept
    /// as one bindable string rather than several inline Runs so the
    /// binding shape stays simple.</summary>
    public string StatusLine => $"next in {RemainingLabel} · {ShortId}";

    public TimedLoop(string prompt, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Loop interval must be positive.");
        }

        Prompt = prompt;
        Interval = interval;
        _nextTriggerAt = DateTimeOffset.Now + interval;
        _remaining = interval;
    }

    /// <summary>
    /// Called by LoopScheduler roughly once a second. Returns true if the
    /// loop just fired (caller is responsible for executing the prompt
    /// and this call also resets the countdown for the next cycle).
    /// </summary>
    public bool Tick(DateTimeOffset now)
    {
        bool fired = false;

        if (now >= NextTriggerAt)
        {
            TriggerCount++;
            NextTriggerAt = now + Interval;
            fired = true;
        }

        TimeSpan remaining = NextTriggerAt - now;
        Remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        Progress = 1.0 - (Remaining.TotalSeconds / Interval.TotalSeconds);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusLine)));

        return fired;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
