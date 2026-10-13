// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Explosion.EntitySystems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents.Events;

public sealed class SurvivalNecromorphBreachRule : StationEventSystem<SurvivalNecromorphBreachRuleComponent>
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    protected override void Started(EntityUid uid,
        SurvivalNecromorphBreachRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.BreachSites.Clear();

        var now = Timing.CurTime;
        var breachCount = Math.Max(component.BreachCount, 1);
        var attempts = 0;
        while (component.BreachSites.Count < breachCount && attempts < breachCount * 10)
        {
            attempts++;
            if (!TryFindRandomTile(out _, out _, out _, out var coords, rule: uid)) // DS14
                continue;

            var breachIndex = component.BreachSites.Count;
            var telegraphTime = now + TimeSpan.FromSeconds(component.BreachInterval * breachIndex);
            var explosionTime = telegraphTime + TimeSpan.FromSeconds(component.TelegraphDelay);
            var spawnTime = explosionTime + TimeSpan.FromSeconds(component.SpawnDelayAfterExplosion);
            component.BreachSites.Add(new SurvivalNecromorphBreachSite(coords, telegraphTime, explosionTime, spawnTime));
        }

        if (component.BreachSites.Count == 0)
        {
            Sawmill.Warning("Survival necromorph breach could not find any valid station tiles.");
            ForceEndSelf(uid, gameRule);
        }
    }

    protected override void ActiveTick(EntityUid uid,
        SurvivalNecromorphBreachRuleComponent component,
        GameRuleComponent gameRule,
        float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var allSpawned = true;
        foreach (var site in component.BreachSites)
        {
            if (!site.TelegraphSpawned && Timing.CurTime >= site.TelegraphTime)
                SpawnTelegraph(component, site);

            if (!site.Exploded && Timing.CurTime >= site.ExplosionTime)
                Explode(component, site);

            if (!site.Spawned && Timing.CurTime >= site.SpawnTime)
                SpawnNecromorph(component, site);

            allSpawned &= site.Spawned;
        }

        if (allSpawned)
            ForceEndSelf(uid, gameRule);
    }

    private void SpawnTelegraph(SurvivalNecromorphBreachRuleComponent component, SurvivalNecromorphBreachSite site)
    {
        var coords = site.Coordinates;
        Spawn(component.TelegraphEffect, coords);

        if (component.TelegraphSound != null)
            _audio.PlayPvs(component.TelegraphSound, coords, component.TelegraphSound.Params);

        site.TelegraphSpawned = true;
    }

    private void Explode(SurvivalNecromorphBreachRuleComponent component, SurvivalNecromorphBreachSite site)
    {
        var coords = site.Coordinates;
        Spawn(component.SmokeEffect, coords);

        _explosion.QueueExplosion(
            _transform.ToMapCoordinates(coords),
            component.ExplosionPrototype,
            component.TotalIntensity,
            component.Slope,
            component.MaxTileIntensity,
            null,
            component.TileBreakScale,
            component.MaxTileBreak,
            component.CanCreateVacuum);

        site.Exploded = true;
    }

    private void SpawnNecromorph(SurvivalNecromorphBreachRuleComponent component, SurvivalNecromorphBreachSite site)
    {
        if (!TryPickSpawn(component, out var entry))
        {
            Sawmill.Warning("Survival necromorph breach has no valid spawn entries.");
            site.Spawned = true;
            return;
        }

        var coords = site.Coordinates;
        Spawn(component.SpawnEffect, coords);
        Spawn(entry.Prototype, coords);

        if (component.SpawnSound != null)
            _audio.PlayPvs(component.SpawnSound, coords, component.SpawnSound.Params);

        site.Spawned = true;
    }

    private bool TryPickSpawn(SurvivalNecromorphBreachRuleComponent component, out SurvivalNecromorphBreachSpawnEntry entry)
    {
        entry = default!;

        var roundMinutes = (float) GameTicker.RoundDuration().TotalMinutes;
        var playerCount = _player.PlayerCount;
        var totalWeight = 0f;

        foreach (var spawnEntry in component.SpawnEntries)
        {
            if (!CanPick(spawnEntry, roundMinutes, playerCount))
                continue;

            totalWeight += spawnEntry.Weight;
        }

        if (totalWeight <= 0f)
            return false;

        var roll = RobustRandom.NextFloat(totalWeight);

        foreach (var spawnEntry in component.SpawnEntries)
        {
            if (!CanPick(spawnEntry, roundMinutes, playerCount))
                continue;

            roll -= spawnEntry.Weight;
            if (roll > 0f)
                continue;

            entry = spawnEntry;
            return true;
        }

        return false;
    }

    private bool CanPick(SurvivalNecromorphBreachSpawnEntry entry, float roundMinutes, int playerCount)
    {
        return entry.Weight > 0f
               && roundMinutes >= entry.EarliestRoundTime
               && playerCount >= entry.MinimumPlayers
               && HasValidPrototype(entry);
    }

    private bool HasValidPrototype(SurvivalNecromorphBreachSpawnEntry entry)
    {
        return _prototype.HasIndex<EntityPrototype>(entry.Prototype);
    }
}
