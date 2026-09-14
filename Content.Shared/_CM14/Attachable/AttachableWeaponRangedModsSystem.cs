using Content.Shared._CM14.Attachable.Components;
using Content.Shared._CM14.Attachable.Events;
using Content.Shared._CM14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
namespace Content.Shared._CM14.Attachable;
public sealed class AttachableWeaponRangedModsSystem : EntitySystem
{
    [Dependency] private readonly CMGunSystem _cmGunSystem = default!;
    // DS14-start
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    // DS14-end
    public override void Initialize()
    {
        // DS14-start
        SubscribeLocalEvent<AttachableWeaponRangedModsComponent, AttachableRelayedEvent<GunRefreshModifiersEvent>>(OnRangedModsRefreshModifiers);
        SubscribeLocalEvent<AttachableWeaponRangedModsComponent, AttachableAlteredEvent>(OnRangedModsAltered);
        SubscribeLocalEvent<AttachableWeaponRangedModsComponent, AttachableRelayedEvent<GetGunDamageModifierEvent>>(OnRangedModsGetGunDamage);
        SubscribeLocalEvent<AttachableWeaponWieldedRangedModsComponent, AttachableRelayedEvent<GunRefreshModifiersEvent>>(OnWieldedRangedModsRefreshModifiers);
        SubscribeLocalEvent<AttachableWeaponWieldedRangedModsComponent, AttachableAlteredEvent>(OnWieldedRangedModsAttachableAltered);
        SubscribeLocalEvent<AttachableWeaponWieldedRangedModsComponent, AttachableRelayedEvent<GetGunDamageModifierEvent>>(OnWieldedRangedModsGetGunDamage);
        SubscribeLocalEvent<AttachableWeaponRangedModsToggleableComponent, AttachableAlteredEvent>(OnWeaponModifiersAltered);
        SubscribeLocalEvent<AttachableWeaponRangedModsToggleableComponent, AttachableRelayedEvent<GunRefreshModifiersEvent>>(OnToggleableRangedModsRefreshModifiers);
        SubscribeLocalEvent<AttachableWeaponRangedModsToggleableComponent, AttachableRelayedEvent<GetGunDamageModifierEvent>>(OnToggleableRangedModsGetGunDamage);
        // DS14-end
    }
    // DS14-start
    private void OnRangedModsRefreshModifiers(Entity<AttachableWeaponRangedModsComponent> ent, ref AttachableRelayedEvent<GunRefreshModifiersEvent> args)
    {
        ApplyModifiers(ent.Comp.Modifiers, ref args.Args);
    }
    private void OnRangedModsAltered(Entity<AttachableWeaponRangedModsComponent> attachable,
        ref AttachableAlteredEvent args)
    {
        _cmGunSystem.RefreshGunDamageMultiplier(args.Holder);
        _gunSystem.RefreshModifiers(args.Holder);
    }
    private void OnRangedModsGetGunDamage(Entity<AttachableWeaponRangedModsComponent> ent, ref AttachableRelayedEvent<GetGunDamageModifierEvent> args)
    {
        args.Args.Multiplier += ent.Comp.Modifiers.DamageFlat;
    }
    private void OnWieldedRangedModsRefreshModifiers(Entity<AttachableWeaponWieldedRangedModsComponent> ent, ref AttachableRelayedEvent<GunRefreshModifiersEvent> args)
    {
        var set = TryComp(args.Holder, out WieldableComponent? wieldable) && wieldable.Wielded
            ? ent.Comp.Wielded
            : ent.Comp.Unwielded;
        ApplyModifiers(set, ref args.Args);
    }
    private void OnWieldedRangedModsAttachableAltered(Entity<AttachableWeaponWieldedRangedModsComponent> ent, ref AttachableAlteredEvent args)
    {
        _cmGunSystem.RefreshGunDamageMultiplier(args.Holder);
        _gunSystem.RefreshModifiers(args.Holder);
    }
    private void OnWieldedRangedModsGetGunDamage(Entity<AttachableWeaponWieldedRangedModsComponent> ent, ref AttachableRelayedEvent<GetGunDamageModifierEvent> args)
    {
        var set = TryComp(args.Holder, out WieldableComponent? wieldable) && wieldable.Wielded
            ? ent.Comp.Wielded
            : ent.Comp.Unwielded;
        args.Args.Multiplier += set.DamageFlat;
    }
    private void OnWeaponModifiersAltered(Entity<AttachableWeaponRangedModsToggleableComponent> attachable, ref AttachableAlteredEvent args)
    {
        _cmGunSystem.RefreshGunDamageMultiplier(args.Holder);
        _gunSystem.RefreshModifiers(args.Holder);
    }

    private void OnToggleableRangedModsRefreshModifiers(Entity<AttachableWeaponRangedModsToggleableComponent> ent,
        ref AttachableRelayedEvent<GunRefreshModifiersEvent> args)
    {
        var set = GetToggleableModifiers(ent, args.Holder);
        ApplyModifiers(set, ref args.Args);
    }

    private void OnToggleableRangedModsGetGunDamage(Entity<AttachableWeaponRangedModsToggleableComponent> ent,
        ref AttachableRelayedEvent<GetGunDamageModifierEvent> args)
    {
        args.Args.Multiplier += GetToggleableModifiers(ent, args.Holder).DamageFlat;
    }

    private AttachableWeaponRangedModifierSet GetToggleableModifiers(
        Entity<AttachableWeaponRangedModsToggleableComponent> ent,
        EntityUid holder)
    {
        var active = TryComp(ent, out AttachableToggleableComponent? toggleable) && toggleable.Active;
        var wielded = TryComp(holder, out WieldableComponent? wieldable) && wieldable.Wielded;

        return (active, wielded) switch
        {
            (true, true) => ent.Comp.ActiveWielded,
            (true, false) => ent.Comp.ActiveUnwielded,
            (false, true) => ent.Comp.InactiveWielded,
            _ => ent.Comp.InactiveUnwielded,
        };
    }

    private static void ApplyModifiers(AttachableWeaponRangedModifierSet set, ref GunRefreshModifiersEvent args)
    {
        args.ShotsPerBurst = Math.Max(args.ShotsPerBurst + set.ShotsPerBurst, 1);
        args.CameraRecoilScalar = Math.Max(args.CameraRecoilScalar + set.RecoilFlat, 0);
        args.AngleIncrease = new Angle(Math.Max(args.AngleIncrease.Theta * set.AngleIncrease, 0.0));
        args.AngleDecay = new Angle(Math.Max(args.AngleDecay.Theta * set.AngleDecay, 0.0));
        args.MinAngle = new Angle(Math.Max(args.MinAngle.Theta * set.MinAngle, 0.0));
        args.MaxAngle = new Angle(Math.Max(args.MaxAngle.Theta * set.MaxAngle, args.MinAngle.Theta));
        args.FireRate = Math.Max(args.FireRate * set.FireRate, 0);
        args.ProjectileSpeed = Math.Max((args.ProjectileSpeed + set.ProjectileSpeedFlat) * set.ProjectileSpeedMultiplier, 0);
    }
    // DS14-end
}
