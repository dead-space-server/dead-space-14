// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.ReviveImplant;
using Content.Shared.Interaction.Events;
using Robust.Server.GameObjects;
using Content.Shared.DoAfter;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Content.Server.DeadSpace.Implants.Revive.Components;
using Content.Shared.Damage;
using Robust.Shared.Timing;
using Content.Shared.Mobs.Systems;
using Content.Server.Body.Systems;
using Content.Server.Body.Components;

namespace Content.Server.DeadSpace.Implants.Revive;

public sealed partial class ReviveImplantSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReviveImplantComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ReviveImplantComponent, ReviveImplantActivateEvent>(OnDoAfter);
        SubscribeLocalEvent<ReviveImplantComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnUseInHand(Entity<ReviveImplantComponent> item, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (HasComp<ReviveImplantComponent>(args.User))
            return;

        if (HasComp<MobStateComponent>(item))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, item.Comp.InjectingTime, new ReviveImplantActivateEvent(), item)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        _doAfter.TryStartDoAfter(doAfterArgs);

        args.Handled = true;
    }

    private void OnDoAfter(Entity<ReviveImplantComponent> item, ref ReviveImplantActivateEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        var userReive = EnsureComp<ReviveImplantComponent>(args.Args.User);
        userReive.HealAmount = item.Comp.HealAmount;
        userReive.PossibleRevives = item.Comp.PossibleRevives;
        userReive.ThresholdHeal = item.Comp.ThresholdHeal;
        userReive.ThresholdRevive = item.Comp.ThresholdRevive;

        TransformToItem(item);

        _audio.PlayPvs(item.Comp.ImplantedSound, args.User, AudioParams.Default.WithVolume(0.5f));
    }

    private void TransformToItem(Entity<ReviveImplantComponent> item)
    {
        var position = _transform.GetMapCoordinates(item);
        Del(item);
        Spawn(item.Comp.SpawnAfterUse, position);
    }

    private void RevivePerson(EntityUid ent, ReviveImplantComponent comp)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

        if (damageable.TotalDamage >= comp.ThresholdHeal)
            _damageable.TryChangeDamage(ent, comp.HealAmount, true, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        foreach (var comp in EntityQuery<ReviveImplantComponent>())
        {
            var ent = comp.Owner;

            if (!TryComp(ent, out MobStateComponent? mobState))
                continue;

            if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
                return;

            if (mobState.CurrentState == MobState.Alive)
                continue;

            if (curTime < comp.NextHealTime)
                continue;

            RevivePerson(ent, comp);

            if (mobState.CurrentState == MobState.Dead &&
                TryComp(ent, out DamageableComponent? damageable) &&
                damageable.TotalDamage <= comp.ThresholdRevive &&
                comp.NumberOfDeath <= comp.PossibleRevives)
            {
                _mobState.ChangeMobState(ent, MobState.Critical, null, null);

                _blood.TryModifyBleedAmount(ent, -bloodstream.BleedAmount);

                _blood.TryModifyBloodLevel(ent, bloodstream.BloodMaxVolume);

                comp.NumberOfDeath += 1;
            }

            comp.NextHealTime = curTime + comp.HealDuration;
        }
    }

    private void OnMobStateChanged(Entity<ReviveImplantComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        ent.Comp.NextHealTime = _timing.CurTime + ent.Comp.HealDuration;
    }
}
