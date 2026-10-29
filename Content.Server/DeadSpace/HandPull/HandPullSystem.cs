using System.Linq;
using System.Numerics;
using Content.Shared.DeadSpace.HandPull;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.HandPull;

public sealed class HandPullSystem : EntitySystem
{
    private static readonly TimeSpan OfferLifeTime = TimeSpan.FromSeconds(10);
    private const float FollowDistance = 0.75f;
    private const float FollowCorrection = 50f;
    private const float MaxFollowCorrection = 16f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<int, HandPullOffer> _offers = new();
    private readonly Dictionary<EntityUid, int> _targetOffers = new();
    private readonly Dictionary<EntityUid, HandPullFollow> _following = new();

    private int _nextRequestID;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeNetworkEvent<HandPullAnswerMessage>(OnAnswer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateOffers();
        UpdateFollowing(frameTime);
    }

    private void UpdateOffers()
    {
        if (_offers.Count == 0)
            return;

        var now = _timing.CurTime;

        foreach (var (requestID, offer) in _offers.ToArray())
        {
            if (offer.ExpiresAt > now)
                continue;

            CloseOffer(requestID);
            PopupTo(offer.User, "hand-pull-popup-expired", ("target", offer.TargetName));
        }
    }

    private void UpdateFollowing(float frameTime)
    {
        foreach (var (pullerUid, follow) in _following.ToArray())
        {
            if (!TryComp<PullerComponent>(pullerUid, out var puller) ||
                puller.Pulling != follow.Target ||
                !puller.PullingWithoutSpeedPenalty ||
                !TryComp<PhysicsComponent>(pullerUid, out var pullerPhysics) ||
                !TryComp<PhysicsComponent>(follow.Target, out var targetPhysics) ||
                !TryComp(pullerUid, out TransformComponent? pullerTransform) ||
                !TryComp(follow.Target, out TransformComponent? targetTransform))
            {
                _following.Remove(pullerUid);
                continue;
            }

            if (pullerTransform.MapID != targetTransform.MapID)
            {
                _following.Remove(pullerUid);

                if (TryComp<PullableComponent>(follow.Target, out var pullable))
                    _pulling.TryStopPull(follow.Target, pullable, pullerUid);

                continue;
            }

            var desiredPosition = _transform.GetWorldPosition(pullerTransform) + follow.Offset;
            var targetPosition = _transform.GetWorldPosition(targetTransform);
            var correction = (desiredPosition - targetPosition) * FollowCorrection;

            if (correction.LengthSquared() > MaxFollowCorrection * MaxFollowCorrection)
                correction = Vector2.Normalize(correction) * MaxFollowCorrection;

            _physics.SetLinearVelocity(
                follow.Target,
                pullerPhysics.LinearVelocity + correction,
                body: targetPhysics);
        }
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        var target = args.Target;

        if (!TryComp<PullableComponent>(target, out var pullable) ||
            args.User == target ||
            !args.CanInteract ||
            (!args.CanAccess &&
             !_interactionSystem.InRangeAndAccessible(
                 args.User,
                 target,
                 _pulling.GetPullRange(target, pullable))) ||
            !CanOfferHandPull(args.User, target, pullable))
        {
            return;
        }

        var user = args.User;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("hand-pull-verb"),
            Act = () => TryCreateOffer(user, target),
            DoContactInteraction = false,
        });
    }

    private void TryCreateOffer(EntityUid user, EntityUid target)
    {
        if (!TryComp<PullableComponent>(target, out var pullable) ||
            !CanOfferHandPull(user, target, pullable))
        {
            PopupTo(user, "hand-pull-popup-invalid");
            return;
        }

        if (!_player.TryGetSessionByEntity(target, out var targetSession))
        {
            PopupTo(user, "hand-pull-popup-invalid");
            return;
        }

        if (_targetOffers.TryGetValue(target, out var existingRequest))
            CloseOffer(existingRequest);

        var requestID = GetNextRequestID();
        var offer = new HandPullOffer(
            requestID,
            user,
            target,
            Identity.Name(user, EntityManager),
            Identity.Name(target, EntityManager),
            _timing.CurTime + OfferLifeTime);

        _offers[requestID] = offer;
        _targetOffers[target] = requestID;

        RaiseNetworkEvent(
            new HandPullOfferMessage(requestID, offer.UserName),
            targetSession);

        PopupTo(user, "hand-pull-popup-offer-sent", ("target", offer.TargetName));
    }

    private void OnAnswer(HandPullAnswerMessage msg, EntitySessionEventArgs args)
    {
        if (!_offers.TryGetValue(msg.RequestID, out var offer))
            return;

        if (args.SenderSession.AttachedEntity is not { } sender ||
            sender != offer.Target)
        {
            return;
        }

        RemoveOffer(msg.RequestID);

        if (_timing.CurTime >= offer.ExpiresAt)
        {
            PopupTo(offer.User, "hand-pull-popup-expired", ("target", offer.TargetName));
            return;
        }

        if (!msg.Accepted)
        {
            PopupTo(offer.User, "hand-pull-popup-declined", ("target", offer.TargetName));
            return;
        }

        if (!TryComp<PullableComponent>(offer.Target, out var pullable) ||
            !CanOfferHandPull(offer.User, offer.Target, pullable))
        {
            PopupTo(offer.User, "hand-pull-popup-invalid");
            PopupTo(offer.Target, "hand-pull-popup-invalid");
            return;
        }

        if (!_pulling.TryStartPull(offer.User, offer.Target, noSpeedPenalty: true))
        {
            PopupTo(offer.User, "hand-pull-popup-invalid");
            PopupTo(offer.Target, "hand-pull-popup-invalid");
            return;
        }

        if (!TryComp(offer.User, out TransformComponent? pullerTransform) ||
            !TryComp(offer.Target, out TransformComponent? targetTransform))
        {
            _pulling.TryStopPull(offer.Target, pullable, offer.User);
            return;
        }

        var separation =
            _transform.GetWorldPosition(targetTransform) -
            _transform.GetWorldPosition(pullerTransform);

        var offset = separation.LengthSquared() > 0.01f
            ? Vector2.Normalize(separation) * FollowDistance
            : new Vector2(0f, -FollowDistance);

        _following[offer.User] = new HandPullFollow(offer.Target, offset);

        _pulling.RemovePullJoint(offer.Target, pullable);
    }

    private bool CanOfferHandPull(EntityUid user, EntityUid target, PullableComponent pullable)
    {
        if (Deleted(user) ||
            Deleted(target) ||
            user == target ||
            !_player.TryGetSessionByEntity(user, out _) ||
            !_player.TryGetSessionByEntity(target, out _) ||
            _mobState.IsIncapacitated(user) ||
            _mobState.IsIncapacitated(target) ||
            !_interactionSystem.InRangeAndAccessible(
                user,
                target,
                _pulling.GetPullRange(target, pullable)))
        {
            return false;
        }

        return _pulling.CanPull(user, target);
    }

    private int GetNextRequestID()
    {
        do
        {
            _nextRequestID++;
        } while (_offers.ContainsKey(_nextRequestID));

        return _nextRequestID;
    }

    private void CloseOffer(int requestID)
    {
        if (!_offers.TryGetValue(requestID, out var offer))
            return;

        RemoveOffer(requestID);

        if (_player.TryGetSessionByEntity(offer.Target, out var targetSession))
            RaiseNetworkEvent(new HandPullOfferClosedMessage(requestID), targetSession);
    }

    private void RemoveOffer(int requestID)
    {
        if (!_offers.Remove(requestID, out var offer))
            return;

        if (_targetOffers.TryGetValue(offer.Target, out var targetRequest) &&
            targetRequest == requestID)
        {
            _targetOffers.Remove(offer.Target);
        }
    }

    private void PopupTo(EntityUid uid, string locID, params (string, object)[] args)
    {
        if (Deleted(uid))
            return;

        _popup.PopupEntity(Loc.GetString(locID, args), uid, uid);
    }

    private readonly record struct HandPullOffer(
        int RequestID,
        EntityUid User,
        EntityUid Target,
        string UserName,
        string TargetName,
        TimeSpan ExpiresAt);

    private readonly record struct HandPullFollow(
        EntityUid Target,
        Vector2 Offset);
}
