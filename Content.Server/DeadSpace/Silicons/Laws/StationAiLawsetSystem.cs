using System.Linq;
using Content.Server.Preferences.Managers;
using Content.Server.Silicons.Laws;
using Content.Shared.Clothing;
using Content.Shared.DeadSpace.Silicons.Laws;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Silicons.Laws;

// DS14-start
/// <summary>
/// Applies the station AI's chosen name and lawset when the player spawns as the AI.
/// </summary>
public sealed class StationAiLawsetSystem : EntitySystem
{
    [Dependency] private readonly SiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private const string StationAiJob = "StationAi";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<StationAiHeldComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId != StationAiJob)
            return;

        ApplyName(args.Mob, args.Profile);
        ApplyLawset(args.Mob, args.Profile);
    }

    private void OnMindAdded(Entity<StationAiHeldComponent> ent, ref MindAddedMessage args)
    {
        if (!_mind.TryGetMind(ent.Owner, out _, out var mind) ||
            mind.UserId is not { } userId)
        {
            return;
        }

        var prefs = _preferences.GetPreferencesOrNull(userId);
        if (prefs?.SelectedCharacter is not HumanoidCharacterProfile profile)
            return;

        ApplyName(ent.Owner, profile);
        ApplyLawset(ent.Owner, profile);
    }

    private void ApplyName(EntityUid mob, HumanoidCharacterProfile profile)
    {
        var jobLoadout = LoadoutSystem.GetJobPrototype(StationAiJob);
        if (!profile.Loadouts.TryGetValue(jobLoadout, out var loadout) ||
            string.IsNullOrWhiteSpace(loadout.EntityName))
        {
            return;
        }

        _metaData.SetEntityName(mob, loadout.EntityName);

        if (_containers.TryGetContainingContainer(mob, out var container) &&
            HasComp<StationAiHolderComponent>(container.Owner))
        {
            _metaData.SetEntityName(container.Owner, loadout.EntityName);
        }
    }

    private void ApplyLawset(EntityUid mob, HumanoidCharacterProfile profile)
    {
        var lawsetId = profile.StationAiLawset;
        if (lawsetId == null ||
            !_prototype.HasIndex(lawsetId.Value) ||
            !StationAiLawsets.Available.Contains(lawsetId.Value))
        {
            return;
        }

        if (TryComp<SiliconLawProviderComponent>(mob, out var provider) &&
            provider.Laws == lawsetId.Value &&
            provider.Lawset != null)
        {
            return;
        }

        _siliconLaw.SetLawset(mob, lawsetId.Value);
    }
}
// DS14-end
