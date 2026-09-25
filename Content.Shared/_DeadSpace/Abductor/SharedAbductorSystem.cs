// TEMP FOR EVENT DONT FCKNG TOUCH
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using System.Linq;
using static Content.Shared.Pinpointer.SharedNavMapSystem;

namespace Content.Shared.DeadSpace.Abductor;

public abstract class SharedAbductorSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _entityLookup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AbductorExperimentatorComponent, EntInsertedIntoContainerMessage>(OnInsertedContainer);
        SubscribeLocalEvent<AbductorExperimentatorComponent, EntRemovedFromContainerMessage>(OnRemovedContainer);
        base.Initialize();
    }

    private void OnRemovedContainer(Entity<AbductorExperimentatorComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        if (ent.Comp.Console == null)
        {
            var xform = EnsureComp<TransformComponent>(ent.Owner);
            var console = _entityLookup.GetEntitiesInRange<AbductorConsoleComponent>(xform.Coordinates, 5, LookupFlags.Approximate | LookupFlags.Dynamic)
                .FirstOrDefault().Owner;
            if (console != default)
                ent.Comp.Console = GetNetEntity(console);
        }

        if (ent.Comp.Console != null && GetEntity(ent.Comp.Console.Value) is var consoleid && TryComp<AbductorConsoleComponent>(consoleid, out var consoleComp))
            UpdateGui(consoleComp.Target, (consoleid, consoleComp));

        _appearance.SetData(ent, AbductorExperimentatorVisuals.Full, args.Container.ContainedEntities.Count > 0);
        Dirty(ent);
    }

    private void OnInsertedContainer(Entity<AbductorExperimentatorComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId)
            return;

        if (!Timing.IsFirstTimePredicted)
            return;

        if (ent.Comp.Console == null)
        {
            var xform = EnsureComp<TransformComponent>(ent.Owner);
            var console = _entityLookup.GetEntitiesInRange<AbductorConsoleComponent>(xform.Coordinates, 5, LookupFlags.Approximate | LookupFlags.Dynamic)
                .FirstOrDefault().Owner;
            if (console != default)
                ent.Comp.Console = GetNetEntity(console);
        }

        if (ent.Comp.Console != null && GetEntity(ent.Comp.Console.Value) is var consoleid && TryComp<AbductorConsoleComponent>(consoleid, out var consoleComp))
            UpdateGui(consoleComp.Target, (consoleid, consoleComp));

        _appearance.SetData(ent, AbductorExperimentatorVisuals.Full, args.Container.ContainedEntities.Count > 0);
        Dirty(ent);
    }

    protected virtual void UpdateGui(NetEntity? target, Entity<AbductorConsoleComponent> computer)
    {
    }
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorHumanObservationConsoleComponent : Component
{
    [DataField(readOnly: true)]
    public EntProtoId? RemoteEntityProto = "AbductorHumanObservationConsoleEye";

    [DataField, AutoNetworkedField]
    public NetEntity? RemoteEntity;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Target;

    [DataField, AutoNetworkedField]
    public NetEntity? AlienPod;

    [DataField, AutoNetworkedField]
    public NetEntity? Experimentator;

    [DataField, AutoNetworkedField]
    public NetEntity? Armor;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorAlienPadComponent : Component
{
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorExperimentatorComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Console;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string ContainerId = "storage";
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorGizmoComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? Target;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorComponent : Component
{
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbductorVictimComponent : Component
{
    [DataField("position"), AutoNetworkedField]
    public EntityCoordinates? Position;

    [DataField, AutoNetworkedField]
    public bool Implanted;

    [DataField, AutoNetworkedField]
    public TimeSpan? LastActivation;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductorOrganComponent : Component;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorScientistComponent : Component
{
    [DataField("position"), AutoNetworkedField]
    public EntityCoordinates? SpawnPosition;

    [DataField, AutoNetworkedField]
    public EntityUid? Console;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class RemoteEyeSourceContainerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Actor;
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorsAbilitiesComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? ExitConsole;

    [DataField, AutoNetworkedField]
    public EntityUid? SendYourself;

    [DataField]
    public EntityUid[] HiddenActions = [];
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedAbductorSystem)), AutoGenerateComponentState]
public sealed partial class AbductorVestComponent : Component
{
    [DataField, AutoNetworkedField]
    public AbductorArmorModeType CurrentState = AbductorArmorModeType.Stealth;
}

[RegisterComponent, Access(typeof(SharedAbductorSystem))]
public sealed partial class AbductConditionComponent : Component
{
    [DataField("abducted"), ViewVariables(VVAccess.ReadWrite)]
    public int Abducted;

    [DataField("hashset"), ViewVariables(VVAccess.ReadWrite)]
    public HashSet<NetEntity> AbductedHashs = [];
}

public sealed partial class ExitConsoleEvent : InstantActionEvent
{
}

public sealed partial class SendYourselfEvent : WorldTargetActionEvent
{
}

public sealed partial class AbductorReturnToShipEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public enum AbductorExperimentatorVisuals : byte
{
    Full
}

[Serializable, NetSerializable]
public enum AbductorCameraConsoleUIKey
{
    Key
}

[Serializable, NetSerializable]
public enum AbductorConsoleUIKey
{
    Key
}

[Serializable, NetSerializable]
public enum AbductorArmorModeType : byte
{
    Combat,
    Stealth
}

[Serializable, NetSerializable]
public sealed partial class AbductorReturnDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class AbductorGizmoMarkDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class AbductorSendYourselfDoAfterEvent : SimpleDoAfterEvent
{
    [DataField("coordinates", required: true)]
    public NetCoordinates TargetCoordinates;

    private AbductorSendYourselfDoAfterEvent()
    {
    }

    public AbductorSendYourselfDoAfterEvent(NetCoordinates coords) => TargetCoordinates = coords;
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class AbductorAttractDoAfterEvent : SimpleDoAfterEvent
{
    [DataField("coordinates", required: true)]
    public NetCoordinates TargetCoordinates;

    [DataField("victim", required: true)]
    public NetEntity Victim;

    private AbductorAttractDoAfterEvent()
    {
    }

    public AbductorAttractDoAfterEvent(NetCoordinates coords, NetEntity target)
    {
        TargetCoordinates = coords;
        Victim = target;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed class AbductorCameraConsoleBuiState : BoundUserInterfaceState
{
    public Dictionary<int, StationBeacons> Stations { get; set; } = new();
}

[Serializable, NetSerializable]
public sealed class AbductorConsoleBuiState : BoundUserInterfaceState
{
    public NetEntity? Target { get; set; }
    public string? TargetName { get; set; }
    public string? VictimName { get; set; }
    public bool AlienPadFound { get; set; }
    public bool ExperimentatorFound { get; set; }
    public bool ArmorFound { get; set; }
    public bool ArmorLocked { get; set; }
    public AbductorArmorModeType CurrentArmorMode { get; set; }
}

[Serializable, NetSerializable]
public sealed class StationBeacons
{
    public int StationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<NavMapBeacon> Beacons { get; set; } = new();
}

[Serializable, NetSerializable]
public sealed class AbductorBeaconChosenBuiMsg : BoundUserInterfaceMessage
{
    public NavMapBeacon Beacon { get; set; }
}

[Serializable, NetSerializable]
public sealed class AbductorAttractBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AbductorCompleteExperimentBuiMsg : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class AbductorVestModeChangeBuiMsg : BoundUserInterfaceMessage
{
    public AbductorArmorModeType Mode { get; set; }
}

[Serializable, NetSerializable]
public sealed class AbductorLockBuiMsg : BoundUserInterfaceMessage
{
}