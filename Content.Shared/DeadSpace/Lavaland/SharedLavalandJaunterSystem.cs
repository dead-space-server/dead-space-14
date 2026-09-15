using Content.Shared.Chasm;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Inventory;
using Robust.Shared.Network;

namespace Content.Shared.DeadSpace.Lavaland;

public sealed class SharedLavalandJaunterSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChasmFallingAttemptEvent>(OnChasmFallingAttempt);
    }

    private void OnChasmFallingAttempt(ChasmFallingAttemptEvent args)
    {
        if (!_net.IsClient ||
            args.Cancelled ||
            !TryFindEquippedJaunter(args.Tripper, out _))
        {
            return;
        }

        args.Cancel();
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
