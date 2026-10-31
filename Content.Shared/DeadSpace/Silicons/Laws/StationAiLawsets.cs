using Content.Shared.Silicons.Laws;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Silicons.Laws;

// DS14-start
/// <summary>
/// Lawsets a player may pick for the station AI before the round starts.
/// </summary>
public static class StationAiLawsets
{
    public static readonly ProtoId<SiliconLawsetPrototype>[] Available =
    {
        "CrewsimovAi",
        "Corporate",
        "NTDefault",
        "CommandmentsLawset",
        "PaladinLawset",
        "EfficiencyLawset",
        "GameMasterLawset",
        "PainterLawset",
        "NutimovLawset",
        "ServantLawset",
        "RobocopLawset",
    };
}
// DS14-end
