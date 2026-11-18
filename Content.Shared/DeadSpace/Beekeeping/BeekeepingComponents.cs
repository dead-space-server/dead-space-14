// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Beekeeping;

[Serializable, NetSerializable]
public enum HiveFrameVisuals : byte
{
    HasHoneycomb,
}

[Serializable, NetSerializable]
public enum HiveFrameVisualLayers : byte
{
    Base,
}

[Serializable, NetSerializable]
public sealed partial class BeePollinatingDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class HiveFrameDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Raised on a plant when a bee pollinates it.
/// </summary>
[ByRefEvent]
public readonly record struct PlantPollinatedEvent(EntityUid Bee, float Pollen);

/// <summary>
/// Raised on a hive when a bee brings pollen.
/// </summary>
[ByRefEvent]
public readonly record struct BeePollenDepositedEvent(EntityUid Bee, float Amount);
