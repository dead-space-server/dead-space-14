using Robust.Shared.GameObjects;

namespace Content.Shared.DeadSpace.Furniture;

public sealed partial class BunkBedSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BunkBedComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BunkBedComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);

        using var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            if (HasComp<BunkBedOverlayComponent>(child))
                return;
        }

        var overlay = Spawn(ent.Comp.Overlay, xform.Coordinates);
        _transform.SetParent(overlay, ent);
    }
}
