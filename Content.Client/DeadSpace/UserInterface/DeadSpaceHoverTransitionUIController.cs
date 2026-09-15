// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.UserInterface;

/// <summary>
/// Smooths color-only transitions between flat button pseudo-states without
/// changing their stylesheet-owned geometry.
/// </summary>
public sealed class DeadSpaceHoverTransitionUIController : UIController
{
    private const float Duration = 0.14f;

    private readonly HashSet<Control> _tracked = [];
    private readonly Dictionary<ContainerButton, Transition> _transitions = [];

    public override void Initialize()
    {
        TrackTree(UIManager.RootControl);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        foreach (var (button, transition) in _transitions.ToArray())
        {
            if (!ReferenceEquals(button.StyleBoxOverride, transition.Box))
            {
                _transitions.Remove(button);
                continue;
            }

            transition.Elapsed += args.DeltaSeconds;
            var progress = Math.Clamp(transition.Elapsed / Duration, 0f, 1f);
            var eased = progress * progress * (3f - 2f * progress);
            transition.Box.BackgroundColor = Color.InterpolateBetween(
                transition.StartBackground,
                transition.TargetBackground,
                eased);
            transition.Box.BorderColor = Color.InterpolateBetween(
                transition.StartBorder,
                transition.TargetBorder,
                eased);

            if (progress < 1f)
                continue;

            button.StyleBoxOverride = null;
            _transitions.Remove(button);
        }
    }

    private void TrackTree(Control control)
    {
        if (!_tracked.Add(control))
            return;

        control.OnChildAdded += TrackTree;
        control.OnChildRemoved += UntrackTree;

        if (control is ContainerButton button)
        {
            button.OnMouseEntered += OnHoverChanged;
            button.OnMouseExited += OnHoverChanged;
        }

        foreach (var child in control.Children.ToArray())
            TrackTree(child);
    }

    private void UntrackTree(Control control)
    {
        foreach (var child in control.Children.ToArray())
            UntrackTree(child);

        control.OnChildAdded -= TrackTree;
        control.OnChildRemoved -= UntrackTree;

        if (control is ContainerButton button)
        {
            button.OnMouseEntered -= OnHoverChanged;
            button.OnMouseExited -= OnHoverChanged;

            if (_transitions.Remove(button, out var transition) &&
                ReferenceEquals(button.StyleBoxOverride, transition.Box))
            {
                button.StyleBoxOverride = null;
            }
        }

        _tracked.Remove(control);
    }

    private void OnHoverChanged(GUIMouseHoverEventArgs args)
    {
        if (args.SourceControl is not ContainerButton button || GetCurrentBox(button) is not { } current)
            return;

        // BaseButton updates its pseudo-class after raising the hover event.
        UIManager.DeferAction(() => BeginTransition(button, current));
    }

    private void BeginTransition(ContainerButton button, StyleBoxFlat current)
    {
        if (!_tracked.Contains(button))
            return;

        // A few controls intentionally own a persistent override (for example color previews).
        // Do not replace or clear UI state that belongs to the control itself.
        if (button.StyleBoxOverride != null &&
            (!_transitions.TryGetValue(button, out var active) ||
             !ReferenceEquals(button.StyleBoxOverride, active.Box)))
        {
            return;
        }

        button.ForceRunStyleUpdate();
        if (!button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var resolved) ||
            resolved is not StyleBoxFlat target ||
            !HasSameGeometry(current, target))
        {
            return;
        }

        var animated = new StyleBoxFlat(target)
        {
            BackgroundColor = current.BackgroundColor,
            BorderColor = current.BorderColor,
        };
        button.StyleBoxOverride = animated;
        _transitions[button] = new Transition(animated, current, target);
    }

    private StyleBoxFlat? GetCurrentBox(ContainerButton button)
    {
        if (button.StyleBoxOverride is StyleBoxFlat current)
        {
            return _transitions.TryGetValue(button, out var active) &&
                   ReferenceEquals(current, active.Box)
                ? new StyleBoxFlat(current)
                : null;
        }

        if (button.StyleBoxOverride != null)
            return null;

        button.ForceRunStyleUpdate();
        return button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var resolved) &&
               resolved is StyleBoxFlat flat
            ? new StyleBoxFlat(flat)
            : null;
    }

    private static bool HasSameGeometry(StyleBoxFlat current, StyleBoxFlat target)
    {
        return current.BorderThickness == target.BorderThickness &&
               current.GetContentMargin(StyleBox.Margin.Left) == target.GetContentMargin(StyleBox.Margin.Left) &&
               current.GetContentMargin(StyleBox.Margin.Top) == target.GetContentMargin(StyleBox.Margin.Top) &&
               current.GetContentMargin(StyleBox.Margin.Right) == target.GetContentMargin(StyleBox.Margin.Right) &&
               current.GetContentMargin(StyleBox.Margin.Bottom) == target.GetContentMargin(StyleBox.Margin.Bottom);
    }

    private sealed class Transition(StyleBoxFlat box, StyleBoxFlat start, StyleBoxFlat target)
    {
        public readonly StyleBoxFlat Box = box;
        public readonly Color StartBackground = start.BackgroundColor;
        public readonly Color StartBorder = start.BorderColor;
        public readonly Color TargetBackground = target.BackgroundColor;
        public readonly Color TargetBorder = target.BorderColor;
        public float Elapsed;
    }
}
