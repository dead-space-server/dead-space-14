// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using CVars = Content.Shared.DeadSpace.CCCCVars.CCCCVars;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Shared.DeadSpace.Psychiatry;

public abstract class SharedPsychiatrySystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public const string SpecialPillReagentId = "Schizotoxin";
    public const string ClarityReagentId = "NeuroClarity";
    public const string PositronicSpecies = "IPC";

    public bool IsPositronic(EntityUid uid)
    {
        return TryComp<HumanoidAppearanceComponent>(uid, out var humanoid)
               && humanoid.Species == PositronicSpecies;
    }

    public bool IsAntagImmune(EntityUid uid, bool pillForced, bool gas = false)
    {
        var mode = (PsychiatryAntagImmunity) _cfg.GetCVar(CVars.PsychiatryAntagImmunityMode);
        if (!Enum.IsDefined(mode))
            mode = PsychiatryAntagImmunity.Partial;
        if (mode == PsychiatryAntagImmunity.None)
            return false;
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return false;
        if (!_roles.MindIsAntagonist(mindId))
            return false;
        if (mode == PsychiatryAntagImmunity.Full)
            return true;

        return !pillForced && !gas;
    }

    public static int CourseTablets(SchizophreniaStage stage) => stage switch
    {
        SchizophreniaStage.Incipient => 1,
        SchizophreniaStage.Latent => 1,
        SchizophreniaStage.Simple => 2,
        SchizophreniaStage.Acute => 3,
        _ => 1,
    };

    public static SchizophreniaStage ClampStage(int stage) =>
        (SchizophreniaStage) Math.Clamp(stage, 0, (int) SchizophreniaStage.Acute);

    public static SchizophreniaStage LowerStage(SchizophreniaStage current, int by)
    {
        var next = (int) current - by;
        return next <= 0 ? SchizophreniaStage.None : ClampStage(next);
    }
}
