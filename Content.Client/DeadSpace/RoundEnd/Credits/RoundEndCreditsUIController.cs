// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Client.MainMenu;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.DeadSpace.Arena;
using Content.Shared.DeadSpace.RoundEnd.Credits;
using Content.Shared.GameTicking;
using JetBrains.Annotations;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.RoundEnd.Credits;

[UsedImplicitly]
public sealed class RoundEndCreditsUIController : UIController, IOnStateEntered<MainScreen>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly TimeSpan FallbackDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FallbackFade = TimeSpan.FromSeconds(2);

    private ChatBox? _chat;

    private RoundEndMessageEvent? _round;
    private RoundEndCreditsVoteEvent? _vote;
    private RoundEndCreditsStartEvent? _credits;
    private ArenaManifestEvent? _arena;
    private RoundEndCreditsOverlay? _overlay;
    private TimeSpan _roundReceived;
    private bool _roundOver;

    public bool ShowingCredits => _overlay is { HasCredits: true };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RoundEndCreditsVoteEvent>(OnVote);
        SubscribeNetworkEvent<RoundEndCreditsStartEvent>(OnCredits);
        SubscribeNetworkEvent<ArenaManifestEvent>(OnArena);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public void OnStateEntered(MainScreen state)
    {
        CloseOverlay();
        _round = null;
        _vote = null;
        _credits = null;
        _arena = null;
        _roundOver = false;
    }

    public void OnRoundEnd(RoundEndMessageEvent message)
    {
        if (!_roundOver && _overlay is { } live && live.RoundId == message.RoundId)
            return;

        if (_roundOver)
        {
            _vote = null;
            _credits = null;
            _arena = null;
        }
        else if (_round?.RoundId != message.RoundId)
        {
            _vote = _vote?.RoundId == message.RoundId ? _vote : null;
            _credits = _credits?.RoundId == message.RoundId ? _credits : null;
            _arena = null;
        }

        _round = message;
        _roundReceived = _timing.CurTime;
        _roundOver = false;

        var fadeEnd = _vote?.FadeEnd ?? _timing.CurTime + FallbackFade;
        OpenOverlay(message, fadeEnd);

        if (_vote != null)
            ApplyVote(_vote);

        if (_credits != null)
            _overlay?.SetCredits(_credits, _credits.StartTime);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_overlay is not { HasCredits: false } overlay ||
            _vote != null ||
            _round == null ||
            _timing.CurTime - _roundReceived < FallbackDelay)
        {
            return;
        }

        _credits = BuildFallbackCredits(_round);
        overlay.SetCredits(_credits, _timing.CurTime);
    }

    private void OnVote(RoundEndCreditsVoteEvent ev, EntitySessionEventArgs args)
    {
        _vote = ev;
        if (_overlay is { } overlay && overlay.RoundId == ev.RoundId)
            ApplyVote(ev);
    }

    private void ApplyVote(RoundEndCreditsVoteEvent ev)
    {
        _overlay?.SetVote(ev, _prototypes);
    }

    private void OnCredits(RoundEndCreditsStartEvent ev, EntitySessionEventArgs args)
    {
        _credits = ev;
        if (_overlay is { } overlay && overlay.RoundId == ev.RoundId)
            overlay.SetCredits(ev, ev.StartTime);
    }

    private void OnArena(ArenaManifestEvent ev, EntitySessionEventArgs args)
    {
        _arena = ev;
        _overlay?.SetArena(ev);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev, EntitySessionEventArgs args)
    {
        _vote = null;
        _roundOver = true;

        if (_overlay is { } overlay)
            overlay.BeginClose();
    }

    private void OpenOverlay(RoundEndMessageEvent round, TimeSpan fadeEnd)
    {
        CloseOverlay();

        _chat ??= new ChatBox
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var overlay = new RoundEndCreditsOverlay(round, fadeEnd, EntityManager, _timing, _cache, _random, _chat);
        overlay.SetPrototypes(_prototypes);
        overlay.Closed += () =>
        {
            if (_overlay == overlay)
                CloseOverlay();
            else
                overlay.Shutdown();
        };

        overlay.VoteCast += (nomination, candidate) =>
            EntityManager.EntityNetManager?.SendSystemNetworkMessage(
                new RoundEndCreditsVoteCastEvent(round.RoundId, nomination, candidate));

        if (_arena != null)
            overlay.SetArena(_arena);

        var root = UIManager.RootControl;
        root.AddChild(overlay);
        overlay.SetPositionInParent(GetOverlayPosition(root));
        _overlay = overlay;
    }

    private static int GetOverlayPosition(WindowRoot root)
    {
        var position = root.ChildCount - 1;
        foreach (var child in root.Children)
        {
            if (child is DropDownDebugConsole || child == root.ModalRoot)
                position = Math.Min(position, child.GetPositionInParent());
        }

        return position;
    }

    private void CloseOverlay()
    {
        if (_overlay == null)
            return;

        var overlay = _overlay;
        _overlay = null;
        overlay.Shutdown();
    }

    private static RoundEndCreditsStartEvent BuildFallbackCredits(RoundEndMessageEvent round)
    {
        var players = round.AllPlayersEndInfo;
        var antags = players.Count(p => p.Antag && p.ShowInAntagManifest);
        var rows = MathF.Ceiling(players.Length / 3f);

        return new RoundEndCreditsStartEvent
        {
            RoundId = round.RoundId,
            ContentDuration = RoundEndCreditsOverlay.IntroSeconds + 12f + antags * 5f + rows * 2.4f,
        };
    }
}
