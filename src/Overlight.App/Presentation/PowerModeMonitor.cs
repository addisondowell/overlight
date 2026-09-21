using System.Runtime.InteropServices;

namespace Overlight.App.Presentation;

public enum EffectivePowerMode
{
    BatterySaver = 0,
    BetterBattery = 1,
    Balanced = 2,
    HighPerformance = 3,
    MaxPerformance = 4,
    GameMode = 5,
    MixedReality = 6,
}

/// <summary>
/// Push-based (not polled) power mode notifications, so ambient motion
/// can throttle itself the moment the system enters battery saver / a
/// low-power state, per docs/DESIGN.md section 5. Wraps
/// PowerRegisterForEffectivePowerModeNotifications from powrprof.dll.
/// </summary>
public sealed class PowerModeMonitor : IDisposable
{
    private const int EFFECTIVE_POWER_MODE_V1 = 1;

    private delegate void EffectivePowerModeCallback(EffectivePowerMode mode, nint context);

    [DllImport("powrprof.dll", SetLastError = true)]
    private static extern int PowerRegisterForEffectivePowerModeNotifications(
        int version,
        EffectivePowerModeCallback callback,
        nint context,
        out nint registrationHandle);

    [DllImport("powrprof.dll")]
    private static extern int PowerUnregisterFromEffectivePowerModeNotifications(nint registrationHandle);

    private readonly EffectivePowerModeCallback _callback;
    private nint _registrationHandle;
    private bool _registered;

    public event Action<EffectivePowerMode>? ModeChanged;

    public PowerModeMonitor()
    {
        // Keep a field reference to the delegate so it isn't collected
        // while the unmanaged side still holds a function pointer to it.
        _callback = OnModeChanged;
    }

    public bool TryStart()
    {
        int hr = PowerRegisterForEffectivePowerModeNotifications(
            EFFECTIVE_POWER_MODE_V1, _callback, nint.Zero, out _registrationHandle);

        _registered = hr == 0;
        return _registered;
    }

    private void OnModeChanged(EffectivePowerMode mode, nint context) => ModeChanged?.Invoke(mode);

    public void Dispose()
    {
        if (_registered)
        {
            PowerUnregisterFromEffectivePowerModeNotifications(_registrationHandle);
            _registered = false;
        }
    }
}
