using Content.Server.GameTicking;
using Content.Server.Shuttles.Systems;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using System.Text;
using Content.Server.Objectives.Commands;
using Content.Shared.Prototypes;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Utility;

namespace Content.Server.Objectives;

public sealed class ObjectivesSystem : SharedObjectivesSystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;

    private IEnumerable<string>? _objectives;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);

        _prototypeManager.PrototypesReloaded += CreateCompletions;
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _prototypeManager.PrototypesReloaded -= CreateCompletions;
    }

    /// <summary>
    /// Adds aggregate objective role text on round end.
    /// </summary>
    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        // go through each gamerule getting data for the roundend summary.
        var summaries = new Dictionary<string, Dictionary<string, List<(EntityUid, string)>>>();
        var query = EntityQueryEnumerator<GameRuleComponent>();
        while (query.MoveNext(out var uid, out var gameRule))
        {
            if (!_gameTicker.IsGameRuleAdded(uid, gameRule))
                continue;

            var info = new ObjectivesTextGetInfoEvent(new List<(EntityUid, string)>(), string.Empty);
            RaiseLocalEvent(uid, ref info);
            if (info.Minds.Count == 0)
                continue;

            // first group the gamerules by their agents, for example 2 different dragons
            var agent = info.AgentName;
            if (!summaries.ContainsKey(agent))
                summaries[agent] = new Dictionary<string, List<(EntityUid, string)>>();

            var prepend = new ObjectivesTextPrependEvent("");
            RaiseLocalEvent(uid, ref prepend);

            // next group them by their prepended texts
            // for example with traitor rule, group them by the codewords they share
            var summary = summaries[agent];
            if (summary.ContainsKey(prepend.Text))
            {
                // same prepended text (usually empty) so combine them
                summary[prepend.Text].AddRange(info.Minds);
            }
            else
            {
                summary[prepend.Text] = info.Minds;
            }
        }

        // convert the data into summary text
        foreach (var (agent, summary) in summaries)
        {
            // first get the total number of players that were in these game rules combined
            var total = 0;
            var totalInCustody = 0;
            foreach (var (_, minds) in summary)
            {
                total += minds.Count;
                totalInCustody += minds.Where(pair => IsInCustody(pair.Item1)).Count();
            }

            var result = new StringBuilder();
            result.AppendLine(Loc.GetString("objectives-round-end-result", ("count", total), ("agent", agent)));
            if (agent == Loc.GetString("traitor-round-end-agent-name"))
            {
                result.AppendLine(Loc.GetString("objectives-round-end-result-in-custody", ("count", total), ("custody", totalInCustody), ("agent", agent)));
            }

            foreach (var (prepend, _) in summary)
            {
                if (!string.IsNullOrWhiteSpace(prepend))
                    result.AppendLine(prepend.Trim());
            }

            ev.AddLine(result.AppendLine().ToString());
        }
    }

    public EntityUid? GetRandomObjective(EntityUid mindId, MindComponent mind, ProtoId<WeightedRandomPrototype> objectiveGroupProto, float maxDifficulty)
    {
        return GetRandomObjective(mindId, mind, objectiveGroupProto, maxDifficulty, null);
    }

    public EntityUid? GetRandomObjective(
        EntityUid mindId,
        MindComponent mind,
        ProtoId<WeightedRandomPrototype> objectiveGroupProto,
        float maxDifficulty,
        IReadOnlySet<string>? excludedPrototypes)
    // DS14-end
    {
        if (!_prototypeManager.TryIndex(objectiveGroupProto, out var groupsProto))
        {
            Log.Error($"Tried to get a random objective, but can't index WeightedRandomPrototype {objectiveGroupProto}");
            return null;
        }

        // Make a copy of the weights so we don't trash the prototype by removing entries
        var groups = groupsProto.Weights.ShallowClone();

        while (_random.TryPickAndTake(groups, out var groupName))
        {
            if (!_prototypeManager.TryIndex<WeightedRandomPrototype>(groupName, out var group))
            {
                Log.Error($"Couldn't index objective group prototype {groupName}");
                return null;
            }

            var objectives = group.Weights.ShallowClone();
            while (_random.TryPickAndTake(objectives, out var objectiveProto))
            {
                // DS14-start
                if (excludedPrototypes != null && excludedPrototypes.Contains(objectiveProto))
                    continue;
                // DS14-end

                if (!_prototypeManager.Index(objectiveProto).TryGetComponent<ObjectiveComponent>(out var objectiveComp, EntityManager.ComponentFactory))
                    continue;

                if (objectiveComp.Difficulty <= maxDifficulty && TryCreateObjective((mindId, mind), objectiveProto, out var objective))
                    return objective;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns whether a target is considered 'in custody' (cuffed on the shuttle).
    /// </summary>
    public bool IsInCustody(EntityUid mindId, MindComponent? mind = null) // DS14. did public
    {
        if (!Resolve(mindId, ref mind))
            return false;

        // Ghosting will not save you
        bool originalEntityInCustody = false;
        EntityUid? originalEntity = GetEntity(mind.OriginalOwnedEntity);
        if (originalEntity.HasValue && originalEntity != mind.OwnedEntity)
        {
            originalEntityInCustody = TryComp<CuffableComponent>(originalEntity, out var origCuffed) && origCuffed.CuffedHandCount > 0
                   && _emergencyShuttle.IsTargetEscaping(originalEntity.Value);
        }

        return originalEntityInCustody || (TryComp<CuffableComponent>(mind.OwnedEntity, out var cuffed) && cuffed.CuffedHandCount > 0
               && _emergencyShuttle.IsTargetEscaping(mind.OwnedEntity.Value));
    }

    /// <summary>
    /// Get the title for a player's mind used in round end.
    /// Pass in the original entity name which is shown alongside username.
    /// </summary>
    public string GetTitle(Entity<MindComponent?> mind, string name)
    {
        if (Resolve(mind, ref mind.Comp) &&
            mind.Comp.OriginalOwnerUserId != null &&
            _player.TryGetPlayerData(mind.Comp.OriginalOwnerUserId.Value, out var sessionData))
        {
            var username = sessionData.UserName;

            var nameWithJobMaybe = name;
            if (_job.MindTryGetJobName(mind, out var jobName))
                nameWithJobMaybe += ", " + jobName;

            return Loc.GetString("objectives-player-user-named", ("user", username), ("name", nameWithJobMaybe));
        }

        return Loc.GetString("objectives-player-named", ("name", name));
    }


    private void CreateCompletions(PrototypesReloadedEventArgs unused)
    {
        CreateCompletions();
    }

    /// <summary>
    /// Get all objective prototypes by their IDs.
    /// This is used for completions in <see cref="AddObjectiveCommand"/>
    /// </summary>
    public IEnumerable<string> Objectives()
    {
        if (_objectives == null)
            CreateCompletions();

        return _objectives!;
    }

    private void CreateCompletions()
    {
        _objectives = _prototypeManager.EnumeratePrototypes<EntityPrototype>()
            .Where(p => p.HasComponent<ObjectiveComponent>())
            .Select(p => p.ID)
            .Order();
    }
}

/// <summary>
/// Raised on the game rule to get info for any objectives.
/// If its minds list is set then the players will have their objectives shown in the round end text.
/// AgentName is the generic name for a player in the list.
/// </summary>
/// <remarks>
/// The objectives system already checks if the game rule is added so you don't need to check that in this event's handler.
/// </remarks>
[ByRefEvent]
public record struct ObjectivesTextGetInfoEvent(List<(EntityUid, string)> Minds, string AgentName);

/// <summary>
/// Raised on the game rule before text for each agent's objectives is added, letting you prepend something.
/// </summary>
[ByRefEvent]
public record struct ObjectivesTextPrependEvent(string Text);
