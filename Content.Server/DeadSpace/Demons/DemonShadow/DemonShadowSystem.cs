// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Beam;
using Content.Server.GameTicking;
using Content.Server.Light.Components;
using Content.Server.StationEvents.Events;
using Content.Server.DeadSpace.Abilities.Cocoon;
using Content.Server.DeadSpace.Demons.DemonShadow.Components;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.DeadSpace.Abilities.Cocoon;
using Content.Shared.DeadSpace.Demons.DemonShadow;
using Content.Shared.DeadSpace.Demons.DemonShadow.Components;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.IgnitionSource;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Light;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Content.Server.PDA;
using Content.Shared.PDA;
using Content.Shared.Gibbing;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;

namespace Content.Server.DeadSpace.Demons.DemonShadow;

public sealed class DemonShadowSystem : SharedDemonShadowSystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedHandheldLightSystem _handheldLight = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedIgnitionSourceSystem _ignitionSource = default!;

    private const float ConeHalfAngle = 60f * MathF.PI / 180f;
    private const float LightExtinguishRadius = 7f;
    private static readonly ProtoId<TagPrototype> FlareTag = "Flare";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DemonShadowComponent, ShadowCrawlActionEvent>(DoShadowCrawl);
        SubscribeLocalEvent<DemonShadowComponent, ShadowGrappleEvent>(DoShadowGrapple);
        SubscribeLocalEvent<DemonShadowComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DemonShadowComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<DemonShadowComponent, ComponentShutdown>(OnComponentShutdown);
        SubscribeLocalEvent<DemonShadowComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<DemonShadowComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<DemonShadowComponent, LockCocoonEvent>(OnLockCocoon, before: new[] { typeof(LockCocoonSystem) });

        SubscribeLocalEvent<RoundEndTextAppendEvent>(_ => MakeVisible(true));
    }

    private void OnLockCocoon(EntityUid uid, DemonShadowComponent component, LockCocoonEvent args)
    {
        if (component.IsShadowCrawl)
        {
            _popup.PopupEntity(Loc.GetString("Вы не можете применить эту способность в теневой фазе."), uid, uid);
            args.Handled = true;
            return;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var demonShadow = EntityQueryEnumerator<DemonShadowComponent>();
        while (demonShadow.MoveNext(out var uid, out var component))
        {
            if (_gameTiming.CurTime > component.TimeToCheck)
                ShadowCheck(uid, component);

            if (component.NextTickForRegen + TimeSpan.FromSeconds(1) < _gameTiming.CurTime)
            {
                Regeneration(uid, component);
                ReceiveLightDamage(uid, component);
            }

            if (component.IsShadowCrawl)
            {
                if (_physics.GetEntitiesIntersectingBody(uid, (int) CollisionGroup.Impassable).Count == 0)
                    component.PhaseLastSafeCoordinates = Transform(uid).Coordinates;

                if (_gameTiming.CurTime >= component.TimeUtilPhaseEnd)
                    EndPhase(uid, component);
            }

            if (_gameTiming.CurTime >= component.TimeUtilTeleport && component.TeleportTarget != null)
                Teleport(uid, component.TeleportTarget.Value, component);

            if (_gameTiming.CurTime >= component.TimeUtilShadowCrawl && component.IsStartShadowCrawl)
                Crawl(uid, component);
        }
    }

    private void Regeneration(EntityUid uid, DemonShadowComponent component)
    {
        component.NextTickForRegen = _gameTiming.CurTime;

        if (!TryComp<MobStateComponent>(uid, out var mobStateComponent))
            return;

        if (!TryComp<DamageableComponent>(uid, out var damageableComponent))
            return;

        if (_mobState.IsDead(uid, mobStateComponent))
            return;

        var healing = component.IsShadowPosition
            ? component.DarknessHealing
            : component.PassiveHealing;

        _damageable.TryChangeDamage(uid, healing, true, false);
    }

    private void ReceiveLightDamage(EntityUid uid, DemonShadowComponent component)
    {
        if (component.IsShadowPosition)
            return;

        if (!TryComp<MobStateComponent>(uid, out var mobStateComponent))
            return;

        if (_mobState.IsDead(uid, mobStateComponent))
            return;

        _damageable.TryChangeDamage(uid, component.LightDamage, true, false);
    }

    public void ShadowCheck(EntityUid uid, DemonShadowComponent component)
    {
        var shadowState = GetShadowPositionState(uid, component);

        if (!shadowState.IsShadowPosition || shadowState.RevealedByLight)
            _appearance.SetData(uid, DemonShadowVisuals.Hide, false);
        else if (!component.IsShadowCrawl)
            _appearance.SetData(uid, DemonShadowVisuals.Hide, true);

        component.IsShadowPosition = shadowState.IsShadowPosition;
        component.TimeToCheck = _gameTiming.CurTime + component.CheckDuration;

        var movementSpeedMultiply = shadowState.IsShadowPosition ? 2f : 1f;
        if (component.MovementSpeedMultiply != movementSpeedMultiply)
        {
            component.MovementSpeedMultiply = movementSpeedMultiply;
            Dirty(uid, component);
            _movement.RefreshMovementSpeedModifiers(uid);
        }

        return;
    }

    private void OnStartup(EntityUid uid, DemonShadowComponent component, ComponentStartup args)
    {
        _appearance.SetData(uid, DemonShadowVisuals.Astral, false);
        _appearance.SetData(uid, DemonShadowVisuals.Hide, false);
        _appearance.SetData(uid, DemonShadowVisuals.Shadow, true);
        _appearance.SetData(uid, DemonShadowVisuals.DemonShadow, false);

        if (TryComp<NpcFactionMemberComponent>(uid, out var factionComp))
            component.OldFaction = factionComp.Factions.FirstOrDefault();

        Astral(uid, false);
    }

    private void OnComponentInit(EntityUid uid, DemonShadowComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.DemonShadowCrawlActionEntity, component.DemonShadowCrawl, uid);
        _actionsSystem.AddAction(uid, ref component.DemonShadowGrappleActionEntity, component.DemonShadowGrapple, uid);
    }

    private void OnMeleeHit(EntityUid uid, DemonShadowComponent component, MeleeHitEvent args)
    {
        if (component.IsShadowCrawl)
        {
            args.Handled = true;
            return;
        }

        if (!args.IsHit)
            return;

        var stunnedTargets = new List<EntityUid>();

        foreach (var target in args.HitEntities)
        {
            if (target == uid)
                continue;

            if (!HasComp<StunnedComponent>(target))
                return;

            stunnedTargets.Add(target);
        }

        if (stunnedTargets.Count == 0)
            return;

        var damage = DamageSpecifier.ApplyModifierSets(args.BaseDamage + args.BonusDamage, args.ModifiersList);

        foreach (var target in stunnedTargets)
        {
            _damageable.TryChangeDamage(target, damage, ignoreResistances: true, origin: args.User);
        }

        args.Handled = true;
    }

    private void OnComponentShutdown(EntityUid uid, DemonShadowComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.DemonShadowCrawlActionEntity);

        component.IsShadowCrawl = false;
        component.IsStartShadowCrawl = false;
        component.PhaseLastSafeCoordinates = null;

        Astral(uid, false);

        component.MovementSpeedMultiply = 1;
        Dirty(uid, component);
        _movement.RefreshMovementSpeedModifiers(uid);
    }

    private void DoShadowCrawl(EntityUid uid, DemonShadowComponent component, ShadowCrawlActionEvent args)
    {
        if (args.Handled)
            return;

        if (component.IsShadowCrawl || component.IsStartShadowCrawl)
            return;

        if (!TryUseShadowCrawl(uid))
            return;

        args.Handled = true;

        _actionsSystem.SetEnabled(component.DemonShadowCrawlActionEntity, false);
        _appearance.SetData(uid, DemonShadowVisuals.Astral, true);
        _audio.PlayPvs("/Audio/_DeadSpace/Demons/shadow.ogg", uid, AudioParams.Default.WithVolume(1f));

        component.IsStartShadowCrawl = true;
        component.TimeUtilShadowCrawl = _gameTiming.CurTime + component.ShadowCrawlDuration;
    }

    private bool TryUseShadowCrawl(EntityUid uid)
    {
        var tileref = _turf.GetTileRef(Transform(uid).Coordinates);
        if (tileref != null)
        {
            if (_physics.GetEntitiesIntersectingBody(uid, (int) CollisionGroup.Impassable).Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("revenant-in-solid"), uid, uid);
                return false;
            }
        }

        return true;
    }

    private void Crawl(EntityUid uid, DemonShadowComponent component)
    {
        component.IsStartShadowCrawl = false;

        if (!TryUseShadowCrawl(uid))
        {
            _appearance.SetData(uid, DemonShadowVisuals.Astral, false);
            _actionsSystem.SetEnabled(component.DemonShadowCrawlActionEntity, true);
            _actionsSystem.ClearCooldown(component.DemonShadowCrawlActionEntity);
            return;
        }

        _appearance.SetData(uid, DemonShadowVisuals.Astral, false);

        component.IsShadowCrawl = true;
        component.TimeUtilPhaseEnd = _gameTiming.CurTime + component.PhaseDuration;
        component.PhaseLastSafeCoordinates = Transform(uid).Coordinates;

        Astral(uid, true);
    }

    private void EndPhase(EntityUid uid, DemonShadowComponent component)
    {
        component.IsShadowCrawl = false;
        component.TimeUtilPhaseEnd = TimeSpan.Zero;

        _actionsSystem.SetEnabled(component.DemonShadowCrawlActionEntity, true);

        var stuck = _physics.GetEntitiesIntersectingBody(uid, (int) CollisionGroup.Impassable).Count > 0;

        Astral(uid, false);

        if (stuck && component.PhaseLastSafeCoordinates is { } safeCoordinates)
        {
            _transform.SetCoordinates(uid, Transform(uid), safeCoordinates);

            if (TryComp<PhysicsComponent>(uid, out var physics))
                _physics.SetLinearVelocity(uid, Vector2.Zero, body: physics);
        }

        component.PhaseLastSafeCoordinates = null;
    }

    /// <summary>
    /// Данный метод удаляет сущность из контейнера, в который ее вставили.
    /// Необходимо для удаления возможности перемещать теневые коконы в ящиках/шкафах.
    /// </summary>
    private void OnInserted(EntityUid uid, DemonShadowComponent component, EntGotInsertedIntoContainerMessage args)
    {
        if (!HasComp<EntityStorageComponent>(args.Container.Owner))
            return;

        // Откладываем удаление на следующий тик! Иначе может возникнуть проблема с флагами системы контейнеров
        Timer.Spawn(0, () =>
        {
            _popup.PopupEntity(
                "Стены хранилища не могут удержать эту материю",
                args.Container.Owner,
                PopupType.MediumCaution
            );
            _containerSystem.Remove(uid, args.Container);
        });
    }

    private void ToggleFixtures(EntityUid uid, bool isHasFixture)
    {
        if (!isHasFixture)
        {
            if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
            {
                var fixture = fixtures.Fixtures.First();

                _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, 0, fixtures);
                _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, 0, fixtures);
            }
        }
        else
        {
            if (TryComp<FixturesComponent>(uid, out var fixtures) && fixtures.FixtureCount >= 1)
            {
                var fixture = fixtures.Fixtures.First();

                _physics.SetCollisionMask(uid, fixture.Key, fixture.Value, (int) CollisionGroup.SmallMobMask, fixtures);
                _physics.SetCollisionLayer(uid, fixture.Key, fixture.Value, (int) CollisionGroup.SmallMobLayer, fixtures);
            }
        }
    }

    private void ToggleVisible(EntityUid uid, bool visible)
    {
        if (!TryComp<VisibilityComponent>(uid, out var visibleComponent))
            return;

        if (visible)
        {
            _visibility.AddLayer((uid, visibleComponent), (int) VisibilityFlags.Normal, false);
            _visibility.RemoveLayer((uid, visibleComponent), (int) VisibilityFlags.Astral, false);
        }
        else
        {
            _visibility.AddLayer((uid, visibleComponent), (int) VisibilityFlags.Astral, false);
            _visibility.RemoveLayer((uid, visibleComponent), (int) VisibilityFlags.Normal, false);
        }

        _visibility.RefreshVisibility(uid, visibleComponent);
    }

    private void Astral(EntityUid uid, bool isAstral)
    {
        if (!TryComp<DemonShadowComponent>(uid, out var component))
            return;

        if (isAstral)
        {
            _actionsSystem.SetEnabled(component.DemonShadowGrappleActionEntity, false);
            ToggleVisible(uid, false);
            ToggleFixtures(uid, false);
            _appearance.SetData(uid, DemonShadowVisuals.Shadow, true);
            _appearance.SetData(uid, DemonShadowVisuals.DemonShadow, true);

            _faction.ClearFactions(uid, dirty: false);
        }
        else
        {
            _actionsSystem.SetEnabled(component.DemonShadowGrappleActionEntity, true);
            ToggleVisible(uid, true);
            ToggleFixtures(uid, true);
            _appearance.SetData(uid, DemonShadowVisuals.Shadow, false);
            _appearance.SetData(uid, DemonShadowVisuals.DemonShadow, false);

            if (component.OldFaction != null)
            {
                _faction.AddFaction(uid, component.OldFaction);
            }
            else
            {
                Logger.Warning($"OldFaction для сущности {uid} равен null.");
            }
        }
    }

    private void DoShadowGrapple(EntityUid uid, DemonShadowComponent component, ShadowGrappleEvent args)
    {
        if (!_gameTiming.IsFirstTimePredicted)
            return;

        if (args.Handled)
            return;

        var user = args.Performer;
        var target = args.Target;

        if (!TryComp<MobStateComponent>(target, out var stateComponent))
            return;

        if (!_interaction.InRangeUnobstructed(user, target, 0f, CollisionGroup.BulletImpassable))
        {
            _popup.PopupEntity(Loc.GetString("Не могу пройти через препятствие!"), uid, uid);
            return;
        }

        args.Handled = true;

        ExtinguishLights(user, LightExtinguishRadius);

        _beam.TryCreateBeam(uid, target, "ShadowHand");
        _stun.TryUpdateParalyzeDuration(target, TimeSpan.FromSeconds(3));

        component.TeleportTarget = target;
        component.TimeUtilTeleport = _gameTiming.CurTime + component.TeleportDuration;
    }

    private void Teleport(EntityUid uid, EntityUid target, DemonShadowComponent component)
    {
        component.TeleportTarget = null;
        _transform.SetCoordinates(target, Transform(uid).Coordinates);
        _transform.AttachToGridOrMap(target);
    }

    private void ExtinguishLights(EntityUid uid, float radius)
    {
        var position = _transform.GetMapCoordinates(uid);
        var lights = _lookup.GetEntitiesInRange(position, radius);

        foreach (var lightUid in lights)
        {
            if (lightUid == uid)
                continue;

            if (TryComp<ExpendableLightComponent>(lightUid, out var expendable))
            {
                if (expendable.Activated)
                    KillFlare(lightUid, expendable);

                continue;
            }

            if (TryComp<HandheldLightComponent>(lightUid, out var handheld))
            {
                if (handheld.Activated)
                    _handheldLight.TurnOff((lightUid, handheld));

                continue;
            }

            if (TryComp<ItemTogglePointLightComponent>(lightUid, out _) &&
                TryComp<ItemToggleComponent>(lightUid, out var itemToggle))
            {
                if (itemToggle.Activated)
                    _itemToggle.Toggle((lightUid, itemToggle));

                continue;
            }

            if (TryComp<PointLightComponent>(lightUid, out var pointLight) && pointLight.Enabled)
                _light.SetEnabled(lightUid, false, pointLight);
        }
    }

    private void KillFlare(EntityUid uid, ExpendableLightComponent expendable)
    {
        if (TryComp<PointLightComponent>(uid, out var flareLight))
            _light.SetEnabled(uid, false, flareLight);

        if (TryComp<IgnitionSourceComponent>(uid, out var ignition))
            _ignitionSource.SetIgnited((uid, ignition), false);

        expendable.CurrentState = ExpendableLightState.Dead;
        Dirty(uid, expendable);

        if (TryComp<AppearanceComponent>(uid, out var appearance))
        {
            _appearance.SetData(uid, ExpendableLightVisuals.State, ExpendableLightState.Dead, appearance);
            _appearance.SetData(uid, ExpendableLightVisuals.Behavior, string.Empty, appearance);
        }

        if (TryComp<ItemComponent>(uid, out var item))
            _item.SetHeldPrefix(uid, "unlit", false, item);
    }

    public bool IsShadowPosition(EntityUid uid, DemonShadowComponent? component = null)
    {
        return GetShadowPositionState(uid, component).IsShadowPosition;
    }

    private (bool IsShadowPosition, bool RevealedByLight) GetShadowPositionState(EntityUid uid, DemonShadowComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return (false, false);

        MapCoordinates lightPosition;
        MapCoordinates entityPosition = _transform.GetMapCoordinates(uid);
        float cocoonRange = 10f;

        var entities = _lookup.GetEntitiesInRange<ShadowCocoonComponent>(_transform.GetMapCoordinates(uid, Transform(uid)), cocoonRange);
        var hasShadowCocoon = entities.Count > 0;

        var pointLightQuery = EntityQueryEnumerator<PointLightComponent, TransformComponent>();

        while (pointLightQuery.MoveNext(out var ent, out var lightComp, out var xform))
        {
            if (Transform(uid).MapID != xform.MapID)
                continue;

            if (HasComp<GhostComponent>(ent))
                continue;

            if (HasComp<PdaComponent>(ent))
                continue;

            if (TryComp<VisibilityComponent>(ent, out var layer) && layer.Layer != (int)VisibilityFlags.Normal)
                continue;

            lightPosition = _transform.GetMapCoordinates(ent);

            if (!_examine.InRangeUnOccluded(entityPosition, lightPosition, lightComp.Radius, null) || !lightComp.Enabled)
                continue;

            if (!IsInsideLightCone(uid, ent, lightComp))
                continue;

            if (IsPortableRevealingLight(ent, lightComp))
            {
                if (hasShadowCocoon)
                    return (true, true);

                return (false, false);
            }

            if (!hasShadowCocoon)
                return (false, false);
        }

        return (true, false);
    }

    private bool IsInsideLightCone(EntityUid uid, EntityUid lightUid, PointLightComponent light)
    {
        if (light.MaskPath == null)
            return true;

        var demonPosition = _transform.GetWorldPosition(uid);
        var lightPosition = _transform.GetWorldPosition(lightUid);
        var directionToDemon = demonPosition - lightPosition;

        if (directionToDemon.LengthSquared() < 0.01f)
            return true;

        var lightRotation = _transform.GetWorldRotation(lightUid);
        var forward = lightRotation.ToWorldVec();
        var dot = Math.Clamp(Vector2.Dot(forward, directionToDemon.Normalized()), -1f, 1f);
        var angle = MathF.Acos(dot);

        return angle <= ConeHalfAngle;
    }

    private bool IsPortableRevealingLight(EntityUid uid, PointLightComponent light)
    {
        if (!light.Enabled)
            return false;

        if (TryComp<HandheldLightComponent>(uid, out var handheldLight))
            return handheldLight.Activated;

        return TryComp<ExpendableLightComponent>(uid, out var expendableLight) &&
               expendableLight.Activated &&
               _tag.HasTag(uid, FlareTag);
    }

    public void MakeVisible(bool visible)
    {
        var query = EntityQueryEnumerator<DemonShadowComponent, VisibilityComponent>();
        while (query.MoveNext(out var uid, out _, out var vis))
        {
            if (visible)
            {
                _visibility.AddLayer((uid, vis), (int)VisibilityFlags.Normal, false);
                _visibility.RemoveLayer((uid, vis), (int)VisibilityFlags.Astral, false);
            }
            else
            {
                _visibility.AddLayer((uid, vis), (int)VisibilityFlags.Astral, false);
                _visibility.RemoveLayer((uid, vis), (int)VisibilityFlags.Normal, false);
            }

            _visibility.RefreshVisibility(uid, vis);
        }
    }
}
