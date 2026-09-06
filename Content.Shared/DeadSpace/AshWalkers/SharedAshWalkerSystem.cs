using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;

namespace Content.Shared.DeadSpace.AshWalkers;

public sealed class SharedAshWalkerSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshWalkerComponent, IsEquippingTargetAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<GunComponent, AttemptShootEvent>(OnShootAttempt);
    }

    private void OnEquipAttempt(Entity<AshWalkerComponent> ent, ref IsEquippingTargetAttemptEvent args)
    {
        if (args.Cancelled ||
            (args.SlotFlags & SlotFlags.FEET) == 0 ||
            _whitelist.IsValid(ent.Comp.FootwearWhitelist, args.Equipment))
            return;

        args.Reason = "ash-walker-cannot-equip-footwear";
        args.Cancel();
    }

    private void OnShootAttempt(Entity<GunComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled ||
            !TryComp<AshWalkerComponent>(args.User, out var walker) ||
            _whitelist.IsValid(walker.GunWhitelist, ent))
            return;

        args.Message = Loc.GetString("ash-walker-cannot-shoot");
        args.Cancelled = true;
    }
}
