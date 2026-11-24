// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.NPC;
using Content.Server.NPC.HTN.Preconditions;

namespace Content.Server.DeadSpace.PlanetSurvey.Npc;

public sealed partial class SurveyRolePrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [DataField(required: true)]
    public List<SurveyRole> Roles = new();

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var role = blackboard.TryGetValue<SurveyRole>(SurveyKeys.Role, out var value, _entManager)
            ? value
            : SurveyRole.None;

        return Roles.Contains(role);
    }
}
