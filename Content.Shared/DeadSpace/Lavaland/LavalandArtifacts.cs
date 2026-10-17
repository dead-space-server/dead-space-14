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

[Serializable, NetSerializable]
public sealed partial class LavalandAsclepiusOathDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class LavalandMementoMoriDoAfterEvent : SimpleDoAfterEvent;
