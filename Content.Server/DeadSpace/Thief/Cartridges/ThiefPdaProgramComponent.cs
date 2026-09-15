using Content.Shared.DeadSpace.Thief.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Thief.Cartridges;

/// <summary>
/// DS14: A single request (order) of the ВорПРО program.
/// </summary>
public sealed class ThiefProgramRequest
{
    public int Id;

    /// <summary>The id of the thief request group prototype this request was generated from.</summary>
    public ProtoId<ThiefRequestGroupPrototype> Group = default!;

    /// <summary>The steal target group id items must match to be sold (StealTargetComponent.StealGroup).</summary>
    public string StealGroup = string.Empty;

    public int Count;

    public int PricePerItem;

    public int TimeLimitMinutes;

    /// <summary>Absolute round time when the delivery window closes. Only meaningful for accepted requests.</summary>
    public TimeSpan Deadline;
}

/// <summary>
/// DS14: Ворпро внутри кпк вора. я хз как по другому.
/// </summary>
[RegisterComponent]
[Access(typeof(ThiefProgramSystem))]
public sealed partial class ThiefPdaProgramComponent : Component
{
    /// <summary>Сколько запросов показывается одновременно.</summary>
    [DataField]
    public int MaxOffers = 4;

    /// <summary>Сколько можно принять ОДНОВРЕМЕННО.</summary>
    [DataField]
    public int MaxActiveRequests = 3;

    /// <summary>Попал в дедлайн? Получаешь те=же бабки умноженные на число снизу.</summary>
    [DataField]
    public float InTimeBonusMultiplier = 1.15f;

    /// <summary>Опоздал на дедлайн? Получаешь те-же бабки, умноженные на число снизу.</summary>
    [DataField]
    public float LatePenaltyMultiplier = 0.85f;

    /// <summary>Сколько над заработать. Надо перенести</summary>
    [DataField]
    public int GoalTarget = 1000000;

    public int NextRequestId;

    public readonly List<ThiefProgramRequest> Offers = new();

    public readonly List<ThiefProgramRequest> ActiveRequests = new();
}
