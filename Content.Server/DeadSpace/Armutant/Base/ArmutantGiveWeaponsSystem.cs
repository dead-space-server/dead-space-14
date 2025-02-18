using Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAbilities;
using Content.Server.DeadSpace.Armutant.Abilities.Components.FistAbilities;
using Content.Server.DeadSpace.Armutant.Abilities.Components.GunAbilities;
using Content.Server.DeadSpace.Armutant.Abilities.Components.ShieldAbilities;
using Content.Server.DeadSpace.Armutant.Base.Components;
using Content.Shared.Hands;

namespace Content.Server.DeadSpace.Armutant.Base;

public sealed class ArmutantGiveWeaponsSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ArmutantGiveWeaponsComponent, DidEquipHandEvent>(OnEquip);
        SubscribeLocalEvent<ArmutantGiveWeaponsComponent, DidUnequipHandEvent>(OnUnequip);
    }

    private void OnEquip(EntityUid uid, ArmutantGiveWeaponsComponent component, DidEquipHandEvent args)
    {
        if (!TryComp(args.Equipped, out MetaDataComponent? metaData))
            return;

        switch (metaData.EntityPrototype?.ID)
        {
            case "BladeArmutant":
                TryAdd<BladeDashArmutantComponent>(args.User);
                TryAdd<BladeTalonSpawnComponent>(args.User);
                TryAdd<SporeBladeComponent>(args.User);
                break;
            case "ShieldArmutant":
                TryAdd<ShieldCreateArmorComponent>(args.User);
                TryAdd<ShieldStunComponent>(args.User);
                TryAdd<VoidShieldComponent>(args.User);
                break;
            case "FistArmutant":
                TryAdd<FistStunAroundComponent>(args.User);
                TryAdd<FistMendSelfComponent>(args.User);
                TryAdd<FistBuffSpeedComponent>(args.User);
                break;
            case "GunArmutant":
                TryAdd<GunZoomComponent>(args.User);
                TryAdd<GunSmokeComponent>(args.User);
                break;
        }
    }

    private void OnUnequip(EntityUid uid, ArmutantGiveWeaponsComponent component, DidUnequipHandEvent args)
    {
        if (!TryComp(args.Unequipped, out MetaDataComponent? metaData))
            return;

        switch (metaData.EntityPrototype?.ID)
        {
            case "BladeArmutant":
                TryRemove<BladeDashArmutantComponent>(args.User);
                TryRemove<BladeTalonSpawnComponent>(args.User);
                TryRemove<SporeBladeComponent>(args.User);
                break;
            case "ShieldArmutant":
                TryRemove<ShieldCreateArmorComponent>(args.User);
                TryRemove<ShieldStunComponent>(args.User);
                TryRemove<VoidShieldComponent>(args.User);
                break;
            case "FistArmutant":
                TryRemove<FistStunAroundComponent>(args.User);
                TryRemove<FistMendSelfComponent>(args.User);
                TryRemove<FistBuffSpeedComponent>(args.User);
                break;
            case "GunArmutant":
                TryRemove<GunZoomComponent>(args.User);
                TryRemove<GunSmokeComponent>(args.User);
                break;
        }
    }

    private void TryAdd<T>(EntityUid user) where T : Component, new()
    {
        if (!HasComp<T>(user))
            AddComp<T>(user);
    }

    private void TryRemove<T>(EntityUid user) where T : Component
    {
        if (HasComp<T>(user))
            RemComp<T>(user);
    }
}
