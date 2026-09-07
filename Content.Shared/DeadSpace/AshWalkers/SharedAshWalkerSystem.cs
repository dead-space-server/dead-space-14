using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Examine;
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
        SubscribeLocalEvent<LavalandFaunaComponent, DamageModifyEvent>(OnFaunaDamageModify);
        SubscribeLocalEvent<AshWalkerClothingComponent, BeingEquippedAttemptEvent>(OnClothingEquipAttempt);
        SubscribeLocalEvent<AshWalkerClothingComponent, ExaminedEvent>(OnClothingExamined);
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

    private void OnFaunaDamageModify(Entity<LavalandFaunaComponent> ent, ref DamageModifyEvent args)
    {
        if (!TryComp<AshWalkerComponent>(args.Origin, out var walker))
            return;

        var damage = new DamageSpecifier(args.Damage);
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount > 0)
                damage.DamageDict[type] = amount * walker.FaunaDamageMultiplier;
        }

        args.Damage = damage;
    }

    private void OnClothingEquipAttempt(Entity<AshWalkerClothingComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled ||
            !TryComp<ClothingComponent>(ent, out var clothing) ||
            (args.SlotFlags & clothing.Slots) == 0 ||
            HasComp<AshWalkerComponent>(args.EquipTarget))
            return;

        args.Reason = "ash-walker-clothing-cannot-equip";
        args.Cancel();
    }

    private void OnClothingExamined(Entity<AshWalkerClothingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("ash-walker-clothing-examine"));
    }
}
