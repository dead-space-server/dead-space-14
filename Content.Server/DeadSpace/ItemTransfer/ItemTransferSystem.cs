using Content.Shared.Administration.Logs;
using Content.Shared.ActionBlocker;
using Content.Shared.Database;
using Content.Shared.DeadSpace.ItemTransfer;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Content.Shared.Inventory.VirtualItem;

namespace Content.Server.DeadSpace.ItemTransfer;

public sealed class ItemTransferSystem : EntitySystem
{
    private static readonly TimeSpan OfferLifetime = TimeSpan.FromSeconds(10);

    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;

    private readonly Dictionary<int, ItemTransferOffer> _offers = new();
    private readonly Dictionary<EntityUid, int> _targetOffers = new();
    private int _nextRequestId;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<ItemTransferAnswerMessage>(OnAnswer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_offers.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (requestId, offer) in _offers.ToArray())
        {
            if (offer.ExpiresAt > now)
                continue;

            CloseOffer(requestId);
            PopupTo(offer.User, "item-transfer-popup-expired", ("target", offer.TargetName));
        }
    }

    private void OnGetVerbs(Entity<HandsComponent> target, ref GetVerbsEvent<Verb> args)
    {
        if (args.Using is not { } item ||
            args.User == args.Target ||
            !args.CanInteract ||
            !args.CanAccess ||
            IsClientSide(args.Target))
            return;

        if (!TryValidateTransfer(args.User, args.Target, item, out _, out _, out _))
            return;

        var user = args.User;
        var targetUid = args.Target;
        var itemUid = item;

        var verb = new Verb
        {
            Text = Loc.GetString("item-transfer-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/insert.svg.192dpi.png")),
            Act = () => TryCreateOffer(user, targetUid, itemUid),
            DoContactInteraction = false,
            Impact = LogImpact.Low,
        };

        args.Verbs.Add(verb);
    }

    private void TryCreateOffer(EntityUid user, EntityUid target, EntityUid item)
    {
        if (!TryValidateTransfer(user, target, item, out _, out _, out _))
        {
            PopupTo(user, "item-transfer-popup-invalid");
            return;
        }

        if (!_player.TryGetSessionByEntity(target, out var targetSession))
            return;

        if (_targetOffers.TryGetValue(target, out var existingRequest))
            CloseOffer(existingRequest);

        var requestId = GetNextRequestId();
        var offer = new ItemTransferOffer(
            requestId,
            user,
            target,
            item,
            Identity.Name(user, EntityManager),
            Identity.Name(target, EntityManager),
            Identity.Name(item, EntityManager),
            _timing.CurTime + OfferLifetime);

        _offers[requestId] = offer;
        _targetOffers[target] = requestId;

        RaiseNetworkEvent(new ItemTransferOfferMessage(
            requestId,
            offer.UserName,
            offer.ItemName), targetSession);

        PopupTo(user,
            "item-transfer-popup-offer-sent",
            ("target", offer.TargetName),
            ("item", offer.ItemName));
    }

    private void OnAnswer(ItemTransferAnswerMessage message, EntitySessionEventArgs args)
    {
        if (!_offers.TryGetValue(message.RequestId, out var offer))
            return;

        if (args.SenderSession.AttachedEntity is not { } sender ||
            sender != offer.Target)
            return;

        RemoveOffer(message.RequestId);

        if (!message.Accepted)
        {
            PopupTo(offer.User,
                "item-transfer-popup-declined",
                ("target", offer.TargetName),
                ("item", offer.ItemName));
            return;
        }

        if (!TryValidateTransfer(offer.User, offer.Target, offer.Item, out var userHands, out var targetHands, out var targetHand))
        {
            PopupTransferInvalid(offer.User, offer.Target);
            return;
        }

        if (!_hands.TryDrop((offer.User, userHands), offer.Item))
        {
            PopupTransferInvalid(offer.User, offer.Target);
            return;
        }

        if (!_hands.TryPickup(offer.Target, offer.Item, targetHand, handsComp: targetHands))
        {
            _hands.PickupOrDrop(offer.User, offer.Item, handsComp: userHands);
            PopupTransferInvalid(offer.User, offer.Target);
            return;
        }

        _popup.PopupEntity(
            Loc.GetString("item-transfer-popup-success-user",
                ("target", offer.TargetName),
                ("item", offer.ItemName)),
            offer.User,
            offer.User);

        _popup.PopupEntity(
            Loc.GetString("item-transfer-popup-success-target",
                ("user", offer.UserName),
                ("item", offer.ItemName)),
            offer.Target,
            offer.Target);

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(offer.User):user} transferred {ToPrettyString(offer.Item):item} to {ToPrettyString(offer.Target):target}");
    }

    private bool TryValidateTransfer(
        EntityUid user,
        EntityUid target,
        EntityUid item,
        out HandsComponent userHands,
        out HandsComponent targetHands,
        out string targetHand)
    {
        userHands = default!;
        targetHands = default!;
        targetHand = string.Empty;

        if (Deleted(user) ||
            Deleted(target) ||
            Deleted(item) ||
            user == target)
            return false;

        if (!_interaction.InRangeAndAccessible(user, target))
            return false;

        if (!_actionBlocker.CanInteract(user, target))
            return false;

        if (!_player.TryGetSessionByEntity(user, out _))
            return false;

        if (!_player.TryGetSessionByEntity(target, out _))
            return false;

        if (_mobState.IsIncapacitated(target))
            return false;

        if (!TryComp(user, out HandsComponent? userHandsComp) ||
            !_hands.TryGetActiveItem((user, userHandsComp), out var activeItem) ||
            activeItem != item ||
            !_hands.CanDrop((user, userHandsComp), item))
            return false;

        if (!TryComp(target, out HandsComponent? targetHandsComp) ||
            !_hands.TryGetEmptyHand((target, targetHandsComp), out var emptyHand))
            return false;

        if (!HasComp<ItemComponent>(item) || HasComp<VirtualItemComponent>(item) ||
            !_hands.CanPickupToHand(target, item, emptyHand, handsComp: targetHandsComp))
            return false;

        userHands = userHandsComp;
        targetHands = targetHandsComp;
        targetHand = emptyHand;
        return true;
    }

    private int GetNextRequestId()
    {
        do
        {
            _nextRequestId++;
        } while (_offers.ContainsKey(_nextRequestId));

        return _nextRequestId;
    }

    private void CloseOffer(int requestId)
    {
        if (!_offers.TryGetValue(requestId, out var offer))
            return;

        RemoveOffer(requestId);

        if (_player.TryGetSessionByEntity(offer.Target, out var targetSession))
            RaiseNetworkEvent(new ItemTransferOfferClosedMessage(requestId), targetSession);
    }

    private void RemoveOffer(int requestId)
    {
        if (!_offers.Remove(requestId, out var offer))
            return;

        if (_targetOffers.TryGetValue(offer.Target, out var targetRequest) &&
            targetRequest == requestId)
            _targetOffers.Remove(offer.Target);
    }

    private void PopupTransferInvalid(EntityUid user, EntityUid target)
    {
        PopupTo(user, "item-transfer-popup-invalid");
        PopupTo(target, "item-transfer-popup-invalid");
    }

    private void PopupTo(EntityUid uid, string locId, params (string, object)[] args)
    {
        if (Deleted(uid))
            return;

        _popup.PopupEntity(Loc.GetString(locId, args), uid, uid);
    }

    private readonly record struct ItemTransferOffer(
        int RequestId,
        EntityUid User,
        EntityUid Target,
        EntityUid Item,
        string UserName,
        string TargetName,
        string ItemName,
        TimeSpan ExpiresAt);
}
