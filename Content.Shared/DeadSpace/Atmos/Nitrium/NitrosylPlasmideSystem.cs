// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Bed.Sleep;
using Content.Shared.Prototypes;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Atmos.Nitrium;

public sealed class NitrosylPlasmideSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<NitrosylPlasmideComponent, BeforeStatusEffectAddedEvent>(OnBeforeStatusEffect);
        SubscribeLocalEvent<NitrosylPlasmideComponent, KnockDownAttemptEvent>(OnKnockDownAttempt);
        SubscribeLocalEvent<NitrosylPlasmideStatusEffectComponent, StatusEffectAppliedEvent>(OnStatusApplied);
        SubscribeLocalEvent<NitrosylPlasmideStatusEffectComponent, StatusEffectRemovedEvent>(OnStatusRemoved);
    }

    private void OnStatusApplied(Entity<NitrosylPlasmideStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        EnsureComp<NitrosylPlasmideComponent>(args.Target);
    }

    private void OnStatusRemoved(Entity<NitrosylPlasmideStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RemComp<NitrosylPlasmideComponent>(args.Target);
    }

    private void OnBeforeStatusEffect(Entity<NitrosylPlasmideComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (IsBlockedEffect(args.Effect))
            args.Cancelled = true;
    }

    private void OnKnockDownAttempt(Entity<NitrosylPlasmideComponent> ent, ref KnockDownAttemptEvent args)
    {
        args.Cancelled = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NitrosylPlasmideComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            RemoveEffects<StunnedStatusEffectComponent>(uid);
            RemoveEffects<KnockdownStatusEffectComponent>(uid);

            if (RemoveEffects<ForcedSleepingStatusEffectComponent>(uid))
                _sleeping.TryWaking(uid, force: true);
        }
    }

    private bool RemoveEffects<T>(EntityUid uid) where T : IComponent
    {
        if (!_statusEffects.TryEffectsWithComp<T>(uid, out var effects))
            return false;

        var removed = false;
        foreach (var effect in effects)
        {
            var proto = MetaData(effect.Owner).EntityPrototype;
            if (proto is not null && _statusEffects.TryRemoveStatusEffect(uid, proto.ID))
                removed = true;
        }

        return removed;
    }

    private bool IsBlockedEffect(EntProtoId effect)
    {
        return _prototype.TryIndex<EntityPrototype>(effect, out var prototype) &&
               (prototype.HasComponent<StunnedStatusEffectComponent>(_factory) ||
                prototype.HasComponent<KnockdownStatusEffectComponent>(_factory) ||
                prototype.HasComponent<ForcedSleepingStatusEffectComponent>(_factory));
    }
}
