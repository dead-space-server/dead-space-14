using Content.Server.DeadSpace.Thief.Cartridges;
using Content.Server.DeadSpace.Thief.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;

namespace Content.Server.DeadSpace.Thief.Objectives.Systems;

/// <summary>
/// DS14: Progress of the thief's money objective is the share of the target amount
/// currently carried as physical dirty credits (dCR) somewhere in the thief's
/// inventory — exactly like steal objectives count items on person.
/// </summary>
public sealed class ThiefEarnConditionSystem : EntitySystem
{
    [Dependency] private readonly ThiefProgramSystem _thiefProgram = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThiefEarnConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, ThiefEarnConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        if (comp.Target <= 0)
        {
            args.Progress = 1f;
            return;
        }

        var owner = args.Mind.OwnedEntity;
        if (owner == null)
        {
            args.Progress = 0f;
            return;
        }

        // Only what the thief carries right now counts — dropping or spending dCR lowers the score.
        var carried = _thiefProgram.CountDirtyCredits(owner.Value);
        args.Progress = Math.Clamp((float) carried / comp.Target, 0f, 1f);
    }
}
