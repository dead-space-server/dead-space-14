// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Threading;
using Content.Server.DeadSpace.Necromorphs.Necroobelisk.Components;
using Content.Server.Emp;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DeadSpace.Camera;
using Content.Shared.DeadSpace.Necromorphs.Necroobelisk;
using Content.Shared.DeadSpace.Necromorphs.Sanity;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using Content.Shared.Silicons.Borgs.Components;

namespace Content.Server.DeadSpace.Necromorphs.Necroobelisk;

public sealed class NecroobeliskSplinterSystem : EntitySystem
{
    private static readonly ReagentId[] StimulantReagents =
    [
        new("Stimulants", null),
        new("Ephedrine", null),
        new("Desoxyephedrine", null),
        new("Depotojil", null),
        new("Celestin", null),
    ];

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;
    [Dependency] private readonly EmpSystem _epm = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly SharedSanitySystem _sharedSanity = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly MovementModStatusSystem _movement = default!;
    [Dependency] private readonly ScreenshakeSystem _screenshake = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecroobeliskSplinterComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NecroobeliskSplinterComponent, NecroSplinterAfterStoperEvent>(OnAfterStoper);
        SubscribeLocalEvent<NecroobeliskSplinterComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnMapInit(EntityUid uid, NecroobeliskSplinterComponent component, MapInitEvent args)
    {
        component.AddChargeTime = _timing.CurTime + component.TimeUtilAddCharge;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NecroobeliskSplinterComponent, LimitedChargesComponent>();
        while (query.MoveNext(out var uid, out var component, out var charges))
        {
            if (_timing.CurTime > component.AddChargeTime)
            {
                _charges.AddCharges((uid, charges), 1);
                component.AddChargeTime = _timing.CurTime + component.TimeUtilAddCharge;
            }
        }
    }

    private void OnAfterInteract(EntityUid uid, NecroobeliskSplinterComponent component, AfterInteractEvent args)
    {
        if (!args.CanReach || args.Target == null || args.Handled)
            return;

        if (HasComp<BorgChassisComponent>(args.Target.Value))
            return;

        if (TryComp<LimitedChargesComponent>(uid, out var charges))
        {
            if (_charges.IsEmpty((uid, charges)))
                return;

            _charges.TryUseCharge((uid, charges));
        }

        if (component.SoundHeadaches != null)
        {
            if (TryComp<MindContainerComponent>(args.Target.Value, out var mind)
                && TryComp<MindComponent>(mind.Mind, out var mindComp)
                && _player.TryGetSessionById(mindComp.UserId, out var session))
            {
                var playerFilter = Filter.Empty().AddPlayer(session);
                _audio.PlayGlobal(component.SoundHeadaches, playerFilter, false);
            }
        }

        var duration = IsStimulated(args.Target.Value)
            ? component.StimulatedInteractionDuration
            : component.InteractionDuration;
        ApplySplinterEffect(args.Target.Value, component, duration);
        _sharedSanity.TryAddSanityLvl(args.Target.Value, -component.SanityDamage);
    }

    private void OnAfterStoper(EntityUid uid,
        NecroobeliskSplinterComponent component,
        NecroSplinterAfterStoperEvent args)
    {
        var entities =
            _lookup.GetEntitiesInRange<SanityComponent>(_transform.GetMapCoordinates(uid, Transform(uid)),
                component.Range);

        if (component.SoundHeadaches != null)
        {
            var playerFilter = Filter.Empty().AddInRange(_transform.GetMapCoordinates(uid), component.Range);
            _audio.PlayGlobal(component.SoundHeadaches, playerFilter, false);
        }

        _epm.EmpPulse(_transform.GetMapCoordinates(uid),
            component.Range * 2,
            component.EnergyConsumption,
            TimeSpan.FromSeconds(component.Duration));

        HashSet<Entity<SanityComponent>> targets = new HashSet<Entity<SanityComponent>>();

        foreach (var entity in entities)
        {
            ApplySplinterEffect(entity.Owner, component, component.Duration);
        }

        if (entities.Count > 0)
            CauseDamageSanity(uid, targets, component);
    }

    private void ApplySplinterEffect(EntityUid target, NecroobeliskSplinterComponent component, float durationSeconds)
    {
        var duration = TimeSpan.FromSeconds(durationSeconds);
        _movement.TryUpdateMovementSpeedModDuration(
            target,
            "StatusEffectSplinterBladeSlowdown",
            duration,
            component.SpeedModifier);

        var decayRate = component.ScreenshakeTrauma / MathF.Pow(durationSeconds, 2);
        var shake = new ScreenshakeParameters
        {
            Trauma = component.ScreenshakeTrauma,
            DecayRate = decayRate,
            Frequency = component.ScreenshakeFrequency,
        };
        _screenshake.Screenshake(target, shake, shake);

        if (TryComp<ActorComponent>(target, out var actor))
            RaiseNetworkEvent(new SplinterBladeVisualEvent(durationSeconds), actor.PlayerSession);
    }

    private bool IsStimulated(EntityUid target)
    {
        if (!_solutionContainer.TryGetSolution(target, "bloodstream", out var bloodstream))
            return false;

        foreach (var reagent in StimulantReagents)
        {
            if (bloodstream.Value.Comp.Solution.ContainsReagent(reagent))
                return true;
        }

        return false;
    }

    private void CauseDamageSanity(EntityUid uid,
        HashSet<Entity<SanityComponent>> targets,
        NecroobeliskSplinterComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var impulseCount = 0;
        var time = component.SanityDamageRepeatingTime * 1000;

        var sanityDamageTokenSource = new CancellationTokenSource();

        Timer.SpawnRepeating(time,
            () =>
            {
                foreach (var (entity, sanity) in targets)
                {
                    if (!Exists(entity))
                        continue;

                    _sharedSanity.TryAddSanityLvl(entity, -component.SanityDamage, sanity);
                }

                impulseCount++;

                if (!Exists(uid))
                    sanityDamageTokenSource.Cancel();

                if (impulseCount >= component.SanityDamageImpulseCount)
                {
                    sanityDamageTokenSource.Cancel();
                    QueueDel(uid);
                }
            },
            sanityDamageTokenSource.Token);
    }
}

[ByRefEvent]
public readonly record struct NecroSplinterAfterStoperEvent;
