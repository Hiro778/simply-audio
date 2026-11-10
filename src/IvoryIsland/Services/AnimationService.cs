using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace IvoryIsland.Services;

/// <summary>
/// Native Windows Composition animation service.
/// Uses GPU-accelerated Visual opacity and scale/offset animations with interruptible keyframe curves.
/// Strictly adheres to timing budgets:
/// - Expand: 220ms (spec: 200ms–260ms)
/// - Collapse: 200ms (spec: 180ms–240ms)
/// - Source Transition: 180ms (spec: 160ms–220ms)
/// - Control Feedback: 120ms (spec: 100ms–160ms)
/// </summary>
public sealed class AnimationService
{
    public static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(220);
    public static readonly TimeSpan CollapseDuration = TimeSpan.FromMilliseconds(200);
    public static readonly TimeSpan SourceTransitionDuration = TimeSpan.FromMilliseconds(180);
    public static readonly TimeSpan ControlFeedbackDuration = TimeSpan.FromMilliseconds(120);

    public void AnimateCapsuleExpand(UIElement capsuleElement)
    {
        if (capsuleElement is null)
            return;

        Visual visual = ElementCompositionPreview.GetElementVisual(capsuleElement);
        Compositor compositor = visual.Compositor;

        UpdateCenterPoint(capsuleElement, visual);

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.16f, 1.0f), new Vector2(0.3f, 1.0f));

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0.0f, Math.Clamp(visual.Opacity, 0.2f, 0.85f));
        opacityAnim.InsertKeyFrame(1.0f, 1.0f, easing);
        opacityAnim.Duration = ExpandDuration;

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0.0f, new Vector3(0.94f, 0.96f, 1.0f));
        scaleAnim.InsertKeyFrame(1.0f, Vector3.One, easing);
        scaleAnim.Duration = ExpandDuration;

        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);
        visual.StartAnimation(nameof(visual.Scale), scaleAnim);
    }

    public void AnimateCapsuleCollapse(UIElement capsuleElement, float targetOpacity = 0.0f)
    {
        if (capsuleElement is null)
            return;

        Visual visual = ElementCompositionPreview.GetElementVisual(capsuleElement);
        Compositor compositor = visual.Compositor;

        UpdateCenterPoint(capsuleElement, visual);

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.4f, 0.0f), new Vector2(0.2f, 1.0f));

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(1.0f, targetOpacity, easing);
        opacityAnim.Duration = CollapseDuration;

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(1.0f, new Vector3(0.95f, 0.95f, 1.0f), easing);
        scaleAnim.Duration = CollapseDuration;

        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);
        visual.StartAnimation(nameof(visual.Scale), scaleAnim);
    }

    public void AnimateSourceSwitchTransition(UIElement metadataContainer)
    {
        if (metadataContainer is null)
            return;

        Visual visual = ElementCompositionPreview.GetElementVisual(metadataContainer);
        Compositor compositor = visual.Compositor;

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.22f, 1.0f), new Vector2(0.36f, 1.0f));

        var opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.InsertKeyFrame(0.0f, 0.35f);
        opacityAnim.InsertKeyFrame(1.0f, 1.0f, easing);
        opacityAnim.Duration = SourceTransitionDuration;

        var offsetAnim = compositor.CreateVector3KeyFrameAnimation();
        offsetAnim.InsertKeyFrame(0.0f, new Vector3(0.0f, 3.0f, 0.0f));
        offsetAnim.InsertKeyFrame(1.0f, Vector3.Zero, easing);
        offsetAnim.Duration = SourceTransitionDuration;

        visual.StartAnimation(nameof(visual.Opacity), opacityAnim);
        visual.StartAnimation(nameof(visual.Offset), offsetAnim);
    }

    public void AnimateControlPressFeedback(UIElement buttonElement)
    {
        if (buttonElement is null)
            return;

        Visual visual = ElementCompositionPreview.GetElementVisual(buttonElement);
        Compositor compositor = visual.Compositor;

        UpdateCenterPoint(buttonElement, visual);

        var easing = compositor.CreateCubicBezierEasingFunction(new Vector2(0.25f, 1.0f), new Vector2(0.5f, 1.0f));

        var scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.InsertKeyFrame(0.0f, Vector3.One);
        scaleAnim.InsertKeyFrame(0.35f, new Vector3(0.91f, 0.91f, 1.0f), easing);
        scaleAnim.InsertKeyFrame(1.0f, Vector3.One, easing);
        scaleAnim.Duration = ControlFeedbackDuration;

        visual.StartAnimation(nameof(visual.Scale), scaleAnim);
    }

    private static void UpdateCenterPoint(UIElement element, Visual visual)
    {
        Vector2 size = element.ActualSize;
        if (size.X > 0 && size.Y > 0)
        {
            visual.CenterPoint = new Vector3(size.X * 0.5f, size.Y * 0.5f, 0.0f);
        }
    }
}
