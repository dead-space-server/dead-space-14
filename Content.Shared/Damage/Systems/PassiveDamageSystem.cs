using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Shared.Damage.Systems;

public sealed partial class PassiveDamageSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

<<<<<<< ours
    #region Subscriptions

    // DS14-start: current engine baseline uses explicit subscriptions and DamageChangedEvent.
    public override void Initialize()
    {
        base.Initialize();
||||||| base
    public override void Initialize()
    {
        base.Initialize();
=======
    #region Subscriptions
>>>>>>> theirs

<<<<<<< ours
        SubscribeLocalEvent<PassiveDamageComponent, MapInitEvent>(OnPendingMapInit);
        SubscribeLocalEvent<PassiveDamageComponent, DamageChangedEvent>(OnDamageTaken);
    }

    private void OnPendingMapInit(Entity<PassiveDamageComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(1f);
        Dirty(ent);
||||||| base
        SubscribeLocalEvent<PassiveDamageComponent, MapInitEvent>(OnPendingMapInit);
=======
    [SubscribeLocalEvent]
    private void OnPendingMapInit(Entity<PassiveDamageComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(1f);
        Dirty(ent);
>>>>>>> theirs
    }

<<<<<<< ours
    private void OnDamageTaken(Entity<PassiveDamageComponent> ent, ref DamageChangedEvent args)
||||||| base
    private void OnPendingMapInit(EntityUid uid, PassiveDamageComponent component, MapInitEvent args)
=======
    [SubscribeLocalEvent]
    private void OnDamageTaken(Entity<PassiveDamageComponent> ent, ref DamageDealtEvent args)
>>>>>>> theirs
    {
<<<<<<< ours
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
||||||| base
        component.NextDamage = _timing.CurTime + TimeSpan.FromSeconds(1f);
=======
        if (ent.Comp.IntervalHaltOnDamageTaken == TimeSpan.Zero || !args.Damage.AnyPositive())
            return;

        var proposedUpdateTime = _timing.CurTime + ent.Comp.IntervalHaltOnDamageTaken;
        if (proposedUpdateTime > ent.Comp.NextDamage)
        {
            ent.Comp.NextDamage = proposedUpdateTime;
            Dirty(ent);
        }

>>>>>>> theirs
    }
    // DS14-end

    #endregion

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
