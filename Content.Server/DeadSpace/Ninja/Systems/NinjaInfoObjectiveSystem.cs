// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class NinjaInfoObjectiveSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaInfoConditionComponent, ObjectiveAssignedEvent>(OnObjectiveAssigned);
        SubscribeLocalEvent<NinjaInfoConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnObjectiveAssigned(Entity<NinjaInfoConditionComponent> ent, ref ObjectiveAssignedEvent args)
    {
        var comp = ent.Comp;

        comp.TargetJobs.Clear();
        comp.ScannedEntities.Clear();
        comp.CorrectScans = 0;
        comp.PriorityMind = null;

        var byJob = new Dictionary<ProtoId<JobPrototype>, List<EntityUid>>();

        var query = EntityQueryEnumerator<MindComponent>();
        while (query.MoveNext(out var mindId, out var mind))
        {
            if (mind.OwnedEntity is not { } body || !_mobState.IsAlive(body))
                continue;

            if (!_jobs.MindTryGetJobId(mindId, out var jobId) || jobId is not { } id)
                continue;

            if (!byJob.TryGetValue(id, out var minds))
                byJob[id] = minds = new List<EntityUid>();

            minds.Add(mindId);
        }

        if (byJob.Count < comp.JobCount)
        {
            args.Cancelled = true;
            return;
        }

        comp.TargetCount = GetTargetCount(byJob.Values.Sum(minds => minds.Count));

        var priority = _random.Pick(byJob.Values.SelectMany(minds => minds).ToList());
        if (!_jobs.MindTryGetJobId(priority, out var priorityJob) || priorityJob is not { } priorityJobId)
        {
            args.Cancelled = true;
            return;
        }

        comp.PriorityMind = priority;

        comp.TargetJobs.Add(priorityJobId);

        var pool = byJob.Keys.Where(job => job != priorityJobId).ToList();
        while (comp.TargetJobs.Count < comp.JobCount)
        {
            var next = _random.Pick(pool);
            pool.Remove(next);
            comp.TargetJobs.Add(next);
        }

        var jobNames = comp.TargetJobs
            .Select(job => _proto.TryIndex<JobPrototype>(job, out var jobProto) ? jobProto.LocalizedName : job.Id)
            .ToList();

        _metaData.SetEntityName(ent, Loc.GetString("objective-condition-ninja-scan-title"));
        _metaData.SetEntityDescription(ent, Loc.GetString(
            "ninja-info-objective-description",
            ("jobs", string.Join(", ", jobNames)),
            ("count", comp.TargetCount)));
    }

    private static int GetTargetCount(int playerCount)
    {
        return playerCount switch
        {
            < 20 => 3,
            < 40 => 4,
            < 60 => 5,
            _ => 6,
        };
    }

    private void OnGetProgress(Entity<NinjaInfoConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var comp = ent.Comp;
        if (comp.TargetCount <= 0)
        {
            args.Progress = 1f;
            return;
        }

        args.Progress = Math.Clamp((float)comp.CorrectScans / comp.TargetCount, 0f, 1f);
    }

    public NinjaInfoScanResult TryScanEntity(EntityUid scannedBody, EntityUid? actor)
    {
        if (!_mobState.IsAlive(scannedBody))
            return NinjaInfoScanResult.None;

        if (!_mind.TryGetMind(scannedBody, out var scannedMind, out _))
            return NinjaInfoScanResult.None;

        if (actor is not { } actorUid || !_mind.TryGetMind(actorUid, out _, out var actorMind))
            return NinjaInfoScanResult.None;

        _jobs.MindTryGetJobId(scannedMind, out var scannedJob);

        var result = NinjaInfoScanResult.None;

        foreach (var objective in actorMind.Objectives)
        {
            if (!TryComp<NinjaInfoConditionComponent>(objective, out var comp))
                continue;

            if (comp.ScannedEntities.Contains(scannedBody))
                continue;

            var isPriority = comp.PriorityMind is { } priority && scannedMind == priority;

            if (!isPriority && (scannedJob is not { } job || !comp.TargetJobs.Contains(job)))
                continue;

            comp.ScannedEntities.Add(scannedBody);
            comp.CorrectScans = isPriority ? comp.TargetCount : Math.Min(comp.CorrectScans + 1, comp.TargetCount);

            if (isPriority)
                result = NinjaInfoScanResult.Priority;
            else if (result == NinjaInfoScanResult.None)
                result = NinjaInfoScanResult.Job;
        }

        return result;
    }
}

public enum NinjaInfoScanResult : byte
{
    None,
    Job,
    Priority,
}
