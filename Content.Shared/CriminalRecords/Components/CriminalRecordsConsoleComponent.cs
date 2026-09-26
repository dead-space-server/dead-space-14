using Content.Shared.CriminalRecords.Systems;
using Content.Shared.CriminalRecords.Components;
using Content.Shared.CriminalRecords;
using Content.Shared.DeadSpace.Photocopier; // DS14
using Content.Shared.Radio;
using Content.Shared.StationRecords;
using Robust.Shared.Audio; // DS14
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom; // DS14
using Content.Shared.Security;
using Robust.Shared.GameStates;

namespace Content.Shared.CriminalRecords.Components;

/// <summary>
/// A component for Criminal Record Console storing an active station record key and a currently applied filter
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedCriminalRecordsConsoleSystem))]
public sealed partial class CriminalRecordsConsoleComponent : Component
{
    /// <summary>
    /// Currently active station record key.
    /// There is no station parameter as the console uses the current station.
    /// </summary>
    /// <remarks>
    /// TODO: in the future this should be clientside instead of something players can fight over.
    /// Client selects a record and tells the server the key it wants records for.
    /// Server then sends a state with just the records, not the listing or filter, and the client updates just that.
    /// I don't know if it's possible to have multiple bui states right now.
    /// </remarks>
    [DataField]
    public uint? ActiveKey;

    /// <summary>
    /// Currently applied filter.
    /// </summary>
    [DataField]
    public StationRecordsFilter? Filter;

    /// <summary>
    /// Current seleced security status for the filter by criminal status dropdown.
    /// </summary>
    [DataField]
    public SecurityStatus FilterStatus;

    /// <summary>
    /// Channel to send messages to when someone's status gets changed.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> SecurityChannel = "Security";

    /// <summary>
    /// Max length of arrest and crime history strings.
    /// </summary>
    [DataField, AutoNetworkedField]
    public uint MaxStringLength = 256;

    // DS14-start
    [DataField, AutoNetworkedField]
    public bool HasPrinter = true;

    [DataField]
    public ProtoId<PaperworkFormPrototype> ArrestWarrantForm = "CriminalArrestWarrant";

    [DataField]
    public ProtoId<PaperworkFormPrototype> VerdictForm = "CriminalVerdict";

    [DataField]
    public SoundSpecifier PrintSound = new SoundCollectionSpecifier("PrinterPrint");

    [DataField]
    public TimeSpan PrintDelay = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextPrintTime = TimeSpan.Zero;
    // DS14-end
}
