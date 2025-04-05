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
using Content.Server.Electrocution;
using Content.Shared.Mobs.Systems;
using Content.Server.Body.Systems;

namespace Content.Server.DeadSpace.Implants.Revive;

public sealed partial class ReviveImplantSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;

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

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, item.Comp.InjectTime, new ReviveImplantActivateEvent(), item)
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

        EnsureComp<ReviveImplantComponent>(args.Args.User);
        TransformToItem(item);
        _audio.PlayPvs(item.Comp.ImplantedSound, args.User, AudioParams.Default.WithVolume(0.5f));
    }
    private void TransformToItem(Entity<ReviveImplantComponent> item)
    {
        var position = _transform.GetMapCoordinates(item);

        Del(item);

        Spawn("AutosurgeonAfter", position);
    }
    private void OnMobStateChanged(EntityUid user, ReviveImplantComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        StartHealingCycle(user, comp);
    }
    private void RevivePerson(EntityUid user, ReviveImplantComponent comp)
    {
        if (!TryComp<DamageableComponent>(user, out var damageable))
            return;

        if (damageable.TotalDamage <= comp.ThresholdHeal)
            _damageable.TryChangeDamage(user, comp.HealCount, true, false);
    }
    private void StartHealingCycle(EntityUid user, ReviveImplantComponent comp)
    {
        Timer.Spawn(TimeSpan.FromSeconds(4), () =>
        {
            if (!Exists(user))
                return;

            if (!TryComp<MobStateComponent>(user, out var mobState))
                return;

            if (mobState.CurrentState == MobState.Alive)
                return;

            RevivePerson(user, comp);

            if (mobState.CurrentState == MobState.Dead &&
                TryComp<DamageableComponent>(user, out var damageable) &&
                damageable.TotalDamage <= comp.ThresholdRevive &&
                comp.CountDeath <= 0)
            {
                _mobState.ChangeMobState(user, MobState.Critical, null, null);
                _blood.TryModifyBloodLevel(user, 1000);
                _blood.TryModifyBleedAmount(user, -1000);
                comp.CountDeath += 1;
            }

            if (mobState.CurrentState != MobState.Alive)
            {
                StartHealingCycle(user, comp);
            }
        });
    }
}
