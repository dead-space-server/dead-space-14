using Content.Shared.Antag;
using Content.Shared.Cloning;
using Content.Shared.GameTicking.Components;
using Content.Shared.GameTicking.Rules.Components;
using Content.Shared.Gibbing.Components;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Random;

namespace Content.Shared.GameTicking.Rules;

public sealed partial class ParadoxCloneRuleSystem : GameRuleSystem<ParadoxCloneRuleComponent>
{
<<<<<<< ours
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CloningSystem _cloning = default!;
    [Dependency] private readonly SuitSensorSystem _sensor = default!;
    // DS14-start
    internal const int MaxCloneTargetAttempts = 3;

    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    // DS14-end
||||||| base
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CloningSystem _cloning = default!;
    [Dependency] private readonly SuitSensorSystem _sensor = default!;
=======
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCloningSystem _cloning = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedSuitSensorSystem _sensor = default!;
    [Dependency] private AliveHumanoidTargetSystem _target = default!;
>>>>>>> theirs

    protected override void Started(Entity<ParadoxCloneRuleComponent, GameRuleComponent> rule, ref GameRuleStartedEvent args)
    {
        base.Started(rule, ref args);

        // DS14-start
        if (component.OriginalBody is { } originalBody)
        {
            if (TryGetValidCloneTarget(originalBody, out _, uid))
                return;

            Log.Info("The overridden paradox clone target is not a valid station employee. A different target will be selected when the role is claimed.");
        }

        // check if we got enough potential cloning targets, otherwise cancel the gamerule so that the ghost role does not show up
<<<<<<< ours
        var allHumans = GetValidCloneTargets(uid);
||||||| base
        var allHumans = _mind.GetAliveHumans();
=======
        var allHumans = _target.GetMinds();
>>>>>>> theirs

        if (allHumans.Count == 0)
        {
<<<<<<< ours
            Log.Info("Could not find any valid alive station employees to create a paradox clone from! Ending gamerule.");
            ForceEndSelf(uid, gameRule);
||||||| base
            Log.Info("Could not find any alive players to create a paradox clone from! Ending gamerule.");
            ForceEndSelf(uid, gameRule);
=======
            Log.Info("Could not find any alive players to create a paradox clone from! Ending gamerule.");
            ForceEndSelf((rule, rule.Comp2));
>>>>>>> theirs
        }
        // DS14-end
    }

    // we have to do the spawning here so we can transfer the mind to the correct entity and can assign the objectives correctly
    [SubscribeLocalEvent]
    private void OnAntagSelectEntity(Entity<ParadoxCloneRuleComponent> ent, ref AntagSelectEntityEvent args)
    {
<<<<<<< ours
        if (args.Session?.AttachedEntity is not { } spawner)
            return;

        // DS14-start
        if (!TryCreateClone(ent, spawner, out var clone, out var attempts) || clone is not { } cloneUid)
||||||| base
        if (args.Session?.AttachedEntity is not { } spawner)
            return;

        if (ent.Comp.OriginalBody != null) // target was overridden, for example by admin antag control
        {
            if (Deleted(ent.Comp.OriginalBody.Value) || !_mind.TryGetMind(ent.Comp.OriginalBody.Value, out var originalMindId, out var _))
            {
                Log.Warning("Could not find mind of target player to paradox clone!");
                return;
            }
            ent.Comp.OriginalMind = originalMindId;
        }
        else
        {
            // get possible targets
            var allAliveHumanoids = _mind.GetAliveHumans();

            // we already checked when starting the gamerule, but someone might have died since then.
            if (allAliveHumanoids.Count == 0)
            {
                Log.Warning("Could not find any alive players to create a paradox clone from!");
                return;
            }

            // pick a random player
            var randomHumanoidMind = _random.Pick(allAliveHumanoids);
            ent.Comp.OriginalMind = randomHumanoidMind;
            ent.Comp.OriginalBody = randomHumanoidMind.Comp.OwnedEntity;

        }

        if (ent.Comp.OriginalBody == null || !_cloning.TryCloning(ent.Comp.OriginalBody.Value, _transform.GetMapCoordinates(spawner), ent.Comp.Settings, out var clone))
=======
        if (ent.Comp.OriginalBody != null) // target was overridden, for example by admin antag control
        {
            if (Deleted(ent.Comp.OriginalBody.Value) || !_mind.TryGetMind(ent.Comp.OriginalBody.Value, out var originalMindId, out var _))
            {
                Log.Warning("Could not find mind of target player to paradox clone!");
                return;
            }
            ent.Comp.OriginalMind = originalMindId;
        }
        else
        {
            // get possible targets
            var allAliveHumanoids = _target.GetMinds();

            // we already checked when starting the gamerule, but someone might have died since then.
            if (allAliveHumanoids.Count == 0)
            {
                Log.Warning("Could not find any alive players to create a paradox clone from!");
                return;
            }

            // pick a random player
            var randomHumanoidMind = _random.Pick(allAliveHumanoids);
            ent.Comp.OriginalMind = randomHumanoidMind;
            ent.Comp.OriginalBody = randomHumanoidMind.Comp.OwnedEntity;

        }

        if (ent.Comp.OriginalBody == null || !_cloning.TryCloning(ent.Comp.OriginalBody.Value, args.Coords, ent.Comp.Settings, out var clone))
>>>>>>> theirs
        {
            Log.Error($"Unable to make a paradox clone after {attempts} of {MaxCloneTargetAttempts} target attempts. Ending gamerule.");
            ent.Comp.OriginalMind = null;
            ent.Comp.OriginalBody = null;
            ForceEndSelf(ent.Owner);
            return;
        }
        // DS14-end

        var targetComp = EnsureComp<TargetOverrideComponent>(cloneUid);
        targetComp.Target = ent.Comp.OriginalMind; // set the kill target

        var gibComp = EnsureComp<GibOnRoundEndComponent>(cloneUid);
        gibComp.SpawnProto = ent.Comp.GibProto;
        gibComp.PreventGibbingObjectives = new() { "ParadoxCloneKillObjective" }; // don't gib them if they killed the original.

        // turn their suit sensors off so they don't immediately get noticed
        _sensor.SetAllSensors(cloneUid, SuitSensorMode.SensorOff);

        args.Entity = cloneUid;
    }

    // DS14-start
    internal bool TryCreateClone(
        Entity<ParadoxCloneRuleComponent> ent,
        EntityUid spawner,
        out EntityUid? clone,
        out int attempts)
    {
        var attemptedMinds = new HashSet<EntityUid>();
        clone = null;
        attempts = 0;

        for (var attempt = 1; attempt <= MaxCloneTargetAttempts; attempt++)
        {
            Entity<MindComponent> originalMind;

            // The first attempt respects an admin override, but it is still subject to the same station-job check.
            if (attempt == 1 && ent.Comp.OriginalBody is { } overriddenBody)
            {
                attempts++;
                if (!TryGetValidCloneTarget(overriddenBody, out originalMind, ent.Owner))
                {
                    Log.Warning("The overridden paradox clone target is no longer a valid station employee. Retrying with a different target.");
                    ent.Comp.OriginalMind = null;
                    ent.Comp.OriginalBody = null;
                    continue;
                }
            }
            else if (!TryPickRandomCloneTarget(attemptedMinds, out originalMind, ent.Owner))
            {
                break;
            }
            else
            {
                attempts++;
            }

            attemptedMinds.Add(originalMind.Owner);
            if (originalMind.Comp.OwnedEntity is not { } originalBody)
            {
                ent.Comp.OriginalMind = null;
                ent.Comp.OriginalBody = null;
                continue;
            }

            ent.Comp.OriginalMind = originalMind.Owner;
            ent.Comp.OriginalBody = originalBody;

            if (_cloning.TryCloning(originalBody, _transform.GetMapCoordinates(spawner), ent.Comp.Settings, out clone))
                break;

            Log.Warning($"Unable to make a paradox clone of entity {ToPrettyString(originalBody)} on attempt {attempt} of {MaxCloneTargetAttempts}. Retrying with a different target.");
            ent.Comp.OriginalMind = null;
            ent.Comp.OriginalBody = null;
        }

        return clone != null;
    }
    // DS14-end

    [SubscribeLocalEvent]
    private void AfterAntagEntitySelected(Entity<ParadoxCloneRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (ent.Comp.OriginalMind == null)
            return;

        if (!_mind.TryGetMind(args.EntityUid, out var cloneMindId, out var cloneMindComp))
            return;

        _mind.CopyObjectives(ent.Comp.OriginalMind.Value, (cloneMindId, cloneMindComp), ent.Comp.ObjectiveWhitelist, ent.Comp.ObjectiveBlacklist);
    }

    // DS14-start
    private HashSet<Entity<MindComponent>> GetValidCloneTargets(EntityUid? rule = null)
    {
        var allHumans = _mind.GetAliveHumans();
        allHumans.RemoveWhere(mind => !IsValidCloneTarget(mind, rule));
        return allHumans;
    }

    private bool TryGetValidCloneTarget(EntityUid body, out Entity<MindComponent> mind, EntityUid? rule = null)
    {
        foreach (var candidate in GetValidCloneTargets(rule))
        {
            if (candidate.Comp.OwnedEntity != body)
                continue;

            mind = candidate;
            return true;
        }

        mind = default;
        return false;
    }

    private bool TryPickRandomCloneTarget(HashSet<EntityUid> attemptedMinds, out Entity<MindComponent> mind, EntityUid? rule = null)
    {
        var candidates = GetValidCloneTargets(rule);
        candidates.RemoveWhere(candidate => attemptedMinds.Contains(candidate.Owner));

        if (candidates.Count == 0)
        {
            mind = default;
            return false;
        }

        mind = _random.Pick(candidates);
        return true;
    }

    internal bool IsValidCloneTarget(Entity<MindComponent> mind, EntityUid? rule = null)
    {
        if (rule is { } uid && (mind.Comp.OwnedEntity is not { } body || !RuleStation.IsTarget(uid, body)))
            return false;

        // CentComm jobs are staffed through special roles rather than normal job preferences.
        var onCentcomm = rule is { } source && RuleStation.IsCentCommRule(source);
        if (!onCentcomm && _roles.MindHasRole<GhostRoleMarkerRoleComponent>(mind.Owner))
            return false;

        return _jobs.MindTryGetJob(mind.Owner, out var job) && (onCentcomm || job.SetPreference);
    }
    // DS14-end
}
