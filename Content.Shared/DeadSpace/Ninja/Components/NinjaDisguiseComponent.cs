// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Ninja.Systems;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedNinjaDisguiseSystem))]
public sealed partial class NinjaDisguiseComponent : Component
{
    [DataField]
    public EntProtoId ActionScan = "ActionNinjaDisguiseScan";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionScanEntity;

    [DataField]
    public EntProtoId ActionMenu = "ActionNinjaDisguiseMenu";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionMenuEntity;

    [DataField]
    public int MaxEntries = 3;

    [DataField, AutoNetworkedField]
    public List<NinjaDisguiseEntry> Entries = new();

    [DataField]
    public float EnergyCost = 50f;

    [DataField("drainRate")]
    public float DrainRate = 5f;

    [DataField]
    public float ScanDelay = 2f;

    [DataField]
    public float ApplyDelay = 2f;

    [DataField]
    public EntProtoId? Effect;

    [DataField]
    public EntityUid? EffectEntity;

    public DoAfterId? ActiveDoAfter;

    public int? PendingIndex;

    [DataField, AutoNetworkedField]
    public bool Disguised;

    [DataField, AutoNetworkedField]
    public int? ActiveIndex;

    [DataField]
    public NinjaDisguiseEntry? OriginalAppearance;

    public List<EntityUid> DisabledIdentityBlockers = new();
}

public sealed partial class NinjaDisguiseScanEvent : EntityTargetActionEvent;

public sealed partial class NinjaDisguiseMenuEvent : InstantActionEvent;

[ByRefEvent]
public record struct NinjaDisguiseRevealedEvent;

[ByRefEvent]
public record struct NinjaDisguiseStripAttemptEvent(EntityUid Wearer);

[Serializable, NetSerializable]
public sealed partial class NinjaDisguiseScanDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class NinjaDisguiseApplyDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}