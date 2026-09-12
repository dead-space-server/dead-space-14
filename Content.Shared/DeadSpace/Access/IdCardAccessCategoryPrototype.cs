// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Access;

/// <summary>Console presentation categories; these do not grant access like access groups do.</summary>
[Prototype("idCardAccessCategory")]
public sealed partial class IdCardAccessCategoryPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public LocId Name = string.Empty;
    [DataField] public int Order;
    [DataField] public bool CentCommOnly;
    [DataField(required: true)] public HashSet<ProtoId<AccessLevelPrototype>> AccessLevels = new();
}
