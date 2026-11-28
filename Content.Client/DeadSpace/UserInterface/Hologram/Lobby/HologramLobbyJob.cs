// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Lobby;

/// <summary>The job a profile wants most, for the "Dead Space" lobby portrait.</summary>
public static class HologramLobbyJob
{
    /// <summary>
    /// The job at the profile's highest priority. Ties go to the first job in the order of the character editor:
    /// departments by their UI order, jobs inside a department by the job weights. False when no job is above "never".
    /// </summary>
    public static bool TryGetTopJob(
        HumanoidCharacterProfile profile,
        IPrototypeManager prototypes,
        [NotNullWhen(true)] out JobPrototype? job)
    {
        job = null;

        var best = JobPriority.Never;
        foreach (var priority in profile.JobPriorities.Values)
        {
            if (priority > best)
                best = priority;
        }

        if (best == JobPriority.Never)
            return false;

        var departments = new List<DepartmentPrototype>();
        foreach (var department in prototypes.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (!department.EditorHidden)
                departments.Add(department);
        }

        departments.Sort(DepartmentUIComparer.Instance);
        JobUIComparer.TryCreate(prototypes, null, out var comparer);

        var jobs = new List<JobPrototype>();
        foreach (var department in departments)
        {
            jobs.Clear();
            foreach (var id in department.Roles)
            {
                if (prototypes.TryIndex(id, out var candidate) && candidate.SetPreference)
                    jobs.Add(candidate);
            }

            if (comparer != null)
                jobs.Sort(comparer);

            foreach (var candidate in jobs)
            {
                if (profile.JobPriorities.TryGetValue(candidate.ID, out var priority) && priority == best)
                {
                    job = candidate;
                    return true;
                }
            }
        }

        // A wanted job outside every listed department: take any of them rather than say "none".
        foreach (var (id, priority) in profile.JobPriorities)
        {
            if (priority == best && prototypes.TryIndex(id, out job))
                return true;
        }

        return false;
    }
}
