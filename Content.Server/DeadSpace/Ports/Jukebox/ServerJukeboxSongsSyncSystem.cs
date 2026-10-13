using Content.Shared.GameTicking;
using Content.Shared.DeadSpace.Ports.Jukebox;

namespace Content.Server.DeadSpace.Ports.Jukebox;

public sealed class ServerJukeboxSongsSyncSystem : EntitySystem
{
    [Dependency] private readonly ServerJukeboxSongsSyncManager _jukeboxManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => ClearSongs());
    }

    internal void ClearSongs() => RaiseNetworkEvent(new JukeboxRoundClearedEvent(_jukeboxManager.CleanUp()));
}
