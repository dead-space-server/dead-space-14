// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Ninja;

[Serializable, NetSerializable]
public sealed class NinjaDisguiseEntry
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Description = string.Empty;

    [DataField]
    public string? JobTitle;

    [DataField]
    public ProtoId<JobIconPrototype> JobIcon = "JobIconUnknown";

    [DataField]
    public bool HasIdCard;

    [DataField]
    public string? IdCardName;

    [DataField]
    public string? IdCardJobTitle;

    [DataField]
    public ProtoId<JobIconPrototype>? IdCardJobIcon;

    [DataField]
    public ProtoId<JobPrototype>? IdCardJobPrototype;

    [DataField]
    public List<ProtoId<DepartmentPrototype>> IdCardJobDepartments = new();

    [DataField]
    public NinjaDisguiseAppearance? Appearance;

    [DataField]
    public string? InventorySpeciesId;

    [DataField]
    public List<NinjaDisguiseInventoryEntry> Inventory = new();

    [DataField]
    public List<string> HiddenClothingSlots = new();

    [DataField]
    public IdentityBlockerCoverage IdentityBlockedCoverage = IdentityBlockerCoverage.NONE;
}

[Serializable, NetSerializable]
public sealed class NinjaDisguiseInventoryEntry
{
    [DataField]
    public string Slot = string.Empty;

    [DataField]
    public EntProtoId ItemId;

    [DataField]
    public Vector2 SlotOffset;
}