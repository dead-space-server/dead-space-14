// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Roles;
using Content.Shared.Preferences.Loadouts;
using Content.Server.Antag.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.AntagGearSelector;

[RegisterComponent, Access(typeof(AntagGearSelectorSystem))]
public sealed partial class AntagGearSelectorComponent : Component
{
    [DataField]
    public TimeSpan SelectionTimeout = TimeSpan.FromMinutes(1);

    [DataField]
    public HashSet<ProtoId<AntagPrototype>> Roles = new();

    [DataField]
    public List<AntagGearSelectorEntry> Gear = new();

    /// <summary>
    /// More roles with choices of their own, so one game rule can offer different picks to different roles.
    /// </summary>
    [DataField]
    public List<AntagGearSelectorSet> Sets = new();
}

[DataDefinition]
public sealed partial class AntagGearSelectorSet
{
    [DataField(required: true)]
    public HashSet<ProtoId<AntagPrototype>> Roles = new();

    [DataField(required: true)]
    public List<AntagGearSelectorEntry> Gear = new();
}

[DataDefinition]
public sealed partial class AntagGearSelectorEntry
{
    [DataField(required: true)] public LocId Name;
    [DataField(required: true)] public LocId Description;
    [DataField(required: true)] public EntProtoId SpritePrototype;
    [DataField] public ProtoId<StartingGearPrototype>? StartingGear;
    [DataField] public ProtoId<RoleLoadoutPrototype>? RoleLoadout;
    [DataField] public ComponentRegistry Components = new();
    [DataField] public BriefingData? Briefing;
    [DataField] public List<AntagGearSelectorEntry> Perks = new();
}
