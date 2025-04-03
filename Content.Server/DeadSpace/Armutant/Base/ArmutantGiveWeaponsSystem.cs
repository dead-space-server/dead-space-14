using Content.Server.DeadSpace.Armutant.Abilities.Components.BladeAblities;
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
        if (TryComp<MetaDataComponent>(args.Equipped, out var clawsMetaData) && clawsMetaData.EntityPrototype?.ID == "BladeArmutant")
        {
            if (!HasComp<BladeDashArmutantComponent>(args.User))
                AddComp<BladeDashArmutantComponent>(args.User);

            if (!HasComp<BladeTalonSpawnComponent>(args.User))
                AddComp<BladeTalonSpawnComponent>(args.User);

            if (!HasComp<SporeBladeComponent>(args.User))
                AddComp<SporeBladeComponent>(args.User);
        }

        if (TryComp<MetaDataComponent>(args.Equipped, out var shieldMetaData) && shieldMetaData.EntityPrototype?.ID == "ShieldArmutant")
        {
            if (!HasComp<ShieldCreateArmorComponent>(args.User))
                AddComp<ShieldCreateArmorComponent>(args.User);

            if (!HasComp<ShieldStunComponent>(args.User))
                AddComp<ShieldStunComponent>(args.User);

            if (!HasComp<VoidShieldComponent>(args.User))
                AddComp<VoidShieldComponent>(args.User);
        }

        if (TryComp<MetaDataComponent>(args.Equipped, out var fistMetaData) && fistMetaData.EntityPrototype?.ID == "FistArmutant")
        {
            if (!HasComp<FistStunAroundComponent>(args.User))
                AddComp<FistStunAroundComponent>(args.User);

            if (!HasComp<FistMendSelfComponent>(args.User))
                AddComp<FistMendSelfComponent>(args.User);

            if (!HasComp<FistBuffSpeedComponent>(args.User))
                AddComp<FistBuffSpeedComponent>(args.User);
        }

        if (TryComp<MetaDataComponent>(args.Equipped, out var gunMetaData) && gunMetaData.EntityPrototype?.ID == "GunArmutant")
        {
            if (!HasComp<GunZoomComponent>(args.User))
                AddComp<GunZoomComponent>(args.User);

            if (!HasComp<GunSmokeComponent>(args.User))
                AddComp<GunSmokeComponent>(args.User);
        }
    }
    private void OnUnequip(EntityUid uid, ArmutantGiveWeaponsComponent component, DidUnequipHandEvent args)
    {
        if (TryComp<MetaDataComponent>(args.Unequipped, out var clawsMetaData) && clawsMetaData.EntityPrototype?.ID == "BladeArmutant")
        {
            if (HasComp<BladeDashArmutantComponent>(args.User))
                RemComp<BladeDashArmutantComponent>(args.User);

            if (HasComp<BladeTalonSpawnComponent>(args.User))
                RemComp<BladeTalonSpawnComponent>(args.User);

            if (HasComp<SporeBladeComponent>(args.User))
                RemComp<SporeBladeComponent>(args.User);
        }

        if (TryComp<MetaDataComponent>(args.Unequipped, out var shieldMetaData) && shieldMetaData.EntityPrototype?.ID == "ShieldArmutant")
        {
            if (HasComp<ShieldCreateArmorComponent>(args.User))
                RemComp<ShieldCreateArmorComponent>(args.User);

            if (HasComp<ShieldStunComponent>(args.User))
                RemComp<ShieldStunComponent>(args.User);

            if (HasComp<VoidShieldComponent>(args.User))
                RemComp<VoidShieldComponent>(args.User);
        }

        if (TryComp<MetaDataComponent>(args.Unequipped, out var fistMetaData) && fistMetaData.EntityPrototype?.ID == "FistArmutant")
        {
            if (HasComp<FistStunAroundComponent>(args.User))
                RemComp<FistStunAroundComponent>(args.User);

            if (HasComp<FistMendSelfComponent>(args.User))
                RemComp<FistMendSelfComponent>(args.User);

            if (HasComp<FistBuffSpeedComponent>(args.User))
                RemComp<FistBuffSpeedComponent>(args.User);
        }

        if (TryComp<MetaDataComponent>(args.Unequipped, out var gunMetaData) && gunMetaData.EntityPrototype?.ID == "GunArmutant")
        {
            if (HasComp<GunZoomComponent>(args.User))
                RemComp<GunZoomComponent>(args.User);

            if (HasComp<GunSmokeComponent>(args.User))
                RemComp<GunSmokeComponent>(args.User);
        }
    }
}






