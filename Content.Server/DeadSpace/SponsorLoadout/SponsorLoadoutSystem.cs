using Content.Shared.GameTicking;
using Content.Server.Hands.Systems;
using Robust.Shared.Prototypes;
using Content.DeadSpace.Interfaces.Server;
using Robust.Shared.Network;

namespace Content.Server.DeadSpace.SponsorLoadout;

public sealed class SponsorLoadoutSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly HandsSystem _handsSystem = default!;
    private IServerSponsorsManager? _sponsorsManager; // DS14-sponsors
    private readonly HashSet<(NetUserId User, string Loadout)> _granted = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _granted.Clear());

        IoCManager.Instance!.TryResolveType(out _sponsorsManager); // DS14-sponsors
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (_sponsorsManager?.TryGetInfo(ev.Player.UserId, out var sponsor) == true)
        {
            foreach (var loadoutId in sponsor.AllowedMarkings)
            {
                // NOTE: Now is easy to not extract method because event give all info we need
                if (_prototypeManager.TryIndex<SponsorLoadoutPrototype>(loadoutId, out var loadout))
                {
                    var isSponsorOnly = loadout.SponsorOnly &&
                                        !sponsor.AllowedMarkings.Contains(loadoutId);
                    var isWhitelisted = ev.JobId != null &&
                                        loadout.WhitelistJobs != null &&
                                        !loadout.WhitelistJobs.Contains(ev.JobId);
                    var isBlacklisted = ev.JobId != null &&
                                        loadout.BlacklistJobs != null &&
                                        loadout.BlacklistJobs.Contains(ev.JobId);
                    var isSpeciesRestricted = loadout.SpeciesRestrictions != null &&
                                              loadout.SpeciesRestrictions.Contains(ev.Profile.Species);

                    if (isSponsorOnly || isWhitelisted || isBlacklisted || isSpeciesRestricted)
                        continue;

                    if (!_granted.Add((ev.Player.UserId, loadoutId)))
                        continue;

                    var entity = Spawn(loadout.EntityId, Transform(ev.Mob).Coordinates);

                    _handsSystem.TryPickup(ev.Mob, entity);
                }
            }
        }
    }
}
