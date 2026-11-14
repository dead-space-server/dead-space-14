using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.DeadSpace.HandPull;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.HandPull;

public sealed class HandPullOfferSystem : EntitySystem
{
    private static readonly TimeSpan OfferLifetime = TimeSpan.FromSeconds(10);

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly HandPullSystem _handPull = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly Dictionary<EntityUid, HandPullOffer> _offers = new();

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActorComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<HandPullAnswerMessage>(OnAnswer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_offers.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (target, offer) in _offers)
        {
            if (offer.ExpiresAt <= now)
                _expired.Add(target);
        }

        foreach (var target in _expired)
        {
            if (!_offers.TryGetValue(target, out var offer))
                continue;

            CloseOffer(target);
            PopupTo(offer.User, "hand-pull-popup-expired", ("target", Identity.Entity(target, EntityManager)));
        }

        _expired.Clear();
    }

    private void OnGetVerbs(Entity<ActorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract ||
            !args.CanAccess && !_interaction.InRangeAndAccessible(args.User, args.Target, _pulling.GetPullRange(args.Target)))
        {
            return;
        }

        var user = args.User;
        var target = args.Target;

        if (_handPull.IsHandPulling(user, target) || !CanOffer(user, target))
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("hand-pull-verb"),
            Act = () => TryCreateOffer(user, target),
            DoContactInteraction = false,
            Impact = LogImpact.Low,
        });
    }

    private bool CanOffer(EntityUid user, EntityUid target)
    {
        return !Deleted(user) &&
               !Deleted(target) &&
               HasComp<ActorComponent>(user) &&
               HasComp<ActorComponent>(target) &&
               _interaction.InRangeAndAccessible(user, target, _pulling.GetPullRange(target)) &&
               _handPull.CanHandPull(user, target);
    }

    private void TryCreateOffer(EntityUid user, EntityUid target)
    {
        if (!CanOffer(user, target) ||
            !TryComp<ActorComponent>(target, out var actor))
        {
            PopupTo(user, "hand-pull-popup-invalid");
            return;
        }

        if (_offers.TryGetValue(target, out var existing))
        {
            if (existing.User == user)
            {
                PopupTo(user, "hand-pull-popup-already-offered");
                return;
            }

            CloseOffer(target);
        }

        _offers[target] = new HandPullOffer(user, _timing.CurTime + OfferLifetime);

        RaiseNetworkEvent(
            new HandPullOfferMessage(GetNetEntity(user), Identity.Name(user, EntityManager, target)),
            actor.PlayerSession);

        PopupTo(user, "hand-pull-popup-offer-sent", ("target", Identity.Entity(target, EntityManager)));
    }

    private void OnAnswer(HandPullAnswerMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } target ||
            !_offers.TryGetValue(target, out var offer) ||
            GetEntity(msg.User) != offer.User)
        {
            return;
        }

        _offers.Remove(target);
        var user = offer.User;

        if (Deleted(user))
            return;

        if (!msg.Accepted)
        {
            PopupTo(user, "hand-pull-popup-declined", ("target", Identity.Entity(target, EntityManager)));
            return;
        }

        if (!CanOffer(user, target) || !_handPull.TryStart(user, target))
        {
            PopupTo(user, "hand-pull-popup-invalid");
            PopupTo(target, "hand-pull-popup-invalid");
            return;
        }

        PopupTo(user, "hand-pull-popup-success-user", ("target", Identity.Entity(target, EntityManager)));
        PopupTo(target, "hand-pull-popup-success-target", ("user", Identity.Entity(user, EntityManager)));

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):user} took {ToPrettyString(target):target} by the hand");
    }

    private void CloseOffer(EntityUid target)
    {
        if (!_offers.Remove(target, out var offer))
            return;

        if (TryComp<ActorComponent>(target, out var actor))
            RaiseNetworkEvent(new HandPullOfferClosedMessage(GetNetEntity(offer.User)), actor.PlayerSession);
    }

    private void PopupTo(EntityUid uid, string locId, params (string, object)[] args)
    {
        if (Deleted(uid))
            return;

        _popup.PopupEntity(Loc.GetString(locId, args), uid, uid);
    }

    private readonly record struct HandPullOffer(EntityUid User, TimeSpan ExpiresAt);
}
