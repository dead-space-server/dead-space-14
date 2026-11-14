// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    public void SendIncantation(EntityUid source, string plain, string markup, float range)
    {
        var nameEv = new TransformSpeakerNameEvent(source, Name(source));
        RaiseLocalEvent(source, nameEv);

        var wrapped = Loc.GetString("rune-incantation-wrap",
            ("entityName", FormattedMessage.EscapeText(nameEv.VoiceName)),
            ("message", markup));

        foreach (var (session, data) in GetRecipients(source, range))
        {
            if (session.AttachedEntity is not { Valid: true } listener)
                continue;

            var entRange = MessageRangeCheck(session, data, ChatTransmitRange.Normal);
            if (entRange == MessageRangeCheckResult.Disallowed || IsCriticalHearingBlocked(listener, source, ChatChannel.Local))
                continue;

            _chatManager.ChatMessageToOne(ChatChannel.Local, plain, wrapped, source, entRange == MessageRangeCheckResult.HideChat, session.Channel);
        }

        _replay.RecordServerMessage(new ChatMessage(ChatChannel.Local, plain, wrapped, GetNetEntity(source), null, MessageRangeHideChatForReplay(ChatTransmitRange.Normal)));
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Incantation from {ToPrettyString(source):user}: {plain}");
    }
}
