using Content.Shared.Sectants;
using Robust.Shared.Map;

namespace Content.Server.Sectants;

public sealed class SectantFemaleBlinkSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantFemaleBlinkActionEvent>(OnBlink);
    }

    private void OnBlink(SectantFemaleBlinkActionEvent args)
    {
        var uid = args.Performer;
        var xform = Transform(uid);

        MapCoordinates mapTarget;
        try
        {
            mapTarget = _xform.ToMapCoordinates(args.Target);
        }
        catch
        {
            return;
        }

        if (mapTarget.MapId != xform.MapID)
            return;

        var origin = _xform.GetWorldPosition(uid);
        if ((mapTarget.Position - origin).Length() > 6f)
            return;

        _xform.SetWorldPosition(uid, mapTarget.Position);
        Spawn("SectantBlinkEffect", _xform.GetMapCoordinates(uid));
        args.Handled = true;
    }
}
