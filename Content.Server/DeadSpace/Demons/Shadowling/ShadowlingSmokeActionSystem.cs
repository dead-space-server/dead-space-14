// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingSmokeActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private float _smokeTickAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingSmokeActionComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<ShadowlingSmokeActionComponent, ShadowlingSmokeActionEvent>(OnSmokeAction);
    }

    private void OnComponentInit(EntityUid uid, ShadowlingSmokeActionComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.ActionSmokeEntity, component.ActionSmoke);
    }

    private void OnSmokeAction(EntityUid uid, ShadowlingSmokeActionComponent component, ShadowlingSmokeActionEvent args)
    {
        if (args.Handled) return;

        var xform = Transform(uid);

        if (xform.GridUid == null)
            return;

        var smoke = Spawn("ShadowSmoke", xform.Coordinates);

        if (TryComp<SmokeComponent>(smoke, out var smokeComp))
        {
            _smoke.StartSmoke(smoke, new Solution(), component.SmokeDuration, component.SmokeSpread, smokeComp);
            args.Handled = true;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _smokeTickAccumulator += frameTime;
        if (_smokeTickAccumulator < 1f)
            return;
        _smokeTickAccumulator -= 1f;

        var processed = new HashSet<EntityUid>();
        var smokeQuery = EntityQueryEnumerator<SmokeComponent>();

        while (smokeQuery.MoveNext(out var smokeUid, out var smokeComp))
        {
            if (MetaData(smokeUid).EntityPrototype?.ID != "ShadowSmoke")
                continue;

            var smokePos = Transform(smokeUid).MapPosition;
            var entities = _lookup.GetEntitiesInRange<MobStateComponent>(smokePos, 0.5f);

            foreach (var (entity, _) in entities)
            {
                if (!processed.Add(entity))
                    continue;

                if (HasComp<ShadowlingComponent>(entity) ||
                    HasComp<ShadowlingRevealComponent>(entity) ||
                    HasComp<ShadowlingSlaveComponent>(entity))
                {
                    var healing = new DamageSpecifier();
                    healing.DamageDict.Add("Slash", -1);
                    healing.DamageDict.Add("Heat", -2);
                    healing.DamageDict.Add("Blunt", -1);
                    healing.DamageDict.Add("Piercing", -1);
                    _damageable.TryChangeDamage(entity, healing, true);
                    continue;
                }

                var damage = new DamageSpecifier();
                damage.DamageDict.Add("Slash", 2f);
                _damageable.TryChangeDamage(entity, damage, true);
            }
        }
    }
}