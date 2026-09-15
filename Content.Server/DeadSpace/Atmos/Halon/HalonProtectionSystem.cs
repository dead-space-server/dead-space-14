// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.Temperature;

namespace Content.Server.DeadSpace.Atmos.Halon;

public sealed class HalonProtectionSystem : EntitySystem
{
    private const string HeatDamageType = "Heat";

    public override void Initialize()
    {
        SubscribeLocalEvent<HalonProtectionComponent, GetFireProtectionEvent>(OnGetFireProtection);
        SubscribeLocalEvent<HalonProtectionComponent, ModifyChangedTemperatureEvent>(OnModifyTemperature);
        SubscribeLocalEvent<HalonProtectionComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<HalonProtectionStatusEffectComponent, StatusEffectAppliedEvent>(OnStatusApplied);
        SubscribeLocalEvent<HalonProtectionStatusEffectComponent, StatusEffectRemovedEvent>(OnStatusRemoved);
    }

    private void OnGetFireProtection(Entity<HalonProtectionComponent> ent, ref GetFireProtectionEvent args)
    {
        args.Reduce(1f);
    }

    private void OnModifyTemperature(EntityUid uid, HalonProtectionComponent comp, ModifyChangedTemperatureEvent args)
    {
        if (args.TemperatureDelta > 0f)
            args.TemperatureDelta = 0f;
    }

    private void OnDamageModify(EntityUid uid, HalonProtectionComponent comp, DamageModifyEvent args)
    {
        if (!args.Damage.DamageDict.TryGetValue(HeatDamageType, out var heat) || heat <= FixedPoint2.Zero)
            return;

        var modified = new DamageSpecifier(args.Damage);
        modified.DamageDict[HeatDamageType] = FixedPoint2.Zero;
        args.Damage = modified;
    }

    private void OnStatusApplied(Entity<HalonProtectionStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<HalonProtectionComponent>(args.Target);
    }

    private void OnStatusRemoved(Entity<HalonProtectionStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<HalonProtectionComponent>(args.Target);
    }
}
