using Content.Shared.GPS.Components;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Content.Shared.DeadSpace.Lavaland; // DS14
using Robust.Shared.Random; // DS14

namespace Content.Client.GPS.UI;

public sealed class HandheldGpsStatusControl : Control
{
    private readonly Entity<HandheldGPSComponent> _parent;
    private readonly RichTextLabel _label;
    private float _updateDif;
    private readonly IEntityManager _entMan;
    private readonly SharedTransformSystem _transform;

    public HandheldGpsStatusControl(Entity<HandheldGPSComponent> parent)
    {
        _parent = parent;
        _entMan = IoCManager.Resolve<IEntityManager>();
        _transform = _entMan.System<TransformSystem>();
        _label = new RichTextLabel { StyleClasses = { StyleClass.ItemStatus } };
        AddChild(_label);
        UpdateGpsDetails();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // don't display the label if the gps component is being removed
        if (_parent.Comp.LifeStage > ComponentLifeStage.Running)
        {
            _label.Visible = false;
            return;
        }

        _updateDif += args.DeltaSeconds;
        if (_updateDif < _parent.Comp.UpdateRate)
            return;

        _updateDif -= _parent.Comp.UpdateRate;

        UpdateGpsDetails();
    }

    private void UpdateGpsDetails()
    {
        var posText = "Error";
        if (_entMan.TryGetComponent(_parent, out TransformComponent? transComp))
        {
            var pos = _transform.GetMapCoordinates(_parent.Owner, xform: transComp);
            var x = (int)pos.X;
            var y = (int)pos.Y;
            posText = $"({x}, {y})";
            // DS14-start: lavaland deep night jams the GPS
            if (_entMan.System<LavalandDayCycleSystem>().IsTrackerJammed(transComp.MapUid))
            {
                var random = IoCManager.Resolve<IRobustRandom>();
                posText = $"({random.Next(-999, 1000)}, ???)";
            }
            // DS14-end
        }
        _label.SetMarkup(Loc.GetString("handheld-gps-coordinates-title", ("coordinates", posText)));
    }
}
