// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.RoundEnd;
using Content.Shared.DeadSpace.Arena;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

public sealed class RoundEndCreditsEventsSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RoundEndCreditsVoteEvent>(OnVote);
        SubscribeNetworkEvent<RoundEndCreditsStartEvent>(OnCredits);
        SubscribeNetworkEvent<ArenaManifestEvent>(OnArena);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnVote(RoundEndCreditsVoteEvent ev)
    {
        _ui.GetUIController<RoundEndCreditsUIController>().OnVote(ev);
    }

    private void OnCredits(RoundEndCreditsStartEvent ev)
    {
        _ui.GetUIController<RoundEndCreditsUIController>().OnCredits(ev);
    }

    private void OnArena(ArenaManifestEvent ev)
    {
        _ui.GetUIController<RoundEndCreditsUIController>().OnArena(ev);
        _ui.GetUIController<RoundEndSummaryUIController>().OnArenaManifest(ev);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _ui.GetUIController<RoundEndCreditsUIController>().OnRoundRestart();
    }
}
