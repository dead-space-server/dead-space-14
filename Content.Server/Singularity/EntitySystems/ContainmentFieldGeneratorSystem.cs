using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.ParticleAccelerator.Components;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Singularity.Events;
using Content.Shared.Construction.Components;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Singularity;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Singularity.Components;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Singularity.EntitySystems;

public sealed class ContainmentFieldGeneratorSystem : EntitySystem
{
    // DS14-start
    private static readonly EntProtoId AntiParticlesProjectile = "AntiParticlesProjectile";
    private static readonly SoundSpecifier DestabilizationAnnouncementSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");
    private static readonly TimeSpan DestabilizationAnnouncementCooldown = TimeSpan.FromMinutes(1);
    private const string DestabilizationAnnouncement = "comp-containment-destabilization-announcement";
    private const string DestabilizationAnnouncementSender = "comp-containment-destabilization-announcement-sender";
    private const string DestabilizationAnnouncementVoice = "Glados";
    private const int StabilizationHitsRequired = 2;
    private const float KickStabilizationChance = 0.2f;
    private TimeSpan _nextDestabilizationAnnouncementTime;
    // DS14-end

    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly AppearanceSystem _visualizer = default!;
    [Dependency] private readonly ChatSystem _chat = default!; // DS14
    [Dependency] private readonly ExplosionSystem _explosion = default!; // DS14
    [Dependency] private readonly NavMapSystem _navMap = default!; // DS14
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly IGameTiming _timing = default!; // DS14
    [Dependency] private readonly IRobustRandom _random = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, StartCollideEvent>(HandleGeneratorCollide);
        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanRaycastFiredEvent>(HandleGeneratorHitscan);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, ReAnchorEvent>(OnReanchorEvent);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, ComponentRemove>(OnComponentRemoved);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, EventHorizonAttemptConsumeEntityEvent>(PreventBreach);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, GotEmaggedEvent>(OnEmagged); // DS14
        SubscribeLocalEvent<ContainmentFieldGeneratorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs); // DS14
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ContainmentFieldGeneratorComponent>();
        while (query.MoveNext(out var uid, out var generator))
        {
            // DS14-start
            if (generator.HackEndTime is { } hackEndTime && _timing.CurTime >= hackEndTime)
            {
                generator.HackEndTime = null;
                RemoveConnections((uid, generator));
                _explosion.TriggerExplosive(uid);
                continue;
            }
            // DS14-end

            if (generator.PowerBuffer <= 0) //don't drain power if there's no power, or if it's somehow less than 0.
                continue;

            generator.Accumulator += frameTime;

            if (generator.Accumulator >= generator.Threshold)
            {
                LosePower((uid, generator), generator.PowerLoss);
                generator.Accumulator -= generator.Threshold;
            }
        }
    }

    #region Events

    private void OnMapInit(Entity<ContainmentFieldGeneratorComponent> generator, ref MapInitEvent args)
    {
        if (generator.Comp.Enabled)
            ChangeFieldVisualizer(generator);
    }

    // DS14-start
    private void OnEmagged(Entity<ContainmentFieldGeneratorComponent> generator, ref GotEmaggedEvent args)
    {
        if (args.Handled ||
            args.SourceUid is not { } source ||
            !TryComp<ContainmentFieldHackComponent>(source, out var hack) ||
            generator.Comp.HackEndTime != null)
        {
            return;
        }

        if (!generator.Comp.Enabled ||
            !generator.Comp.IsConnected ||
            generator.Comp.Connections.Count == 0 ||
            generator.Comp.PowerBuffer < generator.Comp.PowerMinimum)
        {
            _popupSystem.PopupEntity(
                Loc.GetString("comp-containment-hack-inactive"),
                generator,
                args.UserUid,
                PopupType.SmallCaution);
            return;
        }

        var durationSeconds = Math.Max(hack.DestabilizationDuration, 0f);
        var endTime = _timing.CurTime + TimeSpan.FromSeconds(durationSeconds);
        var generatorPosition = _transformSystem.GetWorldPosition(generator);
        generator.Comp.HackDurationSeconds = durationSeconds;
        generator.Comp.HackEndTime = endTime;
        generator.Comp.StabilizationEndTime = null;
        generator.Comp.StabilizationHits = 0;
        Dirty(generator);

        foreach (var (_, (otherGenerator, fields)) in generator.Comp.Connections)
        {
            var connectionDistance = (_transformSystem.GetWorldPosition(otherGenerator) - generatorPosition).Length();
            foreach (var field in fields)
            {
                if (!TryComp<ContainmentFieldComponent>(field, out var fieldComp) ||
                    (fieldComp.HackEndTime is { } fieldEndTime && fieldEndTime <= endTime))
                {
                    continue;
                }

                var fieldDistance = (_transformSystem.GetWorldPosition(field) - generatorPosition).Length();
                fieldComp.HackDurationSeconds = durationSeconds;
                fieldComp.HackEndTime = endTime;
                fieldComp.StabilizationEndTime = null;
                fieldComp.HackIntensity = Math.Clamp(
                    1f - (fieldDistance - 1f) / Math.Max(connectionDistance - 1f, 1f),
                    0f,
                    1f);
                Dirty(field, fieldComp);
            }
        }

        AnnounceDestabilization(generator, durationSeconds);
        args.Handled = true;
    }

    private void AnnounceDestabilization(EntityUid generator, float durationSeconds)
    {
        if (_timing.CurTime < _nextDestabilizationAnnouncementTime)
            return;

        var xform = Transform(generator);
        if (xform.MapID == MapId.Nullspace)
            return;

        var location = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString((generator, xform)));
        var announcement = Loc.GetString(
            DestabilizationAnnouncement,
            ("location", location),
            ("duration", durationSeconds));
        _nextDestabilizationAnnouncementTime = _timing.CurTime + DestabilizationAnnouncementCooldown;

        _chat.DispatchAdminFilteredAnnouncement(
            Filter.Empty().AddInMap(xform.MapID, EntityManager),
            announcement,
            sender: Loc.GetString(DestabilizationAnnouncementSender),
            announcementSound: DestabilizationAnnouncementSound,
            originalMessage: announcement,
            voice: DestabilizationAnnouncementVoice);
    }

    private void OnGetAlternativeVerbs(
        Entity<ContainmentFieldGeneratorComponent> generator,
        ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || generator.Comp.HackEndTime == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("comp-containment-kick-verb"),
            Act = () =>
            {
                if (generator.Comp.HackEndTime == null)
                    return;

                if (!_random.Prob(KickStabilizationChance))
                {
                    _popupSystem.PopupEntity(
                        Loc.GetString("comp-containment-kick-failure"),
                        generator,
                        user,
                        PopupType.SmallCaution);
                    return;
                }

                Stabilize(generator);
                _popupSystem.PopupEntity(
                    Loc.GetString("comp-containment-kick-success"),
                    generator,
                    user,
                    PopupType.Medium);
            },
        });
    }

    private void Stabilize(Entity<ContainmentFieldGeneratorComponent> generator)
    {
        if (generator.Comp.HackEndTime is not { } hackEndTime)
            return;

        var now = _timing.CurTime;
        var duration = Math.Clamp(
            generator.Comp.HackDurationSeconds - (float) (hackEndTime - now).TotalSeconds,
            0f,
            generator.Comp.HackDurationSeconds);
        var endTime = now + TimeSpan.FromSeconds(duration);
        generator.Comp.HackEndTime = null;
        generator.Comp.StabilizationEndTime = endTime;
        generator.Comp.StabilizationHits = 0;
        Dirty(generator);

        foreach (var (_, (_, fields)) in generator.Comp.Connections)
        {
            foreach (var field in fields)
            {
                if (!TryComp<ContainmentFieldComponent>(field, out var fieldComp) || fieldComp.HackEndTime == null)
                    continue;

                fieldComp.HackEndTime = null;
                fieldComp.StabilizationEndTime = endTime;
                Dirty(field, fieldComp);
            }
        }
    }
    // DS14-end

    /// <summary>
    /// A generator receives power from a source colliding with it.
    /// </summary>
    private void HandleGeneratorCollide(Entity<ContainmentFieldGeneratorComponent> generator, ref StartCollideEvent args)
    {
        // DS14-start
        if (generator.Comp.HackEndTime != null &&
            MetaData(args.OtherEntity).EntityPrototype?.ID == AntiParticlesProjectile.Id &&
            HasComp<ParticleProjectileComponent>(args.OtherEntity))
        {
            QueueDel(args.OtherEntity);
            if (++generator.Comp.StabilizationHits < StabilizationHitsRequired)
            {
                _popupSystem.PopupEntity(
                    Loc.GetString("comp-containment-stabilization-hit"),
                    generator,
                    PopupType.Medium);
            }
            else
            {
                Stabilize(generator);
                _popupSystem.PopupEntity(
                    Loc.GetString("comp-containment-stabilized"),
                    generator,
                    PopupType.Large);
            }
            return;
        }
        // DS14-end

        if (args.OtherFixtureId == generator.Comp.SourceFixtureId &&
            _tags.HasTag(args.OtherEntity, generator.Comp.IDTag))
        {
            ReceivePower(generator.Comp.PowerReceived, generator);
            generator.Comp.Accumulator = 0f;
        }
    }

    // DS14-start: emitter bolts use hitscan and no longer raise physics collision events.
    private void HandleGeneratorHitscan(Entity<HitscanBasicRaycastComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity is not { } hit ||
            !TryComp<ContainmentFieldGeneratorComponent>(hit, out var generator) ||
            !_tags.HasTag(ent.Owner, generator.IDTag))
        {
            return;
        }

        ReceivePower(generator.PowerReceived, (hit, generator));
        generator.Accumulator = 0f;
    }
    // DS14-end

    private void OnExamine(EntityUid uid, ContainmentFieldGeneratorComponent component, ExaminedEvent args)
    {
        if (component.Enabled)
            args.PushMarkup(Loc.GetString("comp-containment-on"));

        else
            args.PushMarkup(Loc.GetString("comp-containment-off"));
    }

    private void OnActivate(Entity<ContainmentFieldGeneratorComponent> generator, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp(generator, out TransformComponent? transformComp) && transformComp.Anchored)
        {
            if (!generator.Comp.Enabled)
                TurnOn(generator);
            else if (generator.Comp.Enabled && generator.Comp.IsConnected)
            {
                _popupSystem.PopupEntity(Loc.GetString("comp-containment-toggle-warning"), args.User, args.User, PopupType.LargeCaution);
                return;
            }
            else
                TurnOff(generator);
        }
        args.Handled = true;
    }

    private void OnAnchorChanged(Entity<ContainmentFieldGeneratorComponent> generator, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
            RemoveConnections(generator);
    }

    private void OnReanchorEvent(Entity<ContainmentFieldGeneratorComponent> generator, ref ReAnchorEvent args)
    {
        GridCheck(generator);
    }

    private void OnUnanchorAttempt(EntityUid uid, ContainmentFieldGeneratorComponent component,
        UnanchorAttemptEvent args)
    {
        if (component.Enabled || component.IsConnected)
        {
            _popupSystem.PopupEntity(Loc.GetString("comp-containment-anchor-warning"), args.User, args.User, PopupType.LargeCaution);
            args.Cancel();
        }
    }

    private void TurnOn(Entity<ContainmentFieldGeneratorComponent> generator)
    {
        generator.Comp.Enabled = true;
        ChangeFieldVisualizer(generator);
        _popupSystem.PopupEntity(Loc.GetString("comp-containment-turned-on"), generator);
    }

    private void TurnOff(Entity<ContainmentFieldGeneratorComponent> generator)
    {
        generator.Comp.Enabled = false;
        ChangeFieldVisualizer(generator);
        _popupSystem.PopupEntity(Loc.GetString("comp-containment-turned-off"), generator);
    }

    private void OnComponentRemoved(Entity<ContainmentFieldGeneratorComponent> generator, ref ComponentRemove args)
    {
        RemoveConnections(generator);
    }

    /// <summary>
    /// Deletes the fields and removes the respective connections for the generators.
    /// If a predicate is provided, only remove fields and connections if the predicate returns true.
    /// </summary>
    /// <param name="generator">The field generator component</param>
    /// <param name="removePredicate">An optional predicate that takes in this generator entity and the other generator entity.
    /// It should return true if the connection should be removed, and false otherwise.
    /// If a predicate isn't provided, all connections will be removed.</param>
    private void RemoveConnections(
        Entity<ContainmentFieldGeneratorComponent> generator,
        Func<Entity<ContainmentFieldGeneratorComponent>, Entity<ContainmentFieldGeneratorComponent>, bool>? removePredicate = null)
    {
        var (uid, component) = generator;
        var anyFieldsRemoved = false;

        foreach (var (direction, (otherGen, fields)) in component.Connections.ToArray()) // DS14
        {
            if (removePredicate is not null && !removePredicate(generator, otherGen))
            {
                // Do not delete a connection only if the provided predicate says not to.
                continue;
            }

            anyFieldsRemoved = true;

            foreach (var field in fields)
            {
                QueueDel(field);
            }

            component.Connections.Remove(direction);

            otherGen.Comp.Connections.Remove(direction.GetOpposite());

            if (otherGen.Comp.Connections.Count == 0) //Change isconnected only if there's no more connections
            {
                otherGen.Comp.IsConnected = false;
                ChangeOnLightVisualizer(otherGen);
            }

            ChangeFieldVisualizer(otherGen);
        }

        if (!anyFieldsRemoved)
        {
            // No fields were removed, so no logging or other updates are necessary.
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("comp-containment-disconnected"), uid, PopupType.LargeCaution);

        if (component.Connections.Count == 0)
        {
            component.IsConnected = false;
            ChangeOnLightVisualizer(generator);
        }
        ChangeFieldVisualizer(generator);

        _adminLogger.Add(LogType.FieldGeneration, LogImpact.Medium, $"{ToPrettyString(uid)} lost field connections"); // Ideally LogImpact would depend on if there is a singulo nearby
    }

    #endregion

    #region Connections

    /// <summary>
    /// Stores power in the generator. If it hits the threshold, it tries to establish a connection.
    /// </summary>
    /// <param name="power">The power that this generator received from the collision in <see cref="HandleGeneratorCollide"/></param>
    public void ReceivePower(int power, Entity<ContainmentFieldGeneratorComponent> generator)
    {
        var component = generator.Comp;
        component.PowerBuffer += power;

        var genXForm = Transform(generator);

        if (component.PowerBuffer >= component.PowerMinimum)
        {
            var directions = Enum.GetValues<Direction>().Length;
            for (int i = 0; i < directions - 1; i += 2)
            {
                var dir = (Direction)i;

                if (component.Connections.ContainsKey(dir))
                    continue; // This direction already has an active connection

                TryGenerateFieldConnection(dir, generator, genXForm);
            }
        }

        ChangePowerVisualizer(power, generator);
    }

    public void LosePower(Entity<ContainmentFieldGeneratorComponent> generator, int power)
    {
        var component = generator.Comp;
        component.PowerBuffer -= power;

        if (component.PowerBuffer < component.PowerMinimum && component.Connections.Count != 0)
        {
            // Only remove connections if the generators on BOTH sides of the field don't have enough power.
            // Since we only run this code if we know this gen doesn't have enough power, we only have to check the other gen.
            RemoveConnections(generator, (_, otherGen) => otherGen.Comp.PowerBuffer < otherGen.Comp.PowerMinimum);
        }

        ChangePowerVisualizer(power, generator);
    }

    /// <summary>
    /// This will attempt to establish a connection of fields between two generators.
    /// If all the checks pass and fields spawn, it will store this connection on each respective generator.
    /// </summary>
    /// <param name="dir">The field generator establishes a connection in this direction.</param>
    /// <param name="generator">The field generator component</param>
    /// <param name="gen1XForm">The transform component for the first generator</param>
    /// <returns></returns>
    private bool TryGenerateFieldConnection(Direction dir, Entity<ContainmentFieldGeneratorComponent> generator, TransformComponent gen1XForm)
    {
        var component = generator.Comp;
        if (!component.Enabled)
            return false;

        if (!gen1XForm.Anchored)
            return false;

        var genWorldPosRot = _transformSystem.GetWorldPositionRotation(gen1XForm);
        var dirRad = dir.ToAngle() + genWorldPosRot.WorldRotation; //needs to be like this for the raycast to work properly

        var ray = new CollisionRay(genWorldPosRot.WorldPosition, dirRad.ToVec(), component.CollisionMask);
        var rayCastResults = _physics.IntersectRay(gen1XForm.MapID, ray, component.MaxLength, generator, false);
        var genQuery = GetEntityQuery<ContainmentFieldGeneratorComponent>();

        RayCastResults? closestResult = null;

        foreach (var result in rayCastResults)
        {
            if (genQuery.HasComponent(result.HitEntity))
                closestResult = result;

            break;
        }
        if (closestResult == null)
            return false;

        var ent = closestResult.Value.HitEntity;

        if (!TryComp<ContainmentFieldGeneratorComponent>(ent, out var otherFieldGeneratorComponent) ||
            otherFieldGeneratorComponent == component ||
            !TryComp<PhysicsComponent>(ent, out var collidableComponent) ||
            collidableComponent.BodyType != BodyType.Static ||
            gen1XForm.ParentUid != Transform(ent).ParentUid)
        {
            return false;
        }

        var otherFieldGenerator = (ent, otherFieldGeneratorComponent);
        var fields = GenerateFieldConnection(generator, otherFieldGenerator);

        component.Connections[dir] = (otherFieldGenerator, fields);
        otherFieldGeneratorComponent.Connections[dir.GetOpposite()] = (generator, fields);
        ChangeFieldVisualizer(otherFieldGenerator);

        if (!component.IsConnected)
        {
            component.IsConnected = true;
            ChangeOnLightVisualizer(generator);
        }

        if (!otherFieldGeneratorComponent.IsConnected)
        {
            otherFieldGeneratorComponent.IsConnected = true;
            ChangeOnLightVisualizer(otherFieldGenerator);
        }

        ChangeFieldVisualizer(generator);
        UpdateConnectionLights(generator);
        _popupSystem.PopupEntity(Loc.GetString("comp-containment-connected"), generator);
        return true;
    }

    /// <summary>
    /// Spawns fields between two generators if the <see cref="TryGenerateFieldConnection"/> finds two generators to connect.
    /// </summary>
    /// <param name="firstGen">The source field generator</param>
    /// <param name="secondGen">The second generator that the source is connected to</param>
    /// <returns></returns>
    private List<EntityUid> GenerateFieldConnection(Entity<ContainmentFieldGeneratorComponent> firstGen, Entity<ContainmentFieldGeneratorComponent> secondGen)
    {
        var fieldList = new List<EntityUid>();
        var gen1Coords = Transform(firstGen).Coordinates;
        var gen2Coords = Transform(secondGen).Coordinates;

        var delta = (gen2Coords - gen1Coords).Position;
        var dirVec = delta.Normalized();
        var stopDist = delta.Length();
        var currentOffset = dirVec;
        while (currentOffset.Length() < stopDist)
        {
            var currentCoords = gen1Coords.Offset(currentOffset);
            var newField = Spawn(firstGen.Comp.CreatedField, currentCoords);

            var fieldXForm = Transform(newField);
            _transformSystem.SetParent(newField, fieldXForm, firstGen);
            if (dirVec.GetDir() == Direction.East || dirVec.GetDir() == Direction.West)
            {
                var angle = fieldXForm.LocalPosition.ToAngle();
                var rotateBy90 = angle.Degrees + 90;
                var rotatedAngle = Angle.FromDegrees(rotateBy90);

                fieldXForm.LocalRotation = rotatedAngle;
            }

            fieldList.Add(newField);
            currentOffset += dirVec;
        }
        return fieldList;
    }

    /// <summary>
    /// Creates a light component for the spawned fields.
    /// </summary>
    public void UpdateConnectionLights(Entity<ContainmentFieldGeneratorComponent> generator)
    {
        if (_light.TryGetLight(generator, out var pointLightComponent))
        {
            _light.SetEnabled(generator, generator.Comp.Connections.Count > 0, pointLightComponent);
        }
    }

    /// <summary>
    /// Checks to see if this or the other gens connected to a new grid. If they did, remove connection.
    /// </summary>
    public void GridCheck(Entity<ContainmentFieldGeneratorComponent> generator)
    {
        var xFormQuery = GetEntityQuery<TransformComponent>();

        foreach (var (_, generators) in generator.Comp.Connections)
        {
            var gen1ParentGrid = xFormQuery.GetComponent(generator).ParentUid;
            var gent2ParentGrid = xFormQuery.GetComponent(generators.Item1).ParentUid;

            if (gen1ParentGrid != gent2ParentGrid)
                RemoveConnections(generator);
        }
    }

    #endregion

    #region VisualizerHelpers
    /// <summary>
    /// Check if a fields power falls between certain ranges to update the field gen visual for power.
    /// </summary>
    /// <param name="power"></param>
    /// <param name="generator"></param>
    private void ChangePowerVisualizer(int power, Entity<ContainmentFieldGeneratorComponent> generator)
    {
        var component = generator.Comp;
        _visualizer.SetData(generator, ContainmentFieldGeneratorVisuals.PowerLight, component.PowerBuffer switch
        {
            <= 0 => PowerLevelVisuals.NoPower,
            >= 25 => PowerLevelVisuals.HighPower,
            _ => (component.PowerBuffer < component.PowerMinimum)
                ? PowerLevelVisuals.LowPower
                : PowerLevelVisuals.MediumPower
        });
    }

    /// <summary>
    /// Check if a field has any or no connections and if it's enabled to toggle the field level light
    /// </summary>
    /// <param name="generator"></param>
    private void ChangeFieldVisualizer(Entity<ContainmentFieldGeneratorComponent> generator)
    {
        _visualizer.SetData(generator, ContainmentFieldGeneratorVisuals.FieldLight, generator.Comp.Connections.Count switch
        {
            > 1 => FieldLevelVisuals.MultipleFields,
            1 => FieldLevelVisuals.OneField,
            _ => generator.Comp.Enabled ? FieldLevelVisuals.On : FieldLevelVisuals.NoLevel
        });
    }

    private void ChangeOnLightVisualizer(Entity<ContainmentFieldGeneratorComponent> generator)
    {
        _visualizer.SetData(generator, ContainmentFieldGeneratorVisuals.OnLight, generator.Comp.IsConnected);
        UpdateConnectionLights(generator);
    }
    #endregion

    /// <summary>
    /// Prevents singularities from breaching containment if the containment field generator is connected.
    /// </summary>
    /// <param name="uid">The entity the singularity is trying to eat.</param>
    /// <param name="comp">The containment field generator the singularity is trying to eat.</param>
    /// <param name="args">The event arguments.</param>
    private void PreventBreach(EntityUid uid, ContainmentFieldGeneratorComponent comp, ref EventHorizonAttemptConsumeEntityEvent args)
    {
        if (args.Cancelled)
            return;
        if (comp.IsConnected && !args.EventHorizon.CanBreachContainment)
            args.Cancelled = true;
    }
}
