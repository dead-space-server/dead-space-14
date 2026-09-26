using Content.Shared.Security;
using Content.Shared.StationRecords;
using Robust.Shared.Serialization;

namespace Content.Shared.CriminalRecords;

[Serializable, NetSerializable]
public enum CriminalRecordsConsoleKey : byte
{
    Key
}

/// <summary>
///     Criminal records console state. There are a few states:
///     - SelectedKey null, Record null, RecordListing null
///         - The station record database could not be accessed.
///     - SelectedKey null, Record null, RecordListing non-null
///         - Records are populated in the database, or at least the station has
///           the correct component.
///     - SelectedKey non-null, Record null, RecordListing non-null
///         - The selected key does not have a record tied to it.
///     - SelectedKey non-null, Record non-null, RecordListing non-null
///         - The selected key has a record tied to it, and the record has been sent.
///
///     - there is added new filters and so added new states
///         -SelectedKey null, Record null, RecordListing null, filters non-null
///            the station may have data, but they all did not pass through the filters
///
///     Other states are erroneous.
/// </summary>
[Serializable, NetSerializable]
public sealed class CriminalRecordsConsoleState : BoundUserInterfaceState
{
    /// <summary>
    /// Currently selected crewmember record key.
    /// </summary>
    public uint? SelectedKey = null;
    public CriminalRecord? CriminalRecord = null;
    public GeneralStationRecord? StationRecord = null;
    public SecurityStatus FilterStatus = SecurityStatus.None;
    public readonly Dictionary<uint, string>? RecordListing;
    public readonly StationRecordsFilter? Filter;

    // DS14-start
    public bool CanPrint;
    // DS14-end

    public CriminalRecordsConsoleState(Dictionary<uint, string>? recordListing, StationRecordsFilter? newFilter)
    {
        RecordListing = recordListing;
        Filter = newFilter;
    }

    /// <summary>
    /// Default state for opening the console
    /// </summary>
    public CriminalRecordsConsoleState() : this(null, null)
    {
    }

    public bool IsEmpty() => SelectedKey == null && StationRecord == null && CriminalRecord == null && RecordListing == null;
}

/// <summary>
/// Used to change status, respecting the wanted/reason nullability rules in <see cref="CriminalRecord"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class CriminalRecordChangeStatus : BoundUserInterfaceMessage
{
    public readonly SecurityStatus Status;
    public readonly string? Reason;

    // DS14-start
    public readonly string? Articles;
    public readonly string? Sentence;
    // DS14-end

    public CriminalRecordChangeStatus(SecurityStatus status, string? reason, string? articles = null, string? sentence = null)
    {
        Status = status;
        Reason = reason;
        Articles = articles; // DS14
        Sentence = sentence; // DS14
    }
}

/// <summary>
/// Used to add a single line to the record's crime history.
/// </summary>
[Serializable, NetSerializable]
public sealed class CriminalRecordAddHistory : BoundUserInterfaceMessage
{
    public readonly string Line;

    public CriminalRecordAddHistory(string line)
    {
        Line = line;
    }
}

/// <summary>
/// Used to delete a single line from the crime history, by index.
/// </summary>
[Serializable, NetSerializable]
public sealed class CriminalRecordDeleteHistory : BoundUserInterfaceMessage
{
    public readonly uint Index;

    public CriminalRecordDeleteHistory(uint index)
    {
        Index = index;
    }
}

/// <summary>
/// Used to set what status to filter by index.
///
/// </summary>
///
[Serializable, NetSerializable]

public sealed class CriminalRecordSetStatusFilter : BoundUserInterfaceMessage
{
    public readonly SecurityStatus FilterStatus;
    public CriminalRecordSetStatusFilter(SecurityStatus newFilterStatus)
    {
        FilterStatus = newFilterStatus;
    }
}

// DS14-start
[Serializable, NetSerializable]
public sealed class CriminalRecordPrintDocument : BoundUserInterfaceMessage
{
}
// DS14-end

