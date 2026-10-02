// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Globalization;
using System.Text;
using Content.Client.Administration.Managers;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Client.Player;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Chat;

public sealed class ChatEntityCommandLinkSystem : EntitySystem
{
    public static readonly Type[] TagsWithCommandLinks =
    [
        typeof(BoldItalicTag),
        typeof(BoldTag),
        typeof(BulletTag),
        typeof(ColorTag),
        typeof(CommandLinkTag),
        typeof(FontTag),
        typeof(HeadingTag),
        typeof(ItalicTag),
    ];

    [Dependency] private readonly IClientAdminManager _admin = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public bool TryGetPrefix(ChatMessage message, out string prefix)
    {
        prefix = string.Empty;

        if (!CanAttachLinksToMessage(message) ||
            _player.LocalEntity is not { } localEntity)
        {
            return false;
        }

        if (TryGetEntity(message.SenderEntity, out var sender) &&
            sender is { } senderUid &&
            (localEntity == senderUid || !HasComp<MobStateComponent>(senderUid)))
        {
            return false;
        }

        var builder = new StringBuilder();

        if (HasComp<StationAiHeldComponent>(localEntity))
        {
            var timestamp = _timing.CurTime.TotalSeconds.ToString(CultureInfo.InvariantCulture);
            AppendLink(builder, "[CAM]", $"ai_track_entity {message.SenderEntity} {timestamp}");
        }

        if (CanUseAdminChatLinks(localEntity))
        {
            if (_admin.CanCommand("follow"))
                AppendLink(builder, "[FLW]", $"follow {message.SenderEntity}");

            if (_admin.CanCommand("playerpanel"))
                AppendLink(builder, "[PP]", $"playerpanel {message.SenderEntity}");
        }

        if (builder.Length == 0)
            return false;

        prefix = builder.ToString();
        return true;
    }

    private bool CanUseAdminChatLinks(EntityUid localEntity)
    {
        return _admin.IsActive() &&
               TryComp<GhostComponent>(localEntity, out var ghost) &&
               ghost.CanGhostInteract;
    }

    private static bool CanAttachLinksToMessage(ChatMessage message)
    {
        return message.SenderEntity != NetEntity.Invalid &&
               message.Channel is ChatChannel.Local or ChatChannel.Radio;
    }

    private static void AppendLink(StringBuilder builder, string label, string command)
    {
        if (builder.Length > 0)
            builder.Append(' ');

        builder.Append("[cmdlink=\"");
        builder.Append(label);
        builder.Append("\" command=\"");
        builder.Append(command);
        builder.Append("\" /]");
    }
}
