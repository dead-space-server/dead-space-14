// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Abilities.StunRadius;
using Content.Shared.DeadSpace.Abilities.StunRadius.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Interaction;
using Robust.Shared.Physics;
using Content.Shared.Physics;
using System.Numerics;
using Content.Shared.Atmos.Components;
using Content.Shared.Ghost;
using Robust.Shared.Map.Components;
using Content.Server.Singularity.Components;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Prototypes;
using Content.Shared.StatusEffect;

namespace Content.Server.DeadSpace.Abilities.StunRadius;

public sealed partial class StunRadiusSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    private static readonly ProtoId<StatusEffectPrototype> StunEffect = "Stun";
    private const float MinGravPulseRange = 0.00001f;
    private const float MinRange = 0.01f;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunRadiusComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<StunRadiusComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StunRadiusComponent, StunRadiusActionEvent>(DoStunRadius);
    }

    public override void Update(float frameTime)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        var curTime = _gameTiming.CurTime;
        var query = EntityQueryEnumerator<StunRadiusComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.TimeUntilEndAnimation <= curTime && component.IsRunning)
            {
                component.IsRunning = false;
                UpdateState(uid, component);
            }
        }
    }

    private void OnComponentInit(EntityUid uid, StunRadiusComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionStunRadiusEntity, component.ActionStunRadius, uid);
    }

    private void OnShutdown(EntityUid uid, StunRadiusComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionStunRadiusEntity);
    }

    private void DoStunRadius(EntityUid uid, StunRadiusComponent component, StunRadiusActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var entities = _lookup.GetEntitiesInRange(uid, component.RangeStun);

        if (!string.IsNullOrEmpty(component.EffectPrototype))
        {

            var forcePowerEnt = Spawn(component.EffectPrototype, Transform(uid).Coordinates);

            if (TryComp<TimedDespawnComponent>(forcePowerEnt, out var timedDespawnComp))
            {
                TimeSpan durationEffect = TimeSpan.FromSeconds(timedDespawnComp.Lifetime);
                _statusEffect.TryAddStatusEffect<StunnedComponent>(uid, StunEffect, durationEffect, true);
            }
        }

        if (!string.IsNullOrEmpty(component.StunState) && !string.IsNullOrEmpty(component.State))
        {
            component.TimeUntilEndAnimation = _gameTiming.CurTime + TimeSpan.FromSeconds(component.DurationAnimation);
            component.IsRunning = true;
            UpdateState(uid, component);
        }

        Push(uid, component, Transform(uid));

        if (component.StunRadiusSound != null)
            _audio.PlayPvs(component.StunRadiusSound, uid, AudioParams.Default.WithVolume(3).WithMaxDistance(component.RangeStun * 2));
    }

    public void UpdateState(EntityUid uid, StunRadiusComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.IsRunning)
        {
            _appearance.SetData(uid, StunRadiusVisuals.State, false);
            _appearance.SetData(uid, StunRadiusVisuals.StunRadius, true);
        }
        else
        {
            _appearance.SetData(uid, StunRadiusVisuals.State, true);
            _appearance.SetData(uid, StunRadiusVisuals.StunRadius, false);
        }
    }

    private void Push(EntityUid uid, StunRadiusComponent component, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return;

        var entityPos = xform.Coordinates;
        var minRange2 = MathF.Max(MinRange * MinRange, MinGravPulseRange);
        var mapPos = _transform.ToMapCoordinates(entityPos);
        var epicenter = mapPos.Position;
        var maxRange = component.RangeStun;

        var baseMatrixDeltaV = new Matrix3x2(component.BaseRadialAcceleration, -component.BaseTangentialAcceleration, component.BaseTangentialAcceleration, component.BaseRadialAcceleration, 0.0f, 0.0f);

        var bodyQuery = GetEntityQuery<PhysicsComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();

        foreach (var entity in _lookup.GetEntitiesInRange(mapPos.MapId, epicenter, maxRange, flags: LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (entity == uid)
                continue;

            if (component.IgnorAlien && _npcFaction.IsEntityFriendly(uid, entity))
                continue;

            if (HasComp<BorgChassisComponent>(entity) && !component.StunBorg)
                continue;

            if (!_interaction.InRangeUnobstructed(uid, entity, 0f, CollisionGroup.Opaque))
                continue;

            if (!bodyQuery.TryGetComponent(entity, out var physics)
                || physics.BodyType == BodyType.Static)
            {
                continue;
            }

            if (TryComp<MovedByPressureComponent>(entity, out var movedPressure) && !movedPressure.Enabled)
                continue;

            if (!CanGravPulseAffect(entity))
                continue;

            _stun.TryUpdateParalyzeDuration(entity, TimeSpan.FromSeconds(component.ParalyzeTime));

            var displacement = epicenter - _transform.GetWorldPosition(entity, xformQuery);
            var distance2 = displacement.LengthSquared();
            if (distance2 < minRange2)
                continue;

            var scaling = component.Strenght * physics.Mass;
            _physics.ApplyLinearImpulse(entity, Vector2.TransformNormal(displacement, baseMatrixDeltaV) * scaling, body: physics);
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
