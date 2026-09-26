using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Thief;

/// <summary>
/// DS14: Serializable data about a single ВорПРО request, sent to the client as part of the ui state.
/// </summary>
[Serializable, NetSerializable]
public sealed record ThiefRequestInfo(
    int Id,
    string GroupId,
    string GroupName,
    int Count,
    int PricePerItem,
    int TotalPrice,
    int TimeLimitMinutes,
    int TimeLeftSeconds)
{
    public const int Expired = -1;
}

/// <summary>
/// DS14: Ui state of the ВорПРО program.
/// </summary>
[Serializable, NetSerializable]
public sealed class ThiefProgramUiState : BoundUserInterfaceState
{
    /// <summary>Dirty credits currently carried by the user.</summary>
    public int Balance;

    /// <summary>Total dirty credits earned this round.</summary>
    public int EarnedTotal;

    /// <summary>The round goal (dirty credits to earn).</summary>
    public int GoalTarget;

    /// <summary>Requests available for accepting.</summary>
    public List<ThiefRequestInfo> Offers = new();

    /// <summary>Requests already accepted and awaiting delivery.</summary>
    public List<ThiefRequestInfo> Active = new();

    /// <summary>Whether a linked thief beacon exists for this user.</summary>
    public bool BeaconLinked;
}

[Serializable, NetSerializable]
public enum ThiefProgramUiAction
{
    /// <summary>Take an offer into work. RequestId required.</summary>
    Accept,

    /// <summary>Discard an active request without reward. RequestId required.</summary>
    Decline,

    /// <summary>Sell an active request at a linked beacon. RequestId required.</summary>
    Sell,

    /// <summary>Buy a listing from the uplink tab. ListingId required.</summary>
    Buy,

    /// <summary>Exchange dirty credits to regular ones 1:1. Amount required.</summary>
    Exchange,
}

/// <summary>
/// DS14: Ui message sent by the ВорПРО program fragment.
/// </summary>
[Serializable, NetSerializable]
public sealed class ThiefProgramUiMessageEvent : CartridgeMessageEvent
{
    public readonly ThiefProgramUiAction Action;
    public readonly int RequestId;
    public readonly string ListingId;
    public readonly int Amount;

    public ThiefProgramUiMessageEvent(ThiefProgramUiAction action, int requestId = 0, string listingId = "", int amount = 0)
    {
        Action = action;
        RequestId = requestId;
        ListingId = listingId;
        Amount = amount;
    }
}
