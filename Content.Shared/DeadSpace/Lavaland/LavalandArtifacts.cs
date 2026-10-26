// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lavaland;

/// <summary>Appearance keys of lavaland mining equipment and necropolis artifacts.</summary>
[Serializable, NetSerializable]
public enum LavalandArtifactVisuals : byte
{
    /// <summary>string: marker beacon colour id.</summary>
    Color,
    /// <summary>bool: lazarus injector loaded, lantern holds its wisp, bottle holds its ship.</summary>
    Charged,
    /// <summary>bool: rod of asclepius bound, memento mori linked.</summary>
    Active,
}

[Serializable, NetSerializable]
public enum LavalandArtifactLayers : byte
{
    Base,
}

public sealed partial class LavalandMementoMoriActionEvent : InstantActionEvent;

public sealed partial class LavalandBerserkActionEvent : InstantActionEvent;

public sealed partial class LavalandCainAndAbelThrowActionEvent : WorldTargetActionEvent;

/// <summary>Cain and Abel: the other dagger to the fore (the dash to the dagger, or the ring of ice).</summary>
public sealed partial class LavalandCainAndAbelSwapActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class LavalandAsclepiusOathDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class LavalandMementoMoriDoAfterEvent : SimpleDoAfterEvent;

/// <summary>A warp cube held up for a while before it takes its holder to its twin.</summary>
[Serializable, NetSerializable]
public sealed partial class LavalandWarpCubeDoAfterEvent : SimpleDoAfterEvent;
