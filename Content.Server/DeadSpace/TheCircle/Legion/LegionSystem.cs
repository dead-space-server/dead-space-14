// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DeadSpace.TheCircle.Legion;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Climbing.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Placeable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Server.DeadSpace.TheCircle.Legion;

public sealed class LegionSystem : EntitySystem
{
    private static readonly EntProtoId DoorSlow = "TaserSlowdownStatusEffect";
    private static readonly HashSet<ProtoId<DamageContainerPrototype>> SyntheticDamageContainers =
        ["Silicon", "Inorganic", "StructuralInorganic"];
    private static readonly DamageSpecifier SecondHitDamage = new() { DamageDict = { ["Slash"] = 2 } };

    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly MovementModStatusSystem _movementStatus = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, (TimeSpan EndsAt, EntityUid Origin)> _damageOverTime = new();
    private readonly Dictionary<EntityUid, TimeSpan> _revealUntil = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LegionComponent, LegionRageActionEvent>(OnRage);
        SubscribeLocalEvent<LegionComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<LegionComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<LegionComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<LegionComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<LegionKnifeComponent, MeleeHitEvent>(OnKnifeHit);
        SubscribeLocalEvent<LegionKnifeComponent, LegionKnifeRageAttemptEvent>(OnKnifeRageAttempt);
        SubscribeLocalEvent<LegionSurvivalPerkComponent, UpdateMobStateEvent>(OnSurvivalMobState,
            after: [typeof(MobThresholdSystem)]);
    }

    private void OnRage(Entity<LegionComponent> ent, ref LegionRageActionEvent args)
    {
        if (args.Handled || ent.Comp.Active || _timing.CurTime < ent.Comp.CooldownEndsAt)
            return;

        args.Handled = true;
        ent.Comp.Active = true;
        ent.Comp.EndsAt = _timing.CurTime + ent.Comp.Duration;
        ent.Comp.NextReveal = TimeSpan.MaxValue;
        ent.Comp.Hits.Clear();
        ent.Comp.RevealStarted = false;
        ent.Comp.RevealPulseActive = false;
        if (TryComp<LegionSurvivalPerkComponent>(ent, out var survival))
        {
            survival.HitCount = 0;
            survival.Activated = false;
        }
        // Heartbeat is intentionally non-positional: the source asset is stereo and only the Legionnaire hears it.
        ent.Comp.HeartbeatStream = _audio.PlayGlobal(
            ent.Comp.HeartbeatSound,
            ent.Owner,
            ent.Comp.HeartbeatSound.Params.WithLoop(true))?.Entity;
        _alerts.ShowAlert(ent.Owner, ent.Comp.RageAlert,
            cooldown: (_timing.CurTime, ent.Comp.EndsAt), autoRemove: false);
        Dirty(ent, ent.Comp);
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void OnKnifeRageAttempt(Entity<LegionKnifeComponent> knife, ref LegionKnifeRageAttemptEvent args)
    {
        if (!TryComp<LegionComponent>(args.User, out var legion) || legion.Active ||
            _timing.CurTime < legion.CooldownEndsAt)
            return;

        var action = new LegionRageActionEvent();
        OnRage((args.User, legion), ref action);
    }

    private void OnRefreshSpeed(Entity<LegionComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.Active)
            return;

        var modifier = ent.Comp.SpeedModifier;
        if (TryComp<LegionPredatorPerkComponent>(ent, out var predator) && predator.Activated)
            modifier += predator.SpeedBonus;
        args.ModifySpeed(modifier);
    }

    private void OnCollide(Entity<LegionComponent> ent, ref StartCollideEvent args)
    {
        if (!ent.Comp.Active || !TryComp<DoorComponent>(args.OtherEntity, out var door))
            return;

        if (door.State == DoorState.Closing)
            _movementStatus.TryUpdateMovementSpeedModDuration(ent, DoorSlow, ent.Comp.DoorSlowDuration, ent.Comp.DoorSlowModifier);

        _doors.StartOpening(args.OtherEntity, door);
    }

    private void OnPreventCollide(Entity<LegionComponent> ent, ref PreventCollideEvent args)
    {
        var isTable = HasComp<PlaceableSurfaceComponent>(args.OtherEntity);
        if (isTable && ent.Comp.Active)
            ent.Comp.IgnoredTables.Add(args.OtherEntity);

        if (!ent.Comp.Active && (!isTable || !ent.Comp.IgnoredTables.Contains(args.OtherEntity)))
            return;

        if (HasComp<Content.Shared.Buckle.Components.StrapComponent>(args.OtherEntity) ||
            HasComp<ClimbableComponent>(args.OtherEntity) || isTable)
            args.Cancelled = true;
    }

    private void OnEndCollide(Entity<LegionComponent> ent, ref EndCollideEvent args)
    {
        ent.Comp.IgnoredTables.Remove(args.OtherEntity);
    }

    private void OnKnifeHit(Entity<LegionKnifeComponent> knife, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0 || !TryComp<LegionComponent>(args.User, out var legion))
            return;

        var victimsHit = 0;
        var organicVictimsHit = 0;
        foreach (var target in args.HitEntities)
        {
            if (target == args.User || !HasComp<DamageableComponent>(target))
                continue;

            if (!IsValidRageVictim(args.User, target))
                continue;

            victimsHit++;
            if (!IsSynthetic(target))
                organicVictimsHit++;
            if (!legion.Active)
                continue;

            CountPerkHit(args.User);
            var count = legion.Hits.GetValueOrDefault(target) + 1;
            legion.Hits[target] = count;
            if (count == 1 && TryComp<BloodstreamComponent>(target, out var blood))
            {
                _bloodstream.TryModifyBleedAmount(target, blood.MaxBleedAmount);
                legion.NextReveal = _timing.CurTime;
                legion.RevealStarted = true;
            }
            else if (count == 2)
            {
                _damageOverTime[target] = (_timing.CurTime + knife.Comp.SecondHitDamageDuration, args.User);
            }
            else if (count >= 3)
                EndRage((args.User, legion));

            if (legion.Active)
            {
                legion.EndsAt = _timing.CurTime + legion.Duration;
                _alerts.ShowAlert(args.User, legion.RageAlert,
                    cooldown: (_timing.CurTime, legion.EndsAt), autoRemove: false);
            }
            Dirty(args.User, legion);
        }

        if (organicVictimsHit > 0)
        {
            var vampirism = knife.Comp.Vampirism;
            if (TryComp<LegionPredatorPerkComponent>(args.User, out var predator) && predator.Activated)
                vampirism += predator.VampirismBonus;

            _damage.HealDistributed(args.User, FixedPoint2.New(-25f * vampirism * organicVictimsHit), origin: args.User);

            if (TryComp<BloodstreamComponent>(args.User, out var bloodstream))
            {
                _bloodstream.TryModifyBloodLevel(args.User, knife.Comp.BloodRestore * organicVictimsHit);
                _bloodstream.TryModifyBleedAmount((args.User, bloodstream), -bloodstream.BleedAmount);
            }
        }
    }

    private bool IsSynthetic(EntityUid target)
    {
        return TryComp<DamageableComponent>(target, out var damageable) &&
               damageable.DamageContainerID is { } container &&
               SyntheticDamageContainers.Contains(container);
    }

    private bool IsValidRageVictim(EntityUid user, EntityUid target)
    {
        if (!TryComp<MindContainerComponent>(target, out var mind) || !mind.HasMind ||
            !TryComp<MobStateComponent>(target, out var mob) || mob.CurrentState != MobState.Alive)
            return false;

        return !_factions.IsEntityFriendly(user, target);
    }

    private void CountPerkHit(EntityUid user)
    {
        if (TryComp<LegionSurvivalPerkComponent>(user, out var survival) && !survival.Activated)
        {
            survival.HitCount++;
            if (survival.HitCount >= survival.RequiredHits)
            {
                survival.Activated = true;
                _mobState.UpdateMobState(user);
            }
        }

        if (!TryComp<LegionPredatorPerkComponent>(user, out var predator) || predator.Activated)
            return;

        predator.HitCount++;
        if (predator.HitCount < predator.RequiredHits)
            return;

        predator.Activated = true;
        _movement.RefreshMovementSpeedModifiers(user);
    }

    private void OnSurvivalMobState(Entity<LegionSurvivalPerkComponent> ent, ref UpdateMobStateEvent args)
    {
        if (ent.Comp.Activated &&
            TryComp<DamageableComponent>(ent, out var damageable) &&
            damageable.TotalDamage.Float() < ent.Comp.EndDamage)
            args.State = MobState.Alive;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LegionComponent>();
        while (query.MoveNext(out var uid, out var legion))
        {
            if (!legion.Active)
            {
                if (legion.CooldownEndsAt != default && now >= legion.CooldownEndsAt)
                {
                    legion.CooldownEndsAt = default;
                    _alerts.ClearAlert(uid, legion.RageAlert);
                    Dirty(uid, legion);
                }

                continue;
            }

            if (now >= legion.EndsAt)
            {
                EndRage((uid, legion));
                continue;
            }

            if (legion.RevealStarted && now >= legion.NextReveal)
            {
                legion.RevealPulseActive = true;
                _revealUntil[uid] = now + legion.RevealDuration;
                legion.NextReveal = now + legion.RevealInterval;
                Dirty(uid, legion);
            }
        }

        foreach (var (target, effect) in _damageOverTime.ToArray())
        {
            if (now >= effect.EndsAt || !Exists(target))
            {
                _damageOverTime.Remove(target);
                continue;
            }
            _damage.TryChangeDamage(target, SecondHitDamage * frameTime, ignoreResistances: true, interruptsDoAfters: false, origin: effect.Origin);
        }

        foreach (var (uid, until) in _revealUntil.ToArray())
        {
            if (now < until)
                continue;
            _revealUntil.Remove(uid);
            if (TryComp<LegionComponent>(uid, out var legion))
            {
                legion.RevealPulseActive = false;
                Dirty(uid, legion);
            }
        }
    }

    private void EndRage(Entity<LegionComponent> ent)
    {
        ent.Comp.Active = false;
        ent.Comp.RevealStarted = false;
        ent.Comp.RevealPulseActive = false;
        ent.Comp.CooldownEndsAt = _timing.CurTime + ent.Comp.Cooldown;
        ent.Comp.HeartbeatStream = _audio.Stop(ent.Comp.HeartbeatStream);
        DisableReveal(ent);
        if (TryComp<LegionSurvivalPerkComponent>(ent, out var survival))
        {
            survival.HitCount = 0;
            survival.Activated = false;
            _mobState.UpdateMobState(ent);
        }
        _alerts.ShowAlert(ent.Owner, ent.Comp.RageAlert,
            cooldown: (_timing.CurTime, ent.Comp.CooldownEndsAt), autoRemove: false);
        Dirty(ent, ent.Comp);
        _movement.RefreshMovementSpeedModifiers(ent);
    }

    private void DisableReveal(EntityUid uid)
    {
        _revealUntil.Remove(uid);
        if (!TryComp<LegionComponent>(uid, out var legion))
            return;

        legion.RevealPulseActive = false;
        Dirty(uid, legion);
    }
}
