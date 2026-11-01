// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Necromorphs.Unitology.Components;

/// <summary>
/// Gives a head of the unitologists the pentagram ritual back (a recruit made a plain unitologist, not a slave by the
/// serum): the Circle's geist. The ritual itself is <see cref="UnitologyHeadSystem"/>'s.
/// </summary>
[RegisterComponent]
public sealed partial class UnitologyPentagramRecruiterComponent : Component
{
    [DataField]
    public EntProtoId Action = "ActionSelectTargetRecruitment";

    [ViewVariables]
    public EntityUid? ActionEntity;
}

/// <summary>
/// The Circle's geist sent on its own (not called by the unitologists): its obelisk summoning, used by a supermatter
/// obelisk, drives it to the end of its activation even with no unitologists' mode running.
/// </summary>
[RegisterComponent]
public sealed partial class TheCircleGeistSaboteurComponent : Component
{
    /// <summary>How close the supermatter obelisk must be.</summary>
    [DataField]
    public float SuperObeliskRange = 3f;
}
