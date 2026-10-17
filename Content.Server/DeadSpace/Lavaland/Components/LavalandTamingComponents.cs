// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Lavaland.Components;

/// <summary>
/// Someone who has brought creatures back with a lazarus injector: while any of them is alive they get
/// orders to give them (follow, stay, attack), like the rat king's.
/// </summary>
[RegisterComponent]
public sealed partial class LavalandTamerComponent : Component
{
    [ViewVariables]
    public readonly List<EntityUid> Tamed = new();

    [ViewVariables]
    public LavalandTamedOrder Order = LavalandTamedOrder.Follow;

    [ViewVariables]
    public EntityUid? FollowAction;

    [ViewVariables]
    public EntityUid? StayAction;

    [ViewVariables]
    public EntityUid? AttackAction;

    public TimeSpan NextCheck;
}

/// <summary>A creature tamed by a lazarus injector: it obeys its tamer's orders.</summary>
[RegisterComponent]
public sealed partial class LavalandTamedComponent : Component
{
    [ViewVariables]
    public EntityUid? Tamer;

    /// <summary>The side it fights on now; whatever it spawns (broods, heads, children) joins it too.</summary>
    [ViewVariables]
    public List<ProtoId<NpcFactionPrototype>> Factions = new();

    /// <summary>What it was before taming: an EMP turns it back.</summary>
    [ViewVariables]
    public List<ProtoId<NpcFactionPrototype>> OriginalFactions = new();

    [ViewVariables]
    public string? OriginalRoot;

    /// <summary>
    /// The one its tamer pointed at: it runs at it and attacks by itself (the AI's pathfinding gets lost
    /// on the lavaland grids), until the target is dead or the order changes.
    /// </summary>
    [ViewVariables]
    public EntityUid? Target;
}
