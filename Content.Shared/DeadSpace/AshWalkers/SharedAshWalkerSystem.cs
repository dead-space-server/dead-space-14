using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;

namespace Content.Shared.DeadSpace.AshWalkers;

public sealed class SharedAshWalkerSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AshWalkerComponent, IsEquippingTargetAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<AshWalkerComponent, UserOpenActivatableUIAttemptEvent>(OnOpenUiAttempt);
        SubscribeLocalEvent<BoundUserInterfaceMessageAttempt>(OnUiMessageAttempt);
        SubscribeLocalEvent<GunComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<LavalandFaunaComponent, DamageModifyEvent>(OnFaunaDamageModify);
        SubscribeLocalEvent<AshWalkerClothingComponent, BeingEquippedAttemptEvent>(OnClothingEquipAttempt);
        SubscribeLocalEvent<AshWalkerClothingComponent, ExaminedEvent>(OnClothingExamined);
    }

    private void OnOpenUiAttempt(Entity<AshWalkerComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (args.Cancelled || !_ui.HasUi(args.Target, ShuttleConsoleUiKey.Key))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("ash-walker-cannot-pilot"), args.Target, ent);
    }

    private void OnUiMessageAttempt(BoundUserInterfaceMessageAttempt args)
    {
        if (args.Cancelled || args.UiKey is not ShuttleConsoleUiKey.Key || !HasComp<AshWalkerComponent>(args.Actor))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("ash-walker-cannot-pilot"), args.Target, args.Actor);
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
        var multiplier = walker.FaunaDamageMultiplier;
        if (args.Origin is { } hunter && _status.TryGetStatusEffect(hunter, "AshWalkerHuntEffect", out var effect) &&
            TryComp<AshWalkerRitualEffectComponent>(effect, out var ritual))
            multiplier *= ritual.HuntMultiplier;

        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount > 0)
                damage.DamageDict[type] = amount * multiplier;
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
