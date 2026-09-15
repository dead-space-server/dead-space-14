using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Damage.Systems;

public sealed class PassiveDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    #region Subscriptions

    // DS14-start: current engine baseline uses explicit subscriptions and DamageChangedEvent.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PassiveDamageComponent, MapInitEvent>(OnPendingMapInit);
        SubscribeLocalEvent<PassiveDamageComponent, DamageChangedEvent>(OnDamageTaken);
    }

    private void OnPendingMapInit(Entity<PassiveDamageComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(1f);
        Dirty(ent);
    }

    private void OnDamageTaken(Entity<PassiveDamageComponent> ent, ref DamageChangedEvent args)
    {
        if (_timing.ApplyingState ||
            ent.Comp.IntervalHaltOnDamageTaken == TimeSpan.Zero ||
            !args.DamageIncreased)
            return;

        var proposedUpdateTime = _timing.CurTime + ent.Comp.IntervalHaltOnDamageTaken;
        if (proposedUpdateTime > ent.Comp.NextDamage)
        {
            ent.Comp.NextDamage = proposedUpdateTime;
            Dirty(ent);
        }
    }
    // DS14-end

    #endregion

    // Every tick, attempt to damage entities
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var curTime = _timing.CurTime;

        // Go through every entity with the component
        var query = EntityQueryEnumerator<PassiveDamageComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damage, out var mobState))
        {
            // Make sure they're up for a damage tick
            if (comp.NextDamage > curTime)
                continue;

            if (comp.DamageCap != 0 && damage.TotalDamage >= comp.DamageCap)
                continue;

            // Set the next time they can take damage
            comp.NextDamage = curTime + TimeSpan.FromSeconds(1f);
            Dirty(uid, comp);

            // Damage them
            foreach (var allowedState in comp.AllowedStates)
            {
                if(allowedState == mobState.CurrentState)
                    _damageable.ChangeDamage((uid, damage), comp.Damage, true, false);
            }
        }
    }
}
