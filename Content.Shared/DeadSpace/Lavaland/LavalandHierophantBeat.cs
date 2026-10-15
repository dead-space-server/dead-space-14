// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>
/// The beat of the Hierophant: tile-by-tile movement (SS13 style). Comes from the blessing of the Hierophant staff
/// or from standing on an arena that has it; while it lasts, slides (speed sliding) do not work.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class LavalandHierophantBeatComponent : Component
{
    /// <summary>Blessed with the Hierophant staff.</summary>
    [DataField, AutoNetworkedField]
    public bool Blessed;

    /// <summary>Standing on an arena with tile movement.</summary>
    [DataField, AutoNetworkedField]
    public bool InArena;
}

[Serializable, NetSerializable]
public sealed partial class LavalandHierophantBlessDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Alt + right click with an item whose <see cref="Components.LavalandAltUseActionComponent"/> has no action:
/// the item handles it itself (see <see cref="LavalandAltUseEvent"/>).
/// </summary>
[Serializable, NetSerializable]
public sealed class LavalandAltUseRequestEvent(NetEntity item, NetCoordinates coordinates, NetEntity? target) : EntityEventArgs
{
    public NetEntity Item = item;
    public NetCoordinates Coordinates = coordinates;
    public NetEntity? Target = target;
}

/// <summary>Raised on the held item when its user Alt + right clicks.</summary>
[ByRefEvent]
public record struct LavalandAltUseEvent(EntityUid User, EntityCoordinates Coordinates, EntityUid? Target);
