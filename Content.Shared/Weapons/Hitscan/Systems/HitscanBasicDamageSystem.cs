using Content.Shared.Administration.Logs; // DS14
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Body.Limbs; // DS14
using Content.Shared.Database; // DS14
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Whitelist;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed partial class HitscanBasicDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanBasicDamageComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(Entity<HitscanBasicDamageComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        if (!_whitelist.CheckBoth(args.Data.HitEntity, ent.Comp.Blacklist, ent.Comp.Whitelist))
        {
            LogHit(args.Data, null); // DS14
            return;
        }

        var dmg = ent.Comp.Damage * _damage.UniversalHitscanDamageModifier;

        // DS14-start
        var locate = new LimbArmorZoneEvent(args.Data);
        RaiseLocalEvent(args.Data.HitEntity.Value, ref locate);
        // DS14-end

        if (!_damage.TryChangeDamage(args.Data.HitEntity.Value, dmg, out var damageDealt, ent.Comp.IgnoreResistances, origin: args.Data.Shooter ?? args.Data.Gun)) // DS14
        {
            // DS14-start
            if (TryComp<LimbBodyComponent>(args.Data.HitEntity.Value, out var missed))
                missed.ArmorZone = null;
            LogHit(args.Data, null);
            // DS14-end
            return;
        }

        LogHit(args.Data, damageDealt); // DS14

        var damageEvent = new HitscanDamageDealtEvent
        {
            Target = args.Data.HitEntity.Value,
            DamageDealt = damageDealt,
            Data = args.Data,
        };

        RaiseLocalEvent(ent, ref damageEvent);
    }

    // DS14-start
    private void LogHit(HitscanRaycastFiredData data, DamageSpecifier? dealt)
    {
        if (data.HitEntity == null)
            return;

        var actor = data.Shooter ?? data.Gun;
        if (dealt == null)
        {
            _adminLogger.Add(LogType.HitScanHit,
                $"{ToPrettyString(actor):user} hit {ToPrettyString(data.HitEntity.Value):target} using {ToPrettyString(data.Gun):entity}.");
            return;
        }

        _adminLogger.Add(LogType.HitScanHit,
            $"{ToPrettyString(actor):user} hit {ToPrettyString(data.HitEntity.Value):target} using {ToPrettyString(data.Gun):entity} and dealt {dealt:damage} damage");
    }
    // DS14-end
}
