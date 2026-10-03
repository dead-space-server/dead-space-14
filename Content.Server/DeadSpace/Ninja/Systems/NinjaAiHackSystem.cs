// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Silicons.Laws;
using Content.Shared.DeadSpace.Ninja;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Server.Station.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Content.Shared.Chat;
using Content.Server.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaAiHackSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> StationAiTag = "StationAi";

    [Dependency] private readonly NinjaGlovesSystem _gloves = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly IonStormSystem _ionStorm = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaAiHackComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<NinjaAiHackComponent, NinjaAiHackDoAfterEvent>(OnDoAfter);
    }

    private void OnBeforeInteractHand(EntityUid uid, NinjaAiHackComponent comp, BeforeInteractHandEvent args)
    {
        if (args.Handled || !HasComp<SiliconLawUpdaterComponent>(args.Target))
            return;

        if (!_gloves.AbilityCheck(uid, args, out _))
            return;

        if (NotifyStationAiPlayers(comp.StartedMessage) == 0)
        {
            _popup.PopupEntity(Loc.GetString(comp.NoAiMessage), uid, uid);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, comp.Delay, new NinjaAiHackDoAfterEvent(), target: args.Target, used: uid, eventTarget: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            CancelDuplicate = true
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            args.Handled = true;
    }

    private void OnDoAfter(EntityUid uid, NinjaAiHackComponent comp, NinjaAiHackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;
        args.Handled = true;

        if (ScrambleStationAiLaws() == 0)
        {
            _popup.PopupEntity(Loc.GetString(comp.NoAiMessage), uid, uid);
            return;
        }

        NotifyStationAiPlayers(comp.HackedMessage);

        var station = _station.GetOwningStation(args.Target.Value);

        if (station != null)
        {
            _chat.DispatchStationAnnouncement(
                station.Value,
                Loc.GetString(comp.AnnouncementMessage),
                Loc.GetString(comp.AnnouncementSender),
                announcementSound: comp.AnnouncementSound,
                colorOverride: comp.AnnouncementColor);
        }

        RemComp<NinjaAiHackComponent>(uid);
        var ev = new NinjaAiHackEvent(args.User, args.Target.Value);
        RaiseLocalEvent(ref ev);
    }

    private int ScrambleStationAiLaws()
    {
        var count = 0;

        var query = EntityQueryEnumerator<SiliconLawBoundComponent, TagComponent>();
        while (query.MoveNext(out var uid, out _, out var tags))
        {
            if (!_tags.HasTag(tags, StationAiTag))
                continue;

            count++;

            if (_emag.CheckFlag(uid, EmagType.Interaction))
                continue;

            _lawSystem.SetSubvertedLaws(uid, _ionStorm.GenerateIonLaws(3));
        }

        return count;
    }

    private int NotifyStationAiPlayers(string message)
    {
        var count = 0;

        var query = EntityQueryEnumerator<SiliconLawBoundComponent, TagComponent, ActorComponent>();
        while (query.MoveNext(out _, out _, out var tags, out var actor))
        {
            if (!_tags.HasTag(tags, StationAiTag))
                continue;

            var msg = Loc.GetString(message);
            var wrappedMessage = Loc.GetString("chat-manager-ninja-ai-wrap-message", ("message", msg));
            _chatManager.ChatMessageToOne(ChatChannel.Server, msg, wrappedMessage, default, false, actor.PlayerSession.Channel, colorOverride: Color.Red);
            count++;
        }

        return count;
    }
}
