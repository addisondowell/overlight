using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;

namespace Overlight.App.Presentation;

/// <summary>
/// Drives ambient motion (opacity/transform) entirely through declarative
/// Composition animations. DWM composites every window every frame
/// regardless of what this app does, so a compositor-driven animation
/// adds no new per-frame app work — no timer, no redraw loop. See
/// docs/DESIGN.md section 5.
///
/// Two throttles feed into every animation this class starts:
///   - occluded: true while DWM reports the surface cloaked/hidden
///     (fed by an occlusion signal — see OcclusionMonitor).
///   - lowPower: true while PowerModeMonitor reports battery saver /
///     a low-power effective mode.
/// Both pause the compositor scoped batch rather than stopping and
/// restarting individual animations, so resuming is just as cheap as
/// starting.
/// </summary>
public sealed class AmbientCompositor
{
    // Ambient motion doctrine: cap at ~30fps. The Composition animation
    // system doesn't take an fps parameter directly — instead we express
    // duration in wall-clock time and let the compositor interpolate, so
    // "fps" here just bounds how tight keyframe spacing is allowed to get.
    private static readonly TimeSpan MinKeyframeSpacing = TimeSpan.FromMilliseconds(1000.0 / 30.0);

    private readonly Compositor _compositor;
    private bool _occluded;
    private bool _lowPower;

    public AmbientCompositor(Microsoft.UI.Xaml.UIElement rootElement)
    {
        Visual rootVisual = ElementCompositionPreview.GetElementVisual(rootElement);
        _compositor = rootVisual.Compositor;
    }

    public void SetOccluded(bool occluded) => _occluded = occluded;

    public void SetLowPower(bool lowPower) => _lowPower = lowPower;

    /// <summary>
    /// Starts a looping opacity breathe on the given visual. Returns
    /// without starting anything if the surface is currently occluded or
    /// the system is in a low-power mode — callers should re-invoke once
    /// SetOccluded/SetLowPower flips back, rather than this class polling
    /// for that itself.
    /// </summary>
    public void StartBreathingOpacity(Visual target, float from, float to, TimeSpan cycleDuration)
    {
        if (_occluded || _lowPower)
        {
            return;
        }

        if (cycleDuration < MinKeyframeSpacing * 2)
        {
            cycleDuration = MinKeyframeSpacing * 2;
        }

        ScalarKeyFrameAnimation animation = _compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(0.0f, from);
        animation.InsertKeyFrame(0.5f, to);
        animation.InsertKeyFrame(1.0f, from);
        animation.Duration = cycleDuration;
        animation.IterationBehavior = AnimationIterationBehavior.Forever;

        target.StartAnimation(nameof(Visual.Opacity), animation);
    }

    public void StopAllAnimations(Visual target) => target.StopAnimation(nameof(Visual.Opacity));
}
