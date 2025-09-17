using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.ADT.EmotePanel;
using Robust.Shared.Prototypes;
using Content.Shared.Chat.Prototypes;
using Content.Server.Emoting.Components;
using Content.Shared.Sirena.Animations;
using Content.Shared.Speech.Components;
using Content.Shared.Whitelist;
using Content.Shared.Speech;

namespace Content.Server.ADT.EmotePanel;

public sealed class EmotePanelSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<RequestEmoteMenuEvent>(OnRequestEmoteMenu);
        SubscribeAllEvent<PlayEmoteEvent>(OnPlayEmote);
    }

    private void OnRequestEmoteMenu(RequestEmoteMenuEvent msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        var target = GetEntity(msg.Target);

        if (!player.HasValue || player.Value != target)
            return;

        var availableEmotes = GetAvailableEmotes(target);
        RaiseNetworkEvent(new RequestEmoteMenuEvent(msg.Target, availableEmotes), args.SenderSession);
    }

    private List<string> GetAvailableEmotes(EntityUid uid)
    {
        return _prototypeManager.EnumeratePrototypes<EmotePrototype>()
            .Where(e => IsEmoteAvailable(uid, e))
            .OrderBy(e => Loc.GetString(e.Name))
            .Select(e => e.ID)
            .ToList();
    }

    private bool IsEmoteAvailable(EntityUid uid, EmotePrototype emote)
    {
        if (emote.Category == EmoteCategory.Invalid ||
            emote.ChatTriggers.Count == 0 ||
            !emote.ShowInMenu ||
            !_whitelistSystem.IsWhitelistPassOrNull(emote.Whitelist, uid) ||
            _whitelistSystem.IsBlacklistPass(emote.Blacklist, uid))
            return false;

        if (!emote.Available &&
            _entManager.TryGetComponent<SpeechComponent>(uid, out var speech) &&
            !speech.AllowedEmotes.Contains(emote.ID))
            return false;

        return emote.Category switch
        {
            EmoteCategory.Hands when !HasComp<BodyEmotesComponent>(uid) => false,
            EmoteCategory.Vocal when !HasComp<VocalComponent>(uid) => false,
            EmoteCategory.Animations when !HasComp<EmoteAnimationComponent>(uid) => false,
            _ => true
        };
    }

    private void OnPlayEmote(PlayEmoteEvent msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue || player.Value != GetEntity(msg.Target))
            return;

        _chat.TryEmoteWithChat(player.Value, msg.ProtoId);
    }
}
