using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// Stores data for <see cref="ThiefRuleSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(ThiefRuleSystem), typeof(Content.Server.DeadSpace.Thief.Cartridges.ThiefProgramSystem))]
public sealed partial class ThiefRuleComponent : Component
{
    /// <summary>
    /// DS14: A single tool, picked randomly once per round from the utility tool belt,
    /// that the thief must insert into their PDA's tool slot to unlock ВорПРО.
    /// </summary>
    [DataField]
    public EntProtoId? UnlockTool;
}
