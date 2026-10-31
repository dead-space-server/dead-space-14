using Content.Shared.DeadSpace.Body.Limbs; // DS14
using Content.Shared.FixedPoint; // DS14
using Robust.Shared.Serialization;

namespace Content.Shared.MedicalScanner;

/// <summary>
/// On interacting with an entity retrieves the entity UID for use with getting the current damage of the mob.
/// </summary>
[Serializable, NetSerializable]
public sealed class HealthAnalyzerScannedUserMessage : BoundUserInterfaceMessage
{
    public HealthAnalyzerUiState State;

    public HealthAnalyzerScannedUserMessage(HealthAnalyzerUiState state)
    {
        State = state;
    }
}

/// <summary>
/// Contains the current state of a health analyzer control. Used for the health analyzer and cryo pod.
/// </summary>
[Serializable, NetSerializable]
public struct HealthAnalyzerUiState
{
    public readonly NetEntity? TargetEntity;
    public float Temperature;
    public float BloodLevel;
    public bool? ScanMode;
    public bool? Bleeding;
    public bool? Unrevivable;

    // DS14-start
    public bool? Unclonable;
    public bool Overheating;
    public List<HealthAnalyzerReagentEntry> Reagents = new(); // DS14
    public bool HasLimbs; // DS14
    public LimbScanSummary LimbSummary; // DS14
    public List<LimbScanIssue> LimbIssues = new(); // DS14
    public List<byte> LimbShades = new(); // DS14
    public List<LimbPartDamage> LimbParts = new(); // DS14

    public HealthAnalyzerUiState() {}

    public HealthAnalyzerUiState(NetEntity? targetEntity, float temperature, float bloodLevel, bool? scanMode, bool? bleeding, bool? unrevivable, bool? unclonable = null, List<HealthAnalyzerReagentEntry>? reagents = null, bool overheating = false, bool hasLimbs = false, LimbScanSummary limbSummary = default, List<LimbScanIssue>? limbIssues = null, List<byte>? limbShades = null, List<LimbPartDamage>? limbParts = null)
    {
        TargetEntity = targetEntity;
        Temperature = temperature;
        BloodLevel = bloodLevel;
        ScanMode = scanMode;
        Bleeding = bleeding;
        Unrevivable = unrevivable;
        Unclonable = unclonable; // DS14
        Overheating = overheating;
        Reagents = reagents ?? new List<HealthAnalyzerReagentEntry>(); // DS14
        HasLimbs = hasLimbs; // DS14
        LimbSummary = limbSummary; // DS14
        LimbIssues = limbIssues ?? new List<LimbScanIssue>(); // DS14
        LimbShades = limbShades ?? new List<byte>(); // DS14
        LimbParts = limbParts ?? new List<LimbPartDamage>(); // DS14
    }
    // DS14-end
}

// DS14-start
[Serializable, NetSerializable]
public struct HealthAnalyzerReagentEntry
{
    public string ReagentId;
    public FixedPoint2 Quantity;
    public bool Overdosed;

    public HealthAnalyzerReagentEntry(string reagentId, FixedPoint2 quantity, bool overdosed)
    {
        ReagentId = reagentId;
        Quantity = quantity;
        Overdosed = overdosed;
    }
}
// DS14-end
