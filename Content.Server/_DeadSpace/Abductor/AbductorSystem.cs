// TEMP FOR EVENT DONT FCKNG TOUCH
using Content.Server.Administration.Logs;
using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.DeadSpace.GameTicking.Rules.Components;
using Content.Server.DoAfter;
using Content.Server.Implants;
using Content.Server.Mind;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.Abductor;
using Content.Shared.DeadSpace.ItemSwitch;
using Content.Shared.DeadSpace.ItemSwitch.Components;
using Content.Shared.DeadSpace.Roles;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Eye;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station.Components;
using Content.Shared.Stealth.Components;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server.DeadSpace.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly TransformSystem _xformSys = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly IGameTiming _time = default!;
    [Dependency] private readonly NumberObjectiveSystem _number = default!;
    [Dependency] private readonly SharedItemSwitchSystem _itemSwitch = default!;
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SubdermalImplantSystem _subdermalImplant = default!;

    private static readonly EntProtoId<ActionComponent> SendYourself = "ActionSendYourself";
    private static readonly EntProtoId<ActionComponent> ExitAction = "ActionExitConsole";
    private static readonly EntProtoId TeleportationEffect = "EffectTeleportation";
    private static readonly EntProtoId TeleportationEffectEntity = "EffectTeleportationEntity";

    private static readonly ProtoId<TagPrototype> Abductor = "Abductor";

    private static readonly string DefaultAbductorVictimRule = "AbductorVictim";

    public override void Initialize()
    {
        SubscribeLocalEvent<AbductorHumanObservationConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<AbductorHumanObservationConsoleComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUIOpenAttempt);
        Subs.BuiEvents<AbductorHumanObservationConsoleComponent>(AbductorCameraConsoleUIKey.Key, subs => subs.Event<AbductorBeaconChosenBuiMsg>(OnAbductorBeaconChosenBuiMsg));
        InitializeActions();
        InitializeGizmo();
        InitializeConsole();
        InitializeVest();
        InitializeVictim();
        InitializeGland();
        base.Initialize();
    }

    private void OnAbductorBeaconChosenBuiMsg(Entity<AbductorHumanObservationConsoleComponent> ent, ref AbductorBeaconChosenBuiMsg args)
    {
        OnCameraExit(args.Actor);
        if (ent.Comp.RemoteEntityProto != null)
        {
            var beacon = GetEntity(args.Beacon.NetEnt);
            var eye = SpawnAtPosition(ent.Comp.RemoteEntityProto.Value, Transform(beacon).Coordinates);
            ent.Comp.RemoteEntity = GetNetEntity(eye);

            if (TryComp<HandsComponent>(args.Actor, out var handsComponent))
            {
                foreach (var hand in _hands.EnumerateHands((args.Actor, handsComponent)))
                {
                    if (!_hands.TryGetHeldItem((args.Actor, handsComponent), hand, out var held))
                        continue;

                    if (HasComp<UnremoveableComponent>(held))
                        continue;

                    _hands.DoDrop((args.Actor, handsComponent), hand);
                }

                if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, args.Actor, out var virtItem1))
                {
                    EnsureComp<UnremoveableComponent>(virtItem1.Value);
                }

                if (_virtualItem.TrySpawnVirtualItemInHand(ent.Owner, args.Actor, out var virtItem2))
                {
                    EnsureComp<UnremoveableComponent>(virtItem2.Value);
                }
            }

            var visibility = EnsureComp<VisibilityComponent>(eye);

            Dirty(ent);

            if (TryComp(args.Actor, out EyeComponent? eyeComp))
            {
                _eye.SetVisibilityMask(args.Actor, eyeComp.VisibilityMask | (int) VisibilityFlags.Abductor, eyeComp);
                _eye.SetTarget(args.Actor, eye, eyeComp);
                _eye.SetDrawFov(args.Actor, false);
                _eye.SetRotation(args.Actor, Angle.Zero, eyeComp);

                if (!TryComp(eye, out RemoteEyeSourceContainerComponent? remoteEyeSourceContainerComponent))
                {
                    remoteEyeSourceContainerComponent = new RemoteEyeSourceContainerComponent { Actor = args.Actor };
                    AddComp(eye, remoteEyeSourceContainerComponent);
                }
                else
                    remoteEyeSourceContainerComponent.Actor = args.Actor;
                Dirty(eye, remoteEyeSourceContainerComponent);
                Dirty(args.Actor, eyeComp);
            }

            AddActions(args);

            _mover.SetRelay(args.Actor, eye);
        }
    }

    private void OnCameraExit(EntityUid actor)
    {
        if (TryComp<RelayInputMoverComponent>(actor, out var comp)
            && TryComp<AbductorScientistComponent>(actor, out var abductorComp))
        {
            var relay = comp.RelayEntity;
            RemComp<RelayInputMoverComponent>(actor);

            if (abductorComp.Console != null)
                _virtualItem.DeleteInHandsMatching(actor, abductorComp.Console.Value);

            if (TryComp(actor, out EyeComponent? eyeComp))
            {
                if (HasComp<StationAiOverlayComponent>(actor))
                    RemComp<StationAiOverlayComponent>(actor);

                _eye.SetVisibilityMask(actor, eyeComp.VisibilityMask ^ (int) VisibilityFlags.Abductor, eyeComp);
                _eye.SetDrawFov(actor, true);
                _eye.SetTarget(actor, null, eyeComp);
            }
            RemoveActions(actor);
            QueueDel(relay);
        }
    }

    private void OnBeforeActivatableUIOpen(Entity<AbductorHumanObservationConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        if (!TryComp<AbductorScientistComponent>(args.User, out var abductorComp))
            return;

        abductorComp.Console = ent.Owner;
        var stations = _stationSystem.GetStations();
        var result = new Dictionary<int, StationBeacons>();

        foreach (var station in stations)
        {
            if (_stationSystem.GetLargestGrid(station) is not { } grid
                || !TryComp(station, out MetaDataComponent? stationMetaData))
                continue;

            var mapId = Transform(grid).MapID;

            if (!TryComp<NavMapComponent>(grid, out var navMap))
                continue;

            result.Add(station.Id, new StationBeacons
            {
                Name = stationMetaData.EntityName,
                StationId = station.Id,
                Beacons = new List<SharedNavMapSystem.NavMapBeacon>(navMap.Beacons.Values),
            });
        }

        _uiSystem.SetUiState(ent.Owner, AbductorCameraConsoleUIKey.Key, new AbductorCameraConsoleBuiState { Stations = result });
    }

    private void OnActivatableUIOpenAttempt(Entity<AbductorHumanObservationConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<AbductorScientistComponent>(args.User))
            args.Cancel();
    }

    public void InitializeActions()
    {
        SubscribeLocalEvent<AbductorScientistComponent, ComponentStartup>(AbductorScientistComponentStartup);

        SubscribeLocalEvent<ExitConsoleEvent>(OnExit);

        SubscribeLocalEvent<AbductorReturnToShipEvent>(OnReturn);
        SubscribeLocalEvent<AbductorScientistComponent, AbductorReturnDoAfterEvent>(OnDoAfterAbductorReturn);

        SubscribeLocalEvent<SendYourselfEvent>(OnSendYourself);
        SubscribeLocalEvent<AbductorScientistComponent, AbductorSendYourselfDoAfterEvent>(OnDoAfterSendYourself);
    }

    private void AbductorScientistComponentStartup(Entity<AbductorScientistComponent> ent, ref ComponentStartup args)
        => ent.Comp.SpawnPosition = EnsureComp<TransformComponent>(ent).Coordinates;

    private void OnReturn(AbductorReturnToShipEvent ev)
    {
        EnsureComp<AbductorScientistComponent>(ev.Performer, out var abductorScientistComponent);
        AddTeleportationEffect(ev.Performer, 3.0f, TeleportationEffectEntity, out var effectEnt, true, true);

        if (abductorScientistComponent.SpawnPosition.HasValue)
        {
            var effect = Spawn(TeleportationEffect, abductorScientistComponent.SpawnPosition.Value);
            EnsureComp<TimedDespawnComponent>(effect, out var despawnComp);
            despawnComp.Lifetime = 3.0f;
            _audioSystem.PlayPvs("/Audio/Effects/teleport_departure.ogg", effect);
        }

        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(3), new AbductorReturnDoAfterEvent(), ev.Performer);
        _doAfter.TryStartDoAfter(doAfter);
        ev.Handled = true;
    }

    private void OnDoAfterAbductorReturn(Entity<AbductorScientistComponent> ent, ref AbductorReturnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ent }, Filter.Pvs(ent, entityManager: EntityManager));
        StopPulls(ent);
        if (ent.Comp.SpawnPosition is not null)
            _xformSys.SetCoordinates(ent, ent.Comp.SpawnPosition.Value);
        OnCameraExit(ent);
    }

    private void OnSendYourself(SendYourselfEvent ev)
    {
        AddTeleportationEffect(ev.Performer, 5.0f, TeleportationEffectEntity, out var effectEnt, true, false);
        var effect = Spawn(TeleportationEffect, ev.Target);
        EnsureComp<TimedDespawnComponent>(effect, out var _);

        var @event = new AbductorSendYourselfDoAfterEvent(GetNetCoordinates(ev.Target));
        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(5), @event, ev.Performer);
        _doAfter.TryStartDoAfter(doAfter);
        ev.Handled = true;
    }

    private void OnDoAfterSendYourself(Entity<AbductorScientistComponent> ent, ref AbductorSendYourselfDoAfterEvent args)
    {
        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ent }, Filter.Pvs(ent, entityManager: EntityManager));
        StopPulls(ent);
        _xformSys.SetCoordinates(ent, GetCoordinates(args.TargetCoordinates));
        OnCameraExit(ent);
    }

    private void OnExit(ExitConsoleEvent ev) => OnCameraExit(ev.Performer);

    private void AddActions(AbductorBeaconChosenBuiMsg args)
    {
        EnsureComp<AbductorsAbilitiesComponent>(args.Actor, out var comp);
        _actions.AddAction(args.Actor, ref comp.ExitConsole, ExitAction);
        _actions.AddAction(args.Actor, ref comp.SendYourself, SendYourself);
    }

    private void RemoveActions(EntityUid actor)
    {
        EnsureComp<AbductorsAbilitiesComponent>(actor, out var comp);
        _actions.RemoveAction(actor, comp.ExitConsole);
        _actions.RemoveAction(actor, comp.SendYourself);
    }

    private void StopPulls(EntityUid ent)
    {
        if (_pullingSystem.IsPulling(ent))
        {
            if (!TryComp<PullerComponent>(ent, out var pullerComp)
                || pullerComp.Pulling == null
                || !TryComp<PullableComponent>(pullerComp.Pulling.Value, out var pullableComp)
                || !_pullingSystem.TryStopPull(pullerComp.Pulling.Value, pullableComp)) return;
        }

        if (_pullingSystem.IsPulled(ent))
        {
            if (!TryComp<PullableComponent>(ent, out var pullableComp)
                || !_pullingSystem.TryStopPull(ent, pullableComp)) return;
        }
    }

    private void AddTeleportationEffect(EntityUid performer,
        float lifetime,
        EntProtoId effectEntity,
        out EntityUid effectEnt,
        bool applyColor = true,
        bool playAudio = true)
    {
        if (applyColor)
            _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { performer }, Filter.Pvs(performer, entityManager: EntityManager));

        EnsureComp<TransformComponent>(performer, out var xform);
        effectEnt = SpawnAttachedTo(effectEntity, xform.Coordinates);
        _xformSys.SetParent(effectEnt, performer);
        EnsureComp<TimedDespawnComponent>(effectEnt, out var despawnComp);
        despawnComp.Lifetime = lifetime;

        if (playAudio)
            _audioSystem.PlayPvs("/Audio/Effects/teleport_departure.ogg", effectEnt);
    }

    public void InitializeConsole()
    {
        SubscribeLocalEvent<AbductorConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<AbductConditionComponent, ObjectiveGetProgressEvent>(OnAbductGetProgress);

        Subs.BuiEvents<AbductorConsoleComponent>(AbductorConsoleUIKey.Key, subs => subs.Event<AbductorAttractBuiMsg>(OnAttractBuiMsg));
        Subs.BuiEvents<AbductorConsoleComponent>(AbductorConsoleUIKey.Key, subs => subs.Event<AbductorCompleteExperimentBuiMsg>(OnCompleteExperimentBuiMsg));
        Subs.BuiEvents<AbductorConsoleComponent>(AbductorConsoleUIKey.Key, subs => subs.Event<AbductorVestModeChangeBuiMsg>(OnVestModeChangeBuiMsg));
        Subs.BuiEvents<AbductorConsoleComponent>(AbductorConsoleUIKey.Key, subs => subs.Event<AbductorLockBuiMsg>(OnVestLockBuiMsg));
        SubscribeLocalEvent<AbductorComponent, AbductorAttractDoAfterEvent>(OnDoAfterAttract);
    }

    private void OnAbductGetProgress(Entity<AbductConditionComponent> ent, ref ObjectiveGetProgressEvent args)
        => args.Progress = AbductProgress(ent.Comp, _number.GetTarget(ent.Owner));

    private float AbductProgress(AbductConditionComponent comp, int target)
        => target == 0 ? 1f : MathF.Min(comp.Abducted / (float) target, 1f);

    private void OnVestModeChangeBuiMsg(Entity<AbductorConsoleComponent> ent, ref AbductorVestModeChangeBuiMsg args)
    {
        if (ent.Comp.Armor != null)
            _itemSwitch.Switch(GetEntity(ent.Comp.Armor.Value), args.Mode.ToString());
    }

    private void OnVestLockBuiMsg(Entity<AbductorConsoleComponent> ent, ref AbductorLockBuiMsg args)
    {
        if (ent.Comp.Armor != null && GetEntity(ent.Comp.Armor.Value) is EntityUid armor)
            if (TryComp<UnremoveableComponent>(armor, out var unremoveable))
                RemComp<UnremoveableComponent>(armor);
            else
                AddComp<UnremoveableComponent>(armor);
    }

    private void OnCompleteExperimentBuiMsg(Entity<AbductorConsoleComponent> ent, ref AbductorCompleteExperimentBuiMsg args)
    {
        var component = ent.Comp;
        if (component.Experimentator != null
            && GetEntity(component.Experimentator) is EntityUid experimentatorId
            && TryComp<AbductorExperimentatorComponent>(experimentatorId, out var experimentatorComp))
        {
            var container = _container.GetContainer(experimentatorId, experimentatorComp.ContainerId);
            var victim = container.ContainedEntities.FirstOrDefault(HasComp<AbductorVictimComponent>);
            if (victim != default && TryComp(victim, out AbductorVictimComponent? victimComp))
            {
                if (victimComp.Implanted
                    && TryComp<MindContainerComponent>(args.Actor, out var mindContainer)
                    && mindContainer.Mind.HasValue
                    && TryComp<MindComponent>(mindContainer.Mind.Value, out var mind)
                    && mind.Objectives.FirstOrDefault(HasComp<AbductConditionComponent>) is EntityUid objId
                    && TryComp<AbductConditionComponent>(objId, out var condition)
                    && !condition.AbductedHashs.Contains(GetNetEntity(victim)))
                {
                    condition.AbductedHashs.Add(GetNetEntity(victim));
                    condition.Abducted++;
                }
                _audioSystem.PlayPvs("/Audio/Voice/Human/wilhelm_scream.ogg", experimentatorId);

                if (victimComp.Position is not null)
                    _xformSys.SetCoordinates(victim, victimComp.Position.Value);
            }
        }
    }

    private void OnAttractBuiMsg(Entity<AbductorConsoleComponent> ent, ref AbductorAttractBuiMsg args)
    {
        if (ent.Comp.Target == null || ent.Comp.AlienPod == null) return;
        var target = GetEntity(ent.Comp.Target.Value);
        EnsureComp<TransformComponent>(target, out var xform);
        var effectEnt = SpawnAttachedTo(TeleportationEffectEntity, xform.Coordinates);
        _xformSys.SetParent(effectEnt, target);
        EnsureComp<TimedDespawnComponent>(effectEnt, out var despawnEffectEntComp);
        despawnEffectEntComp.Lifetime = 3.0f;
        _audioSystem.PlayPvs("/Audio/Effects/teleport_departure.ogg", effectEnt);

        var telepad = GetEntity(ent.Comp.AlienPod.Value);
        var telepadXform = EnsureComp<TransformComponent>(telepad);
        var effect = Spawn(TeleportationEffect, telepadXform.Coordinates);
        EnsureComp<TimedDespawnComponent>(effect, out var despawnComp);
        despawnComp.Lifetime = 3.0f;
        _audioSystem.PlayPvs("/Audio/Effects/teleport_arrival.ogg", effect);

        var @event = new AbductorAttractDoAfterEvent(GetNetCoordinates(telepadXform.Coordinates), GetNetEntity(target));
        ent.Comp.Target = null;
        var doAfter = new DoAfterArgs(EntityManager, args.Actor, TimeSpan.FromSeconds(3), @event, args.Actor)
        {
            BreakOnDamage = false,
            BreakOnDropItem = false,
            BreakOnHandChange = false,
            BreakOnMove = false,
            BreakOnWeightlessMove = false,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfterAttract(Entity<AbductorComponent> ent, ref AbductorAttractDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var victim = GetEntity(args.Victim);
        if (_pullingSystem.IsPulling(victim))
        {
            if (!TryComp<PullerComponent>(victim, out var pullerComp)
                || pullerComp.Pulling == null
                || !TryComp<PullableComponent>(pullerComp.Pulling.Value, out var pullableComp)
                || !_pullingSystem.TryStopPull(pullerComp.Pulling.Value, pullableComp)) return;
        }
        if (_pullingSystem.IsPulled(victim))
        {
            if (!TryComp<PullableComponent>(victim, out var pullableComp)
                || !_pullingSystem.TryStopPull(victim, pullableComp)) return;
        }
        _xformSys.SetCoordinates(victim, GetCoordinates(args.TargetCoordinates));
    }

    private void OnBeforeActivatableUIOpen(Entity<AbductorConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args) => UpdateGui(ent.Comp.Target, ent);

    protected override void UpdateGui(NetEntity? target, Entity<AbductorConsoleComponent> computer)
    {
        string? targetName = null;
        string? victimName = null;
        if (target.HasValue && TryComp(GetEntity(target.Value), out MetaDataComponent? metadata))
            targetName = metadata?.EntityName;

        var armorLock = false;
        var armorMode = AbductorArmorModeType.Stealth;

        if (computer.Comp.Armor != null)
        {
            if (HasComp<UnremoveableComponent>(GetEntity(computer.Comp.Armor.Value)))
                armorLock = true;
            if (TryComp<ItemSwitchComponent>(GetEntity(computer.Comp.Armor.Value), out var switchVest) && Enum.TryParse<AbductorArmorModeType>(switchVest.State, ignoreCase: true, out var State))
                armorMode = State;
        }

        if (computer.Comp.AlienPod == null)
        {
            var xform = EnsureComp<TransformComponent>(computer.Owner);
            var alienpad = _entityLookup.GetEntitiesInRange<AbductorAlienPadComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic)
                .FirstOrDefault().Owner;
            if (alienpad != default)
                computer.Comp.AlienPod = GetNetEntity(alienpad);
        }

        if (computer.Comp.Experimentator == null)
        {
            var xform = EnsureComp<TransformComponent>(computer.Owner);
            var experimentator = _entityLookup.GetEntitiesInRange<AbductorExperimentatorComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic)
                .FirstOrDefault().Owner;
            if (experimentator != default)
                computer.Comp.Experimentator = GetNetEntity(experimentator);
        }

        if (computer.Comp.Experimentator != null
            && GetEntity(computer.Comp.Experimentator) is EntityUid experimentatorId
            && TryComp<AbductorExperimentatorComponent>(experimentatorId, out var experimentatorComp))
        {
            var container = _container.GetContainer(experimentatorId, experimentatorComp.ContainerId);
            var victim = container.ContainedEntities.FirstOrDefault(e => HasComp<AbductorVictimComponent>(e));
            if (victim != default && TryComp(victim, out MetaDataComponent? victimMetadata))
                victimName = victimMetadata?.EntityName;
        }

        _uiSystem.SetUiState(computer.Owner, AbductorConsoleUIKey.Key, new AbductorConsoleBuiState()
        {
            Target = target,
            TargetName = targetName,
            VictimName = victimName,
            AlienPadFound = computer.Comp.AlienPod != default,
            ExperimentatorFound = computer.Comp.Experimentator != default,
            ArmorFound = computer.Comp.Armor != default,
            ArmorLocked = armorLock,
            CurrentArmorMode = armorMode
        });
    }

    public void InitializeGizmo()
    {
        SubscribeLocalEvent<AbductorGizmoComponent, AfterInteractEvent>(OnGizmoInteract);
        SubscribeLocalEvent<AbductorGizmoComponent, MeleeHitEvent>(OnGizmoHitInteract);

        SubscribeLocalEvent<AbductorGizmoComponent, AbductorGizmoMarkDoAfterEvent>(OnGizmoDoAfter);
    }

    private void OnGizmoHitInteract(Entity<AbductorGizmoComponent> ent, ref MeleeHitEvent args)
    {
        if (args.HitEntities.Count != 1) return;
        var target = args.HitEntities[0];
        if (!HasComp<HumanoidAppearanceComponent>(target)) return;
        GizmoUse(ent, target, args.User);
    }

    private void OnGizmoInteract(Entity<AbductorGizmoComponent> ent, ref AfterInteractEvent args)
    {
        if (!_actionBlockerSystem.CanInteract(args.User, args.Target)) return;
        if (!args.Target.HasValue) return;

        if (TryComp<AbductorConsoleComponent>(args.Target, out var console))
        {
            console.Target = ent.Comp.Target;
            _popup.PopupEntity(Loc.GetString("abductors-ui-gizmo-transferred"), args.User);
            _color.RaiseEffect(Color.FromHex("#00BA00"), new List<EntityUid>(2) { ent.Owner, args.Target.Value }, Filter.Pvs(args.User, entityManager: EntityManager));
            UpdateGui(console.Target, (args.Target.Value, console));
            return;
        }

        if (HasComp<HumanoidAppearanceComponent>(args.Target))
            GizmoUse(ent, args.Target.Value, args.User);
    }

    private void GizmoUse(Entity<AbductorGizmoComponent> ent, EntityUid target, EntityUid user)
    {
        if (HasComp<AbductorComponent>(target))
            return;

        var time = TimeSpan.FromSeconds(6);
        if (_tags.HasTag(target, Abductor))
            time = TimeSpan.FromSeconds(0.5);

        var doAfter = new DoAfterArgs(EntityManager, user, time, new AbductorGizmoMarkDoAfterEvent(), ent, target, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnGizmoDoAfter(Entity<AbductorGizmoComponent> ent, ref AbductorGizmoMarkDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is null)
            return;

        ent.Comp.Target = GetNetEntity(args.Target);
        EnsureComp<AbductorVictimComponent>(args.Target.Value, out var victimComponent);
        victimComponent.LastActivation = _time.CurTime + TimeSpan.FromMinutes(5);
        victimComponent.Position ??= EnsureComp<TransformComponent>(args.Target.Value).Coordinates;

        args.Handled = true;
    }

    public void InitializeVest()
    {
        SubscribeLocalEvent<AbductorVestComponent, AfterInteractEvent>(OnVestInteract);
        SubscribeLocalEvent<AbductorVestComponent, ItemSwitchedEvent>(OnItemSwitch);
        SubscribeLocalEvent<AbductorVestComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<AbductorVestComponent, GotEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(Entity<AbductorVestComponent> ent, ref GotEquippedEvent args)
    {
        if (args.Equipee.IsValid() && !HasComp<StealthComponent>(args.Equipee) && ent.Comp.CurrentState != AbductorArmorModeType.Combat)
        {
            AddComp<StealthComponent>(args.Equipee);
            AddComp<StealthOnMoveComponent>(args.Equipee);
        }
    }

    private void OnUnequipped(Entity<AbductorVestComponent> ent, ref GotUnequippedEvent args)
    {
        if (args.Equipee.IsValid() && HasComp<StealthComponent>(args.Equipee))
        {
            RemComp<StealthComponent>(args.Equipee);
            RemComp<StealthOnMoveComponent>(args.Equipee);
        }
    }

    private void OnItemSwitch(Entity<AbductorVestComponent> ent, ref ItemSwitchedEvent args)
    {
        if (Enum.TryParse<AbductorArmorModeType>(args.State, ignoreCase: true, out var state))
            ent.Comp.CurrentState = state;

        var user = Transform(ent.Owner).ParentUid;

        if (state == AbductorArmorModeType.Combat)
        {
            if (TryComp<ClothingComponent>(ent.Owner, out var clothingComponent))
                _clothing.SetEquippedPrefix(ent.Owner, "combat", clothingComponent);

            if (HasComp<MobStateComponent>(user) && HasComp<StealthComponent>(user))
            {
                RemComp<StealthComponent>(user);
                RemComp<StealthOnMoveComponent>(user);
            }
        }
        else
        {
            if (TryComp<ClothingComponent>(ent.Owner, out var clothingComponent))
                _clothing.SetEquippedPrefix(ent.Owner, null, clothingComponent);

            if (HasComp<MobStateComponent>(user) && !HasComp<StealthComponent>(user))
            {
                AddComp<StealthComponent>(user);
                AddComp<StealthOnMoveComponent>(user);
            }
        }
    }

    private void OnVestInteract(Entity<AbductorVestComponent> ent, ref AfterInteractEvent args)
    {
        if (!_actionBlockerSystem.CanInteract(args.User, args.Target)) return;
        if (!args.Target.HasValue) return;

        if (TryComp<AbductorConsoleComponent>(args.Target, out var console))
        {
            console.Armor = GetNetEntity(ent.Owner);
            _popup.PopupEntity(Loc.GetString("abductors-ui-vest-linked"), args.User);
            return;
        }
    }

    public void InitializeVictim()
    {
        SubscribeLocalEvent<AbductorOrganComponent, ImplantImplantedEvent>(OnImplanted);
    }

    private void OnImplanted(Entity<AbductorOrganComponent> ent, ref ImplantImplantedEvent args)
    {
        if (HasComp<AbductorComponent>(args.Implanted)
            || !EnsureComp<AbductorVictimComponent>(args.Implanted, out var victimComp)
            || victimComp.Implanted
            || !HasComp<HumanoidAppearanceComponent>(args.Implanted)
            || !_mind.TryGetMind(args.Implanted, out var mindId, out var mind)
            || !TryComp<ActorComponent>(args.Implanted, out var actor))
            return;

        if (mindId == default
            || !_role.MindHasRole<AbductorVictimRoleComponent>(mindId, out _))
        {
            _role.MindAddRole(mindId, "MindRoleAbductorVictim");
            victimComp.Implanted = true;
            _antag.ForceMakeAntag<AbductorVictimRuleComponent>(actor.PlayerSession, DefaultAbductorVictimRule);

            _adminLogManager.Add(LogType.Mind,
                LogImpact.Medium,
                $"{ToPrettyString(args.Implanted)} has been implanted with {ToPrettyString(ent.Owner)} and given an abductee objective.");
        }
    }

    /// <summary>
    /// Lets a loose dubious gland be used directly on a person, implanting it
    /// subdermally without needing a syringe in between.
    /// </summary>
    public void InitializeGland()
    {
        SubscribeLocalEvent<AbductorOrganComponent, AfterInteractEvent>(OnGlandAfterInteract);
    }

    private void OnGlandAfterInteract(Entity<AbductorOrganComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (!TryComp<SubdermalImplantComponent>(ent.Owner, out var implantComp)
            || implantComp.ImplantedEntity != null)
            return;

        if (!HasComp<MobStateComponent>(target))
            return;

        _subdermalImplant.ForceImplant(target, (ent.Owner, implantComp));
        _popup.PopupEntity(Loc.GetString("gland-implanted-popup"), target, args.User);
        args.Handled = true;
    }
}