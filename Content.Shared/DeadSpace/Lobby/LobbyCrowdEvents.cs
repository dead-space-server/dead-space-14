// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Lobby;

/// <summary>
/// Server to client: who else waits in the lobby, for the people walking through the "Dead Space" lobby cabin, plus
/// the round line of the lobby. Only what the stock lobby info text already shows, and per player only what it takes to
/// dress a preview doll: no names, no user ids. Built by the server from saved profiles; the client validates every
/// entry before spawning anything.
/// </summary>
[Serializable, NetSerializable]
public sealed class LobbyCrowdSnapshotEvent : EntityEventArgs
{
    /// <summary>Most players one snapshot carries.</summary>
    public const int MaxEntries = 8;

    /// <summary>Most markings an entry may carry.</summary>
    public const int MaxMarkings = 64;

    public int RoundId;
    public int PlayerCount;
    public int ReadyCount;
    public string? Map;
    public string? Mode;
    public List<LobbyCrowdEntry> Entries = new();
}

[Serializable, NetSerializable]
public sealed class LobbyCrowdEntry
{
    public ProtoId<SpeciesPrototype> Species;
    public Sex Sex;
    public HumanoidCharacterAppearance Appearance = default!;
    public ProtoId<JobPrototype> Job;
}
