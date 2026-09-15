// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.Roles.Jobs;

namespace Content.Shared.Mind.Filters;

/// <summary>
/// Removes minds whose job shares a department with the excluded mind.
/// </summary>
public sealed partial class DifferentDepartmentMindFilter : MindFilter
{
    protected override bool ShouldRemove(
        Entity<MindComponent> mind,
        EntityUid? exclude,
        IEntityManager entMan,
        SharedMindSystem mindSys)
    {
        if (exclude is not { } excludedMind)
            return false;

        var jobs = entMan.System<SharedJobSystem>();
        if (!jobs.MindTryGetJobId(excludedMind, out var excludedJob) ||
            !jobs.MindTryGetJobId(mind.Owner, out var targetJob) ||
            excludedJob is not { } excludedJobId ||
            targetJob is not { } targetJobId ||
            !jobs.TryGetAllDepartments(excludedJobId, out var excludedDepartments) ||
            !jobs.TryGetAllDepartments(targetJobId, out var targetDepartments))
        {
            return false;
        }

        return excludedDepartments
            .Select(department => department.ID)
            .Intersect(targetDepartments.Select(department => department.ID))
            .Any();
    }
}
