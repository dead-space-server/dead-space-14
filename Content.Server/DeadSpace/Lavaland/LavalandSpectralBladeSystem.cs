using System.Linq;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.EUI;
using Content.Shared.Eye;
using Robust.Server.GameObjects;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Follower.Components;
using Content.Shared.Follower;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Server.Player;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandSpectralBladeSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, PendingSummon> _activeSummons = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextGhostSummon = new();
    private readonly List<EntityUid> _expiredSummons = new();
    private readonly List<EntityUid> _expiredCooldowns = new();

    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FollowerSystem _follower = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;

    private readonly HashSet<EntityUid> _stillFollowing = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandSpectralBladeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LavalandSpectralBladeComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<LavalandSpectralBladeComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        PruneSummonState(now);

        var query = EntityQueryEnumerator<LavalandSpectralBladeComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            UpdateRevealedGhosts((uid, component), component.ActiveUntil > now);

            if (component.CurrentSpirits <= 0 || component.ActiveUntil > now)
                continue;

            component.CurrentSpirits = 0;
            ApplyDamage((uid, component));
        }
    }

    private void OnShutdown(Entity<LavalandSpectralBladeComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.CurrentSpirits = 0;
        ApplyDamage(ent);
        UpdateRevealedGhosts(ent, false);
    }

    /// <summary>
    /// While the blade is active the spirits circling it are seen by the living too; let go, or the blade calmed,
    /// they are ghosts again.
    /// </summary>
    private void UpdateRevealedGhosts(Entity<LavalandSpectralBladeComponent> ent, bool active)
    {
        _stillFollowing.Clear();
        if (active && TryComp<FollowedComponent>(ent, out var followed))
        {
            foreach (var follower in followed.Following)
            {
                if (HasComp<GhostComponent>(follower))
                    _stillFollowing.Add(follower);
            }
        }

        foreach (var ghost in ent.Comp.RevealedGhosts.ToArray())
        {
            if (_stillFollowing.Contains(ghost))
                continue;

            ent.Comp.RevealedGhosts.Remove(ghost);
            SetGhostShown(ghost, false);
        }

        foreach (var ghost in _stillFollowing)
        {
            // A ghost already shown to all (the round's end) is left as it is.
            if (ent.Comp.RevealedGhosts.Contains(ghost) ||
                !TryComp<VisibilityComponent>(ghost, out var visibility) ||
                (visibility.Layer & (int) VisibilityFlags.Normal) != 0)
            {
                continue;
            }

            ent.Comp.RevealedGhosts.Add(ghost);
            SetGhostShown(ghost, true);
        }
    }

    private void SetGhostShown(EntityUid ghost, bool shown)
    {
        // Back in a body, the ghost's own shutdown has already made it visible as it should be.
        if (TerminatingOrDeleted(ghost) ||
            !HasComp<GhostComponent>(ghost) ||
            !TryComp<VisibilityComponent>(ghost, out var visibility))
        {
            return;
        }

        if (shown)
        {
            _visibility.AddLayer((ghost, visibility), (int) VisibilityFlags.Normal, false);
            _visibility.RemoveLayer((ghost, visibility), (int) VisibilityFlags.Ghost, false);
        }
        else
        {
            _visibility.AddLayer((ghost, visibility), (int) VisibilityFlags.Ghost, false);
            _visibility.RemoveLayer((ghost, visibility), (int) VisibilityFlags.Normal, false);
        }

        _visibility.RefreshVisibility(ghost, visibilityComponent: visibility);
    }

    private void OnUseInHand(Entity<LavalandSpectralBladeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var now = _timing.CurTime;
        if (ent.Comp.NextActivation > now)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-spectral-blade-cooldown"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        var summonExpiresAt = now + TimeSpan.FromSeconds(Math.Max(ent.Comp.SummonResponseTime, 1f));
        ent.Comp.CurrentSpirits = CountSpirits(ent.Owner, ent.Comp);
        var summons = ent.Comp.CurrentSpirits >= ent.Comp.MaxSpirits
            ? 0
            : OpenSummonWindows(ent.Owner, args.User, ent.Comp, summonExpiresAt);

        if (ent.Comp.CurrentSpirits <= 0 && summons <= 0)
        {
            ent.Comp.NextActivation = now + TimeSpan.FromSeconds(Math.Max(ent.Comp.FailedActivationCooldown, 1f));
            ApplyDamage(ent);
            _popup.PopupEntity(Loc.GetString("lavaland-spectral-blade-no-spirits"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.ActiveUntil = now + TimeSpan.FromSeconds(Math.Max(ent.Comp.ActivationDuration, 1f));
        ent.Comp.NextActivation = now + TimeSpan.FromSeconds(Math.Max(ent.Comp.ActivationCooldown, 1f));
        ApplyDamage(ent);

        _audio.PlayPvs(ent.Comp.ActivationSound, args.User);
        _popup.PopupEntity(
            Loc.GetString("lavaland-spectral-blade-summoned", ("spirits", ent.Comp.CurrentSpirits), ("summons", summons)),
            args.User,
            args.User,
            PopupType.Medium);
    }

    private void OnExamined(Entity<LavalandSpectralBladeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("lavaland-spectral-blade-examine", ("spirits", ent.Comp.CurrentSpirits)));
    }

    public void HandleSpectralBladeSummonResponse(
        EntityUid blade,
        EntityUid summoner,
        ICommonSession session,
        bool accepted,
        TimeSpan expiresAt)
    {
        if (session.AttachedEntity is not { } ghost)
            return;

        if (!_activeSummons.TryGetValue(ghost, out var pending) ||
            pending.Blade != blade ||
            pending.ExpiresAt != expiresAt)
        {
            return;
        }

        _activeSummons.Remove(ghost);

        if (!accepted)
            return;

        var now = _timing.CurTime;
        if (now > expiresAt ||
            !HasComp<GhostComponent>(ghost) ||
            !Exists(blade) ||
            !TryComp<LavalandSpectralBladeComponent>(blade, out var component))
        {
            return;
        }

        var bladeCoordinates = _transform.GetMapCoordinates(blade);
        if (bladeCoordinates.MapId == MapId.Nullspace)
            return;

        _follower.StartFollowingEntity(ghost, blade);
        _popup.PopupEntity(Loc.GetString("lavaland-spectral-blade-summon-accepted"), ghost, ghost, PopupType.Medium);

        if (component.ActiveUntil <= now)
            return;

        component.CurrentSpirits = CountSpirits(blade, component);
        ApplyDamage((blade, component));

        if (Exists(summoner))
        {
            _popup.PopupEntity(
                Loc.GetString("lavaland-spectral-blade-spirit-joined", ("spirits", component.CurrentSpirits)),
                summoner,
                summoner,
                PopupType.Small);
        }
    }

    private int CountSpirits(EntityUid blade, LavalandSpectralBladeComponent component)
    {
        if (component.MaxSpirits <= 0)
            return 0;

        var coordinates = _transform.GetMapCoordinates(blade);
        if (coordinates.MapId == MapId.Nullspace)
            return 0;

        var countedGhosts = new HashSet<EntityUid>();
        var spirits = CountFollowingGhosts(blade, coordinates, component, countedGhosts);
        if (spirits >= component.MaxSpirits)
            return component.MaxSpirits;

        spirits += CountNearbyGhosts(coordinates, component, countedGhosts, component.MaxSpirits - spirits);
        if (spirits >= component.MaxSpirits)
            return component.MaxSpirits;

        spirits += CountNearbyDeadHumanoids(coordinates, component, component.MaxSpirits - spirits);
        return Math.Min(spirits, component.MaxSpirits);
    }

    private int OpenSummonWindows(
        EntityUid blade,
        EntityUid summoner,
        LavalandSpectralBladeComponent component,
        TimeSpan expiresAt)
    {
        PruneSummonState(_timing.CurTime);

        if (component.MaxSummonWindows <= 0)
            return 0;

        var summons = 0;
        var summonerName = MetaData(summoner).EntityName;
        var maxWindows = Math.Min(component.MaxSummonWindows, component.MaxSpirits * 4);
        var nextSummon = _timing.CurTime + TimeSpan.FromSeconds(Math.Max(component.GhostSummonCooldown, 1f));

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } ghost ||
                !HasComp<GhostComponent>(ghost) ||
                IsAlreadyFollowing(ghost, blade) ||
                IsSummonSuppressed(ghost))
            {
                continue;
            }

            _activeSummons[ghost] = new PendingSummon(blade, expiresAt);
            _nextGhostSummon[ghost] = nextSummon;
            _eui.OpenEui(new LavalandSpectralBladeSummonEui(blade, summoner, this, expiresAt, summonerName), session);
            summons++;

            if (summons >= maxWindows)
                break;
        }

        return summons;
    }

    private bool IsAlreadyFollowing(EntityUid ghost, EntityUid blade)
    {
        return TryComp<FollowerComponent>(ghost, out var follower) && follower.Following == blade;
    }

    private bool IsSummonSuppressed(EntityUid ghost)
    {
        var now = _timing.CurTime;
        return _activeSummons.TryGetValue(ghost, out var active) && active.ExpiresAt > now ||
               _nextGhostSummon.TryGetValue(ghost, out var nextSummon) && nextSummon > now;
    }

    private void PruneSummonState(TimeSpan now)
    {
        _expiredSummons.Clear();
        foreach (var (ghost, summon) in _activeSummons)
        {
            if (!Exists(ghost) || !Exists(summon.Blade) || summon.ExpiresAt <= now)
                _expiredSummons.Add(ghost);
        }

        foreach (var ghost in _expiredSummons)
            _activeSummons.Remove(ghost);

        _expiredCooldowns.Clear();
        foreach (var (ghost, nextSummon) in _nextGhostSummon)
        {
            if (!Exists(ghost) || nextSummon <= now)
                _expiredCooldowns.Add(ghost);
        }

        foreach (var ghost in _expiredCooldowns)
            _nextGhostSummon.Remove(ghost);
    }

    private int CountFollowingGhosts(
        EntityUid blade,
        MapCoordinates coordinates,
        LavalandSpectralBladeComponent component,
        HashSet<EntityUid> countedGhosts)
    {
        if (!TryComp<FollowedComponent>(blade, out var followed))
            return 0;

        var spirits = 0;
        foreach (var follower in followed.Following)
        {
            if (!IsValidGhostSpirit(follower, coordinates) || !countedGhosts.Add(follower))
            {
                continue;
            }

            spirits++;
            if (spirits >= component.MaxSpirits)
                break;
        }

        return spirits;
    }

    private int CountNearbyGhosts(
        MapCoordinates coordinates,
        LavalandSpectralBladeComponent component,
        HashSet<EntityUid> countedGhosts,
        int remaining)
    {
        if (remaining <= 0 || component.NearbyGhostRange <= 0f)
            return 0;

        var spirits = 0;
        foreach (var (ghost, _) in _lookup.GetEntitiesInRange<GhostComponent>(coordinates, component.NearbyGhostRange))
        {
            if (!IsValidGhostSpirit(ghost, coordinates) || !countedGhosts.Add(ghost))
                continue;

            spirits++;
            if (spirits >= remaining)
                break;
        }

        return spirits;
    }

    private bool IsValidGhostSpirit(EntityUid ghost, MapCoordinates bladeCoordinates)
    {
        if (!HasComp<GhostComponent>(ghost))
            return false;

        var ghostCoordinates = _transform.GetMapCoordinates(ghost);
        return ghostCoordinates.MapId == bladeCoordinates.MapId;
    }

    private int CountNearbyDeadHumanoids(
        MapCoordinates coordinates,
        LavalandSpectralBladeComponent component,
        int remaining)
    {
        if (remaining <= 0 || component.CorpseRange <= 0f)
            return 0;

        var spirits = 0;
        foreach (var (candidate, _) in _lookup.GetEntitiesInRange<HumanoidAppearanceComponent>(coordinates, component.CorpseRange))
        {
            if (!TryComp<MobStateComponent>(candidate, out var mobState) ||
                !_mobState.IsDead(candidate, mobState))
            {
                continue;
            }

            spirits++;
            if (spirits >= remaining)
                break;
        }

        return spirits;
    }

    private void ApplyDamage(Entity<LavalandSpectralBladeComponent> ent)
    {
        if (!TryComp<MeleeWeaponComponent>(ent.Owner, out var melee))
            return;

        var bonus = ent.Comp.DamagePerSpirit * ent.Comp.CurrentSpirits;
        if (bonus > ent.Comp.MaxBonusDamage)
            bonus = ent.Comp.MaxBonusDamage;

        if (bonus < FixedPoint2.Zero)
            bonus = FixedPoint2.Zero;

        var damage = new DamageSpecifier();
        damage.DamageDict[ent.Comp.DamageType] = ent.Comp.BaseDamage + bonus;
        melee.Damage = damage;
        Dirty(ent.Owner, melee);
    }

    private readonly record struct PendingSummon(EntityUid Blade, TimeSpan ExpiresAt);
}
