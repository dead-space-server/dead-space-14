// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Content.Shared.Ghost;
using Robust.Shared.Map.Components;
using Content.Server.Singularity.Components;
using Content.Shared.DeadSpace.Abilities.JumpAbility.Components;
using Content.Shared.DeadSpace.Abilities.JumpAbility;
using Robust.Shared.GameStates;
using Content.Server.Gravity;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Robust.Shared.Timing;
using Content.Shared.Gravity;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Abilities.JumpAbility;

public sealed partial class JumpAbilitySystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly GravitySystem _gravity = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    private static readonly ProtoId<StatusEffectPrototype> StunEffect = "Stun";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeadSpaceJumpAbilityComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<DeadSpaceJumpAbilityComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DeadSpaceJumpAbilityComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<DeadSpaceJumpAbilityComponent, JumpToPointActionEvent>(DoJump);
    }

    public override void Update(float frameTime)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<DeadSpaceJumpAbilityComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.TimeUntilEndJump > curTime && component.TimeUntilNextJump <= curTime && component.IsJumps)
            {
                Jump(uid, component);
            }
        }
    }

    private void OnGetState(EntityUid uid, DeadSpaceJumpAbilityComponent component, ref ComponentGetState args)
    {
        args.State = new JumpAnimationComponentState();
    }
    private void OnComponentInit(EntityUid uid, DeadSpaceJumpAbilityComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionJumpEntity, component.ActionJump, uid);
    }

    private void OnShutdown(EntityUid uid, DeadSpaceJumpAbilityComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionJumpEntity);
    }

    private void DoJump(EntityUid uid, DeadSpaceJumpAbilityComponent component, JumpToPointActionEvent args)
    {
        if (args.Handled)
            return;

        component.IsJumps = true;
        component.TimeUntilEndJump = _gameTiming.CurTime + TimeSpan.FromSeconds(component.JumpDuration);
        component.Target = args.Target;

        Dirty(uid, component);

        args.Handled = true;

        TimeSpan durationEffect = TimeSpan.FromSeconds(component.JumpDuration);
        _statusEffect.TryAddStatusEffect<StunnedComponent>(uid, StunEffect, durationEffect, true);

        if (component.JumpSound != null)
            _audio.PlayPvs(component.JumpSound, uid, AudioParams.Default.WithVolume(3));
    }

    private void Jump(EntityUid uid, DeadSpaceJumpAbilityComponent component)
    {
        if (!TryComp<PhysicsComponent>(uid, out var physics)
                || physics.BodyType == BodyType.Static)
            return;

        if (!TryComp<GravityAffectedComponent>(uid, out var gravity))
            return;

        if (_gravity.IsWeightless((uid, gravity)))
            return;

        if (component.Target == null)
            return;

        if (!CanGravPulseAffect(uid))
            return;

        var fromCoordinates = Transform(uid).Coordinates;
        var toCoordinates = component.Target;

        var fromMap = fromCoordinates.ToMapPos(EntityManager, _transform);
        var toMap = toCoordinates.Value.ToMapPos(EntityManager, _transform);

        var shotDirection = (toMap - fromMap).Normalized();

        var scaling = component.Strenght * physics.Mass;
        var impulseVector = shotDirection;

        _physics.ApplyLinearImpulse(uid, impulseVector * scaling, body: physics);

        component.TimeUntilNextJump = _gameTiming.CurTime + TimeSpan.FromSeconds(component.Interval);

        var entities = _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(toCoordinates.Value), 0.5f);

        if (entities.Contains(uid))
        {
            component.TimeUntilEndJump = _gameTiming.CurTime;
            component.TimeUntilNextJump = _gameTiming.CurTime;
            component.IsJumps = false;
        }
    }

    private bool CanGravPulseAffect(EntityUid entity)
    {
        return !(
            EntityManager.HasComponent<GhostComponent>(entity) ||
            EntityManager.HasComponent<MapGridComponent>(entity) ||
            EntityManager.HasComponent<MapComponent>(entity) ||
            EntityManager.HasComponent<GravityWellComponent>(entity)
        );
    }
}
