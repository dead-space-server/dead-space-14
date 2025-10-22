using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DeadSpace.Asthma.Components;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Asthma.UI;

public sealed class InhalerStatusControl : Control
{
    private readonly Entity<InhalerComponent> _parent;
    private readonly RichTextLabel _label;
    private readonly SharedSolutionContainerSystem _solutionContainers;

    private FixedPoint2 _prevVolume;
    private FixedPoint2 _prevMaxVolume;

    public InhalerStatusControl(Entity<InhalerComponent> parent, SharedSolutionContainerSystem solutionContainers)
    {
        _parent = parent;
        _solutionContainers = solutionContainers;
        _label = new RichTextLabel { StyleClasses = { StyleNano.StyleClassItemStatus } };
        AddChild(_label);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_solutionContainers.TryGetSolution(_parent.Owner, _parent.Comp.SolutionName, out _, out var solution))
            return;

        if (_prevVolume == solution.Volume
            && _prevMaxVolume == solution.MaxVolume)
            return;

        _prevVolume = solution.Volume;
        _prevMaxVolume = solution.MaxVolume;


        _label.SetMarkup(Loc.GetString("inhaler-volume-label",
            ("currentVolume", solution.Volume),
            ("totalVolume", solution.MaxVolume)));
    }
}
