using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private DamageExamineSystem _damageExamine = default!;

    // needed for server system
    protected virtual void InitializeCartridge()
    {
        SubscribeLocalEvent<CartridgeAmmoComponent, ExaminedEvent>(OnCartridgeExamine);
        SubscribeLocalEvent<CartridgeAmmoComponent, DamageExamineEvent>(OnCartridgeDamageExamine);
    }

    private void OnCartridgeExamine(Entity<CartridgeAmmoComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Spent
            ? Loc.GetString("gun-cartridge-spent")
            : Loc.GetString("gun-cartridge-unspent"));
    }

    private void OnCartridgeDamageExamine(Entity<CartridgeAmmoComponent> ent, ref DamageExamineEvent args)
    {
        if (!ProtoMan.TryIndex(ent.Comp.Prototype, out var entityProto))
            return;

        var damageSpec = GetProjectileDamage(entityProto);
        var stamina = GetProjectileStamina(entityProto);

        if (damageSpec == null)
            return;

        _damageExamine.AddDamageExamine(args.Message, Damageable.ApplyUniversalAllModifiers(damageSpec), Loc.GetString("damage-projectile"), stamina);
    }

    private DamageSpecifier? GetProjectileDamage(EntityPrototype proto)
    {
<<<<<<< ours
        if (!ProtoManager.TryIndex(proto, out var entityProto))
            return null;

        if (entityProto.TryGetComponent<ProjectileComponent>(out var projectile, Factory) &&
            !projectile.Damage.Empty)
||||||| base
        if (!ProtoManager.TryIndex(proto, out var entityProto))
            return null;

        if (!entityProto.TryGetComponent<ProjectileComponent>(out var projectile, Factory))
            return null;

        if (!projectile.Damage.Empty)
=======
        if (!proto.TryComp<ProjectileComponent>(out var projectile, Factory))
            return null;

        if (!projectile.Damage.Empty)
>>>>>>> theirs
            return projectile.Damage * Damageable.UniversalProjectileDamageModifier;

        // DS14-start: cartridges can now point at hitscan ballistic traces.
        if (entityProto.TryGetComponent<HitscanBasicDamageComponent>(out var hitscan, Factory) &&
            !hitscan.Damage.Empty)
            return hitscan.Damage * Damageable.UniversalHitscanDamageModifier;
        // DS14-end

        return null;
    }

    private float? GetProjectileStamina(EntityPrototype proto)
    {
        if (proto.TryComp<StaminaDamageOnCollideComponent>(out var comp, Factory)
            && comp.Damage != 0)
            return comp.Damage;

        return null;
    }
}
