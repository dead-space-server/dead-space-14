using Content.Shared.DragDrop;
using Content.Shared.Mobs.Systems;

namespace Content.Shared.DeadSpace.AshWalkers;

public abstract class SharedAshWalkerNestSystem : EntitySystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AshWalkerNestComponent, CanDropTargetEvent>(OnCanDrop);
    }

    private void OnCanDrop(Entity<AshWalkerNestComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = CanDrop(ent, args.User, args.Dragged);
        args.Handled = true;
    }

    protected virtual bool CanDrop(Entity<AshWalkerNestComponent> nest, EntityUid user, EntityUid body)
    {
        return Transform(nest).Anchored && HasComp<AshWalkerComponent>(user) &&
               _mobState.IsDead(body) && !HasComp<AshWalkerComponent>(body);
    }
}
