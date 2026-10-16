using Content.Shared.Buckle.Components;
using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.Lavaland;

public sealed class SharedLavalandJaunterSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    /// <summary>How deep into bags, body bags and the like the jaunter is looked for.</summary>
    private const int SearchDepth = 4;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
    }

    private void OnChasmFallingAttempt(ChasmFallingAttemptEvent args)
    {
        if (!_net.IsClient ||
            args.Cancelled ||
            HasComp<LavalandChromitePitComponent>(args.Chasm) ||
            !TryFindJaunterOn(args.Tripper, out _))
        {
            return;
        }

        args.Cancel();
    }

    /// <summary>
    /// The jaunter that saves whatever falls into a chasm. A living player needs it in hand or pocket.
    /// A corpse, a body bag, a rolling bed and other objects are searched through: their whole contents
    /// and whoever is buckled to them - so a jaunter on a corpse sends it to the outpost.
    /// </summary>
    public bool TryFindJaunterOn(EntityUid tripper, out Entity<LavalandJaunterComponent> jaunter)
    {
        return TryFindJaunterOn(tripper, SearchDepth, out jaunter);
    }

    private bool TryFindJaunterOn(EntityUid uid, int depth, out Entity<LavalandJaunterComponent> jaunter)
    {
        if (TryFindEquippedJaunter(uid, out jaunter))
            return true;

        if (depth <= 0 || TryComp<MobStateComponent>(uid, out var mobState) && _mobState.IsAlive(uid, mobState))
            return false;

        if (TryComp<ContainerManagerComponent>(uid, out var manager))
        {
            foreach (var container in manager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    if (TryComp<LavalandJaunterComponent>(contained, out var component))
                    {
                        jaunter = (contained, component);
                        return true;
                    }

                    if (TryFindJaunterOn(contained, depth - 1, out jaunter))
                        return true;
                }
            }
        }

        if (TryComp<StrapComponent>(uid, out var strap))
        {
            foreach (var buckled in strap.BuckledEntities)
            {
                if (TryFindJaunterOn(buckled, depth - 1, out jaunter))
                    return true;
            }
        }

        jaunter = default;
        return false;
    }

    public bool TryFindEquippedJaunter(EntityUid user, out Entity<LavalandJaunterComponent> jaunter)
    {
        jaunter = default;

        foreach (var item in _inventory.GetHandOrInventoryEntities(user, SlotFlags.POCKET))
        {
            if (!TryComp<LavalandJaunterComponent>(item, out var component))
                continue;

            jaunter = (item, component);
            return true;
        }

        return false;
    }
}
