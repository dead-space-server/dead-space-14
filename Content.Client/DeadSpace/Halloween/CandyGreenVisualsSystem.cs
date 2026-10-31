using Content.Shared.DeadSpace.Halloween;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Client.DeadSpace.Halloween;

/// <summary>
/// Tints a green-marked entity and everything on it (clothing, held items) green,
/// restoring the original colors when the mark is removed.
/// </summary>
public sealed class CandyGreenVisualsSystem : EntitySystem
{
    private static readonly Color Green = Color.FromHex("#5fbf4a");

    // green root entity -> (tinted entity -> original color)
    private readonly Dictionary<EntityUid, Dictionary<EntityUid, Color>> _originals = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CandyGreenComponent, ComponentStartup>(OnAdded);
        SubscribeLocalEvent<CandyGreenComponent, ComponentShutdown>(OnRemoved);
        SubscribeLocalEvent<CandyGreenComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CandyGreenComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
    }

    private void OnAdded(Entity<CandyGreenComponent> ent, ref ComponentStartup args)
    {
        TintTree(ent.Owner, ent.Owner);
    }

    private void OnRemoved(Entity<CandyGreenComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent.Owner))
            RestoreTree(ent.Owner, ent.Owner);

        _originals.Remove(ent.Owner);
    }

    private void OnInserted(Entity<CandyGreenComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        TintTree(args.Entity, ent.Owner);
    }

    private void OnContainerRemoved(Entity<CandyGreenComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        RestoreTree(args.Entity, ent.Owner);
    }

    private void TintTree(EntityUid uid, EntityUid root)
    {
        if (TerminatingOrDeleted(uid))
            return;

        TintEntity(uid, root);

        var enumerator = Transform(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
            TintTree(child, root);
    }

    private void TintEntity(EntityUid uid, EntityUid root)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!_originals.TryGetValue(root, out var known))
        {
            known = new Dictionary<EntityUid, Color>();
            _originals[root] = known;
        }

        if (known.ContainsKey(uid))
            return;

        known[uid] = sprite.Color;
        sprite.Color = sprite.Color * Green;
    }

    private void RestoreTree(EntityUid uid, EntityUid root)
    {
        if (TerminatingOrDeleted(uid))
            return;

        RestoreEntity(uid, root);

        var enumerator = Transform(uid).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
            RestoreTree(child, root);
    }

    private void RestoreEntity(EntityUid uid, EntityUid root)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!_originals.TryGetValue(root, out var known) || !known.TryGetValue(uid, out var original))
            return;

        sprite.Color = original;
        known.Remove(uid);
    }
}