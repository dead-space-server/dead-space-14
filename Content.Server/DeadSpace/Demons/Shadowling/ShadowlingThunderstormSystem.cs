// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stunnable;
using Content.Server.Beam;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Demons.Shadowling
{
    public sealed partial class ShadowlingThunderstormSystem : EntitySystem
    {
        [Dependency] private SharedActionsSystem _actions = default!;
        [Dependency] private MobStateSystem _mobState = default!;
        [Dependency] private EntityLookupSystem _lookup = default!;
        [Dependency] private BeamSystem _beam = default!;
        [Dependency] private SharedStunSystem _stun = default!;
        [Dependency] private SharedTransformSystem _transform = default!;
        [Dependency] private DamageableSystem _damageable = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShadowlingThunderstormComponent, ComponentInit>(OnComponentInit);
            SubscribeLocalEvent<ShadowlingThunderstormComponent, ShadowlingThunderstormEvent>(OnThunderstormAction);
        }

        private void OnComponentInit(EntityUid uid, ShadowlingThunderstormComponent component, ComponentInit args)
        {
            _actions.AddAction(uid, ref component.ActionThunderstormEntity, component.ActionThunderstorm);
        }

        private void OnThunderstormAction(EntityUid uid, ShadowlingThunderstormComponent component, ShadowlingThunderstormEvent args)
        {
            if (args.Handled)
                return;

            var target = args.Target;

            if (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target) || HasComp<ShadowlingComponent>(target) || HasComp<ShadowlingSlaveComponent>(target))
                return;

            args.Handled = true;

            var struck = new List<EntityUid> { uid };
            var source = uid;
            var current = target;
            var chains = 0;

            StrikeChain(uid, component, struck, source, current, chains);
        }

        private void StrikeChain(EntityUid uid, ShadowlingThunderstormComponent component, List<EntityUid> struck, EntityUid source, EntityUid current, int chains)
        {
            if (chains >= component.MaxTargets)
                return;

            _beam.TryCreateBeam(source, current, component.LightningPrototype);

            _stun.TryUpdateParalyzeDuration(current, TimeSpan.FromSeconds(component.StunDuration));

            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Shock", 25);
            _damageable.TryChangeDamage(current, damage, true);

            struck.Add(current);

            var xform = Transform(current);
            var mapPos = _transform.GetMapCoordinates(current, xform);

            var nearby = _lookup.GetEntitiesInRange<MobStateComponent>(mapPos, component.Range);

            EntityUid? next = null;
            var dist = float.MaxValue;

            foreach (var (ent, _) in nearby)
            {
                if (struck.Contains(ent) || _mobState.IsDead(ent) || HasComp<ShadowlingComponent>(ent) || HasComp<ShadowlingSlaveComponent>(ent))
                    continue;

                var curDist = (_transform.GetWorldPosition(ent) - _transform.GetWorldPosition(current)).LengthSquared();
                if (dist > curDist)
                {
                    dist = curDist;
                    next = ent;
                }
            }

            if (next == null)
                return;

            var nextChain = chains + 1;
            Timer.Spawn(TimeSpan.FromSeconds(component.ChainDelay), () =>
            {
                StrikeChain(uid, component, struck, current, next.Value, nextChain);
            });
        }
    }
}