// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Movement.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Necromorphs.CorpseCollector.Components;
using Robust.Shared.Timing;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;

namespace Content.Shared.DeadSpace.Necromorphs.CorpseCollector;

public abstract partial class SharedCorpseCollectorSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CorpseCollectorComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(EntityUid uid, CorpseCollectorComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(component.MovementSpeedMultiplier, component.MovementSpeedMultiplier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var demonShadow = EntityQueryEnumerator<CorpseCollectorComponent>();
        while (demonShadow.MoveNext(out var uid, out var component))
        {

            if (component.NextTickForRegen + TimeSpan.FromSeconds(1) < _gameTiming.CurTime)
            {
                Regeneration(uid, component);
            }
        }
    }
    private void Regeneration(EntityUid uid, CorpseCollectorComponent component)
    {
        component.NextTickForRegen = _gameTiming.CurTime;

        if (!TryComp<MobStateComponent>(uid, out var mobStateComponent))
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageableComponent))
            return;

        if (_mobState.IsDead(uid, mobStateComponent))
            return;

        _damageable.TryChangeDamage(uid, component.PassiveHealing * component.PassiveHealingMultiplier, ignoreResistances: true, interruptsDoAfters: false);
    }
}
