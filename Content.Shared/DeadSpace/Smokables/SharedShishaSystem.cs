using Content.Shared.DragDrop;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Smokables;

/// <summary>Shared click routing and drag-to-self pickup, including client drag affordances.</summary>
public abstract class SharedShishaSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShishaComponent, InteractHandEvent>(OnHandInteract, before: [typeof(SharedItemSystem)]);
        SubscribeLocalEvent<ShishaComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<ShishaComponent, CanDropDraggedEvent>(OnCanDrop);
        SubscribeLocalEvent<ShishaComponent, DragDropDraggedEvent>(OnDragDrop);
    }

    private void OnHandInteract(Entity<ShishaComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        HandleHoseClick(ent, args.User);
    }

    protected virtual void HandleHoseClick(Entity<ShishaComponent> ent, EntityUid user) { }

    private void OnCanDrag(Entity<ShishaComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    private void OnCanDrop(Entity<ShishaComponent> ent, ref CanDropDraggedEvent args)
    {
        if (args.Handled || args.Target != args.User)
            return;

        args.Handled = true;
        args.CanDrop = _hands.CanPickupAnyHand(args.User, ent.Owner);
    }

    private void OnDragDrop(Entity<ShishaComponent> ent, ref DragDropDraggedEvent args)
    {
        if (args.Handled || args.Target != args.User)
            return;

        args.Handled = true;
        if (_interaction.InRangeAndAccessible(args.User, ent.Owner))
            _hands.TryPickupAnyHand(args.User, ent.Owner);
    }
}

[Serializable, NetSerializable]
public enum ShishaVisuals : byte
{
    Base,
    HoseDocked,
    State,
}
