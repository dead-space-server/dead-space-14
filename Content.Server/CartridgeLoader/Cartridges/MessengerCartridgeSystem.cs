using System.Linq;
using Content.Shared.PDA;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.CartridgeLoader.Cartridges;
using Content.Shared.Popups;
using Content.Shared.Radio.Components;
using Content.Server.Popups;
using Content.Server.Power.Components;
using Content.Server.GameTicking;
using Robust.Server.GameObjects; // DS14
using Robust.Shared.Localization;

namespace Content.Server.CartridgeLoader.Cartridges;

public sealed partial class MessengerCartridgeSystem : EntitySystem
{
    [Dependency] private CartridgeLoaderSystem _cartridgeLoaderSystem = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private PopupSystem _popupSystem = default!;
    [Dependency] private UserInterfaceSystem _userInterfaceSystem = default!; // DS14

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<MessengerCartridgeComponent, CartridgeDeactivatedEvent>(OnDeactivated); // DS14
    }

    // DS14-start
    /// <summary>
    ///     When the NanoChat app is closed (PDA closed, another app opened, cartridge ejected),
    ///     the active chat is forgotten so new messages start raising unread badges again.
    /// </summary>
    private void OnDeactivated(Entity<MessengerCartridgeComponent> ent, ref CartridgeDeactivatedEvent args)
    {
        ent.Comp.ActiveChatPartnerId = null;
    }

    /// <summary>
    ///     Checks whether the recipient is actually looking at an open chat with the sender right now
    ///     (messenger app is active on screen with that chat open).
    /// </summary>
    private bool IsViewingChat(EntityUid cartridgeUid, MessengerCartridgeComponent component, int partnerId)
    {
        if (component.ActiveChatPartnerId != partnerId)
            return false;

        var loaderUid = GetLoaderUid(cartridgeUid);
        if (loaderUid == null || !TryComp<CartridgeLoaderComponent>(loaderUid.Value, out var loader))
            return false;

        return _userInterfaceSystem.IsUiOpen(loaderUid.Value, loader.UiKey)
            && loader.ActiveProgram == cartridgeUid;
    }
    // DS14-end

    /// <summary>
    /// Syncing client and server
    /// </summary>
    private void SyncUsers()
    {
        // Excluding users for later deletion
        var activeUserIdsByServer = new Dictionary<EntityUid, HashSet<int>>();

        var cartridgeQuery = EntityQueryEnumerator<MessengerCartridgeComponent>();
        while (cartridgeQuery.MoveNext(out var cartridgeUid, out _))
        {
            var userData = GetUserData(cartridgeUid);
            if (userData == null)
                continue;

            var server = GetServerForCartridge(cartridgeUid);
            if (server == null)
                continue;

            activeUserIdsByServer.TryAdd(server.Value.Uid, new HashSet<int>());
            activeUserIdsByServer[server.Value.Uid].Add(userData.Value.Id);

            SendUserData(cartridgeUid);
        }

        // Delete users
        foreach (var (serverUid, activeIds) in activeUserIdsByServer)
        {
            if (!TryComp(serverUid, out MessengerServerComponent? serverComponent))
                continue;

            var usersToRemove = serverComponent.Users.Keys
                .Where(id => !activeIds.Contains(id))
                .ToList();

            foreach (var id in usersToRemove)
            {
                serverComponent.Users.Remove(id);
            }

            if (usersToRemove.Count > 0)
            {
                Dirty(serverUid, serverComponent);
            }
        }
    }

    /// <summary>
    /// Find the Server
    /// </summary>
    private (EntityUid Uid, MessengerServerComponent Component)? GetServerForCartridge(EntityUid cartridgeUid)
    {
        if (!TryComp(cartridgeUid, out TransformComponent? transform))
            return null;

        var serverQuery = EntityQueryEnumerator<MessengerServerComponent, TransformComponent, ApcPowerReceiverComponent>();
        while (serverQuery.MoveNext(out var serverUid, out var serverComponent, out var serverTransform, out var power))
        {
            if (serverTransform.MapID != transform.MapID)
                continue;

            if (power.Powered)
                return (serverUid, serverComponent);
        }
        return null;
    }

    /// <summary>
    /// Sending user data to UserList on Server
    /// </summary>
    private void SendUserData(EntityUid cartridgeUid)
    {
        var userData = GetUserData(cartridgeUid);
        if (userData == null)
            return;

        var server = GetServerForCartridge(cartridgeUid);
        if (server == null)
            return;

        var userId = userData.Value.Id;
        var userName = userData.Value.Name;
        var jobIconId = userData.Value.JobIconId;
        var jobTitle = userData.Value.JobTitle;

        // Checking for data matches
        if (server.Value.Component.Users.TryGetValue(userId, out var existing) && existing.Name == userName && existing.JobIconId == jobIconId && existing.JobTitle == jobTitle)
            return;

        server.Value.Component.Users[userId] = new MessengerUser(userId, userName, jobIconId, jobTitle);
        Dirty(server.Value.Uid, server.Value.Component);
    }

    /// <summary>
    /// Getting user data from Server UserList
    /// </summary>
    private (Dictionary<int, MessengerUserEntry> Users, MessengerStatus Status) GetUserList(EntityUid cartridgeUid)
    {
        var server = GetServerForCartridge(cartridgeUid);
        if (server == null)
            return (new Dictionary<int, MessengerUserEntry>(), MessengerStatus.ConnectionLost);

        var userData = GetUserData(cartridgeUid);
        var currentUserId = userData?.Id;

        var userList = server.Value.Component.Users
            .Where(kv => kv.Key != currentUserId)
            .Select(kv =>
            {
                var unreadCount = 0;
                if (currentUserId.HasValue)
                {
                    kv.Value.UnreadCounts.TryGetValue(currentUserId.Value, out unreadCount);
                }
                return new MessengerUserEntry(kv.Value.Id, kv.Value.Name, kv.Value.JobIconId, kv.Value.JobTitle, unreadCount);
            })
            .OrderByDescending(u => u.UnreadCount)
            .ToList();

        return (userList.ToDictionary(u => u.Id), MessengerStatus.Connected);
    }

    /// <summary>
    /// Takes messages from server and sends them to client
    /// </summary>
    private List<MessengerMessageEntry> GetMessages(EntityUid cartridgeUid)
    {
        var server = GetServerForCartridge(cartridgeUid);
        if (server == null)
            return new List<MessengerMessageEntry>();

        var userData = GetUserData(cartridgeUid);
        var currentUserId = userData?.Id ?? 0;

        var messages = new List<MessengerMessageEntry>();
        foreach (var msg in server.Value.Component.Messages)
        {
            if (msg.SenderId != currentUserId && msg.ReceiverId != currentUserId)
                continue;

            var senderName = server.Value.Component.Users.TryGetValue(msg.SenderId, out var sender)
                ? sender.Name
                : Loc.GetString("generic-unknown");

            messages.Add(new MessengerMessageEntry(msg.Id, msg.Content, msg.Timestamp, msg.SenderId, msg.ReceiverId)
            {
                SenderName = senderName,
                IsIncoming = msg.SenderId != currentUserId
            });
        }

        return messages;
    }

    /// <summary>
    /// Processing messages from the client
    /// </summary>
    private void OnUiMessage(EntityUid uid, MessengerCartridgeComponent component, CartridgeMessageEvent args)
    {
        var userData = GetUserData(uid);
        if (userData == null)
            return;

        var server = GetServerForCartridge(uid);
        if (server == null)
            return;

        var loaderUid = GetLoaderUid(uid);
        if (loaderUid == null)
            return;

        if (args is MessengerSendMessageEvent sendMessage)
        {
            //DS14-start
            const int MaxMessageLength = 512;
            const int MaxHistoryCount = 200;
            const double CooldownSeconds = 0.5;

            if (!server.Value.Component.Users.ContainsKey(sendMessage.ReceiverId))
                return;

            if (IsBlocked(userData.Value.Id, sendMessage.ReceiverId))
                return;

            if (string.IsNullOrWhiteSpace(sendMessage.Content))
                return;

            var receiverCartridgeUid = GetCartridgeByUserId(sendMessage.ReceiverId);
            if (receiverCartridgeUid != null &&
                TryComp<MessengerCartridgeComponent>(receiverCartridgeUid.Value, out var receiverCartridge) &&
                receiverCartridge.IncomingMessagesDisabled)
            {
                return;
            }

            var content = sendMessage.Content.Trim();
            if (content.Length > MaxMessageLength)
                content = content[..MaxMessageLength];

            var now = _gameTicker.RoundDuration();
            if (component.LastMessageTime.TryGetValue(userData.Value.Id, out var lastTime)
                && (now - lastTime).TotalSeconds < CooldownSeconds)
                return;

            component.LastMessageTime[userData.Value.Id] = now;
            //DS14-end
            var messageId = server.Value.Component.Messages.Count > 0
                ? server.Value.Component.Messages.Max(m => m.Id) + 1
                : 1;

            var message = new MessengerMessage(
                messageId,
                userData.Value.Id,
                sendMessage.ReceiverId,
                //DS14-start
                content,
                now
                //DS14-end
            );

            server.Value.Component.Messages.Add(message);
            //DS14-start
            if (server.Value.Component.Messages.Count > MaxHistoryCount)
                server.Value.Component.Messages.RemoveAt(0);
            //DS14-end
            Dirty(server.Value.Uid, server.Value.Component);

            UpdateUiState(uid, loaderUid.Value);

            if (receiverCartridgeUid == null)
                return;
            //DS14-start
            var receiverServer = GetServerForCartridge(receiverCartridgeUid.Value);
            if (receiverServer == null || receiverServer.Value.Uid != server.Value.Uid)
                return;
            //DS14-end
            var receiverLoaderUid = GetLoaderUid(receiverCartridgeUid.Value);
            if (receiverLoaderUid == null)
                return;

            var receiverComp = Comp<MessengerCartridgeComponent>(receiverCartridgeUid.Value);
            // DS14-start: only skip the unread badge if the recipient is actually viewing this chat right now
            if (!IsViewingChat(receiverCartridgeUid.Value, receiverComp, userData.Value.Id))
            {
                if (server.Value.Component.Users.TryGetValue(sendMessage.ReceiverId, out var receiverUser))
                {
                    receiverUser.UnreadCounts[userData.Value.Id] =
                        receiverUser.UnreadCounts.GetValueOrDefault(userData.Value.Id, 0) + 1;
                    Dirty(server.Value.Uid, server.Value.Component);
                }
            }

            SendNotificationToUser(receiverCartridgeUid.Value, userData.Value.Name, content); //DS14
            UpdateUiState(receiverCartridgeUid.Value, receiverLoaderUid.Value);
        }

        if (args is MessengerRequestMessagesEvent requestMessages)
        {
            if (requestMessages.UserId == 0)
            {
                component.ActiveChatPartnerId = null;
            }
            else
            {
                component.ActiveChatPartnerId = requestMessages.UserId;
                if (server.Value.Component.Users.TryGetValue(requestMessages.UserId, out var chatUser))
                {
                    chatUser.UnreadCounts[userData.Value.Id] = 0;
                    Dirty(server.Value.Uid, server.Value.Component);
                }
            }
            UpdateUiState(uid, loaderUid.Value);
        }

        if (args is MessengerTypingEvent)
        {
            _popupSystem.PopupEntity(Loc.GetString("messenger-typing-popup"), uid, PopupType.Small);
        }

        // DS14-Start
        if (args is MessengerBlockUserEvent blockEvent)
        {
            if (blockEvent.Block)
                component.BlockedUsers.Add(blockEvent.TargetUserId);
            else
                component.BlockedUsers.Remove(blockEvent.TargetUserId);

            UpdateUiState(uid, loaderUid.Value);

            // Also update the target user's UI if they're chatting with us
            var targetCartridgeUid = GetCartridgeByUserId(blockEvent.TargetUserId);
            if (targetCartridgeUid != null)
            {
                var targetLoaderUid = GetLoaderUid(targetCartridgeUid.Value);
                if (targetLoaderUid != null)
                {
                    UpdateUiState(targetCartridgeUid.Value, targetLoaderUid.Value);
                }
            }
        }

        if (args is MessengerSetIncomingDisabledEvent incomingEvent)
        {
            component.IncomingMessagesDisabled = incomingEvent.Disabled;
            UpdateUiState(uid, loaderUid.Value);
        }

        if (args is MessengerDeleteMessageEvent deleteEvent)
        {
            var message = server.Value.Component.Messages.FirstOrDefault(message => message.Id == deleteEvent.MessageId);
            if (message == null)
                return;
            if (message.SenderId != userData.Value.Id)
                return;
            server.Value.Component.Messages.Remove(message);
            Dirty(server.Value.Uid, server.Value.Component);
            UpdateUiState(uid, loaderUid.Value);
            var receiverCartridge = GetCartridgeByUserId(message.ReceiverId);

            if (receiverCartridge == null)
                return;
            var receiverLoader = GetLoaderUid(receiverCartridge.Value);

            if (receiverLoader != null)
                UpdateUiState(receiverCartridge.Value, receiverLoader.Value);
        }
        // DS14-End
    }

    private void SendNotificationToUser(EntityUid cartridgeUid, string senderName, string messagePreview)
    {
        var loaderUid = GetLoaderUid(cartridgeUid);
        if (loaderUid == null)
            return;

        var title = Loc.GetString("messenger-notification-message", ("sender", senderName));
        _cartridgeLoaderSystem.SendNotification(loaderUid.Value, title, senderName + ": " + messagePreview);
    }


    private EntityUid? GetLoaderUid(EntityUid cartridgeUid)
    {
        if (!TryComp(cartridgeUid, out TransformComponent? transform))
            return null;

        return transform.ParentUid;
    }

    /// <summary>
    /// looking for the recipient's PDA
    /// </summary>
    private EntityUid? GetCartridgeByUserId(int userId)
    {
        var cartridgeQuery = EntityQueryEnumerator<MessengerCartridgeComponent>();
        while (cartridgeQuery.MoveNext(out var cartridgeUid, out _))
        {
            var userData = GetUserData(cartridgeUid);
            if (userData?.Id == userId)
                return cartridgeUid;
        }
        return null;
    }

    /// <summary>
    /// Getting user data from the IDcard
    /// </summary>
    public (int Id, string Name, string JobIconId, string JobTitle)? GetUserData(EntityUid cartridgeUid)
    {
        var pdaUid = GetLoaderUid(cartridgeUid);
        if (!TryComp<PdaComponent>(pdaUid, out var pda))
            return null;

        var idCardUid = pda.ContainedId;
        if (idCardUid == null)
            return null;

        if (!TryComp<IdCardComponent>(idCardUid, out var idCard))
            return null;

        var fullName = string.IsNullOrEmpty(idCard.FullName) ? Loc.GetString("generic-unknown") : idCard.FullName;
        var jobTitle = string.IsNullOrEmpty(idCard.LocalizedJobTitle) ? Loc.GetString("job-name-unknown") : idCard.LocalizedJobTitle;
        var id = (int)idCardUid.Value;
        return (id, fullName, idCard.JobIcon, jobTitle);
    }

    private void OnUiReady(EntityUid uid, MessengerCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader);
    }

    // DS14-Start
    private bool IsBlocked(int senderId, int receiverId)
    {
        var senderCartridge = GetCartridgeByUserId(senderId);
        if (senderCartridge != null && TryComp<MessengerCartridgeComponent>(senderCartridge, out var senderComp)
            && senderComp.BlockedUsers.Contains(receiverId))
            return true;

        var receiverCartridge = GetCartridgeByUserId(receiverId);
        if (receiverCartridge != null && TryComp<MessengerCartridgeComponent>(receiverCartridge, out var receiverComp)
            && receiverComp.BlockedUsers.Contains(senderId))
            return true;

        return false;
    }
    // DS14-End

    private void UpdateUiState(EntityUid cartridgeUid, EntityUid loaderUid)
    {
        // checking for an IDcard
        if (!TryComp<PdaComponent>(loaderUid, out var pda) || pda.ContainedId == null)
        {
            var lostState = new MessengerCartridgeUiState(MessengerStatus.ConnectionLost, new Dictionary<int, MessengerUserEntry>(), new List<MessengerMessageEntry>());
            _cartridgeLoaderSystem.UpdateCartridgeUiState(loaderUid, lostState);
            return;
        }

        SyncUsers();
        var (users, status) = GetUserList(cartridgeUid);
        var messages = GetMessages(cartridgeUid);

        // DS14-Start
        var userData = GetUserData(cartridgeUid);
        var isBlocked = false;
        var isBlocking = false;
        TryComp<MessengerCartridgeComponent>(cartridgeUid, out var cartridgeComp); // DS14
        var incomingMessagesDisabled = cartridgeComp?.IncomingMessagesDisabled ?? false;
        if (userData.HasValue && cartridgeComp != null
            && cartridgeComp.ActiveChatPartnerId != null)
        {
            isBlocked = IsBlocked(cartridgeComp.ActiveChatPartnerId.Value, userData.Value.Id);
            isBlocking = IsBlocked(userData.Value.Id, cartridgeComp.ActiveChatPartnerId.Value);
        }
        // DS14-End

        var state = new MessengerCartridgeUiState(status, users, messages, isBlocked, isBlocking, incomingMessagesDisabled); //DS14
        _cartridgeLoaderSystem.UpdateCartridgeUiState(loaderUid, state);
    }
}
