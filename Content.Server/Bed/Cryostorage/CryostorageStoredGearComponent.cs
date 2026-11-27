//DS-14 start
namespace Content.Server.Bed.Cryostorage;

[RegisterComponent]
public sealed partial class CryostorageStoredGearComponent : Component
{
    public const string ContainerId = "cryo_gear";

    [DataField]
    public int NextId = 1;

    [DataField]
    public List<CryostorageGearEntry> Entries = new();
}

[DataDefinition]
public sealed partial class CryostorageGearEntry
{
    [DataField]
    public int Id;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public Dictionary<string, EntityUid> Inventory = new();

    [DataField]
    public Dictionary<string, EntityUid> Hands = new();
}
//DS-14 end
