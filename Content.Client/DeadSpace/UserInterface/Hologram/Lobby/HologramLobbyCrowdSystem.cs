// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Lobby;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>Keeps the latest lobby crowd snapshot from the server for the "Dead Space" lobby.</summary>
public sealed class HologramLobbyCrowdSystem : EntitySystem
{
    /// <summary>The latest snapshot, trimmed to <see cref="LobbyCrowdSnapshotEvent.MaxEntries"/>; null before the first.</summary>
    public LobbyCrowdSnapshotEvent? Latest { get; private set; }

    /// <summary>Raised after <see cref="Latest"/> changes.</summary>
    public event Action? Updated;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<LobbyCrowdSnapshotEvent>(OnSnapshot);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Latest = null;
    }

    private void OnSnapshot(LobbyCrowdSnapshotEvent ev)
    {
        // Never trust the sizes: whatever the server sends, the lobby spawns a bounded number of dolls.
        if (ev.Entries.Count > LobbyCrowdSnapshotEvent.MaxEntries)
            ev.Entries.RemoveRange(LobbyCrowdSnapshotEvent.MaxEntries, ev.Entries.Count - LobbyCrowdSnapshotEvent.MaxEntries);

        Latest = ev;
        Updated?.Invoke();
    }
}
