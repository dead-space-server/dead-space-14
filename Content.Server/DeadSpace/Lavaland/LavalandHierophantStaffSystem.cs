using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Attachments;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandHierophantStaffSystem : EntitySystem
{
    private const int MaxPendingBlasts = 90;
    private const float DuplicateBlastWindow = 0.12f;

    private static readonly Vector2i[] Cardinals =
    {
        new(0, 1),
        new(1, 0),
        new(0, -1),
        new(-1, 0),
    };

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly LavalandHierophantBeatSystem _beat = default!;

    private readonly List<PendingBlast> _pendingBlasts = new();
    private readonly List<PendingTeleport> _pendingTeleports = new();
    private readonly List<PendingChaser> _pendingChasers = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _detonatingTiles = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), PendingBlast> _detonatingBlasts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandHierophantStaffComponent, LavalandHierophantStaffCrossActionEvent>(OnCross);
        SubscribeLocalEvent<LavalandHierophantStaffComponent, LavalandHierophantStaffBurstActionEvent>(OnBurst);
        SubscribeLocalEvent<LavalandHierophantStaffComponent, LavalandHierophantStaffBlinkActionEvent>(OnBlink);
        SubscribeLocalEvent<LavalandHierophantStaffComponent, LavalandHierophantStaffChaserActionEvent>(OnChaser);
        SubscribeLocalEvent<AttachmentHierophantChaserEvent>(OnAttachmentChaser);
        SubscribeLocalEvent<LavalandHierophantStaffComponent, LavalandAltUseEvent>(OnAltUse);
        SubscribeLocalEvent<LavalandHierophantStaffComponent, LavalandHierophantBlessDoAfterEvent>(OnBlessDoAfter);
        SubscribeLocalEvent<LavalandHierophantStaffComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<LavalandHierophantStaffComponent, GotUnequippedHandEvent>(OnUnequippedHand);
    }

    #region Alt + right click: blink and blessing

    private void OnAltUse(Entity<LavalandHierophantStaffComponent> ent, ref LavalandAltUseEvent args)
    {
        var user = args.User;
        if (!CanUseStaff(ent, user))
            return;

        // On a creature: the blessing. Anywhere else: the blink.
        if (args.Target is { } target && target != ent.Owner &&
            HasComp<InputMoverComponent>(target) && HasComp<MobStateComponent>(target) && !IsDead(target))
        {
            TryStartBlessing(ent, user, target);
            return;
        }

        var now = _timing.CurTime;
        if (now < ent.Comp.NextBlink)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-hierophant-staff-blink-cooldown",
                ("seconds", (int) Math.Ceiling((ent.Comp.NextBlink - now).TotalSeconds))), user, user);
            return;
        }

        var userPosition = _transform.GetMapCoordinates(user);
        var targetPosition = _transform.ToMapCoordinates(args.Coordinates);
        if (userPosition.MapId != targetPosition.MapId ||
            (userPosition.Position - targetPosition.Position).Length() > ent.Comp.BlinkRange)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-hierophant-staff-blink-range"), user, user);
            return;
        }

        if (!TryBlink(user, args.Coordinates, ent.Comp.Blink))
            return;

        ent.Comp.NextBlink = now + ent.Comp.BlinkCooldown;
        UpdateAlerts(ent, user);
    }

    private void TryStartBlessing(Entity<LavalandHierophantStaffComponent> ent, EntityUid user, EntityUid target)
    {
        var userPosition = _transform.GetMapCoordinates(user);
        var targetPosition = _transform.GetMapCoordinates(target);
        if (userPosition.MapId != targetPosition.MapId ||
            (userPosition.Position - targetPosition.Position).Length() > ent.Comp.BlessRange)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-hierophant-staff-bless-range"), user, user);
            return;
        }

        // Ending the walk is free; giving it takes the charge.
        var now = _timing.CurTime;
        if (!_beat.IsBlessed(target) && now < ent.Comp.NextBless)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-hierophant-staff-bless-cooldown",
                ("seconds", (int) Math.Ceiling((ent.Comp.NextBless - now).TotalSeconds))), user, user);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.BlessTime, new LavalandHierophantBlessDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DistanceThreshold = ent.Comp.BlessRange + 0.5f,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("lavaland-hierophant-staff-bless-start"), user, PopupType.Medium);
    }

    private void OnBlessDoAfter(Entity<LavalandHierophantStaffComponent> ent, ref LavalandHierophantBlessDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || TerminatingOrDeleted(target))
            return;

        args.Handled = true;
        if (_beat.IsBlessed(target))
        {
            _beat.SetBlessed(target, false);
            _popup.PopupEntity(Loc.GetString("lavaland-hierophant-staff-bless-end"), target, PopupType.Medium);
            return;
        }

        var now = _timing.CurTime;
        if (now < ent.Comp.NextBless)
            return;

        _beat.SetBlessed(target, true);
        ent.Comp.NextBless = now + ent.Comp.BlessCooldown;
        UpdateAlerts(ent, args.User);
        _popup.PopupEntity(Loc.GetString("lavaland-hierophant-staff-bless-done"), target, PopupType.Medium);
    }

    private void OnEquippedHand(Entity<LavalandHierophantStaffComponent> ent, ref GotEquippedHandEvent args)
    {
        UpdateAlerts(ent, args.User);
    }

    private void OnUnequippedHand(Entity<LavalandHierophantStaffComponent> ent, ref GotUnequippedHandEvent args)
    {
        _alerts.ClearAlert(args.User, ent.Comp.BlinkAlert);
    }

    /// <summary>The blink alert at the side of the screen, with its cooldown.</summary>
    private void UpdateAlerts(Entity<LavalandHierophantStaffComponent> ent, EntityUid user)
    {
        var now = _timing.CurTime;
        _alerts.ShowAlert(user, ent.Comp.BlinkAlert,
            cooldown: ent.Comp.NextBlink > now ? (now, ent.Comp.NextBlink) : null, autoRemove: false);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        ProcessTeleports(now);
        ProcessChasers(now);
        ProcessPendingBlasts(now);
    }

    private void OnCross(EntityUid uid, LavalandHierophantStaffComponent component, LavalandHierophantStaffCrossActionEvent args)
    {
        if (args.Handled ||
            !CanUseStaff(uid, args.Performer) ||
            !TryGetWorldTargetTile(args.Performer, args.Target, out var gridUid, out var grid, out var targetTile, out _))
        {
            return;
        }

        var now = _timing.CurTime;
        var detonateAt = now + ClampDelay(args.TelegraphDelay, TimeSpan.FromSeconds(0.25));

        PlayTile(args.TelegraphSound, gridUid, grid, targetTile, -3f);
        QueueBlast(gridUid, grid, targetTile, args.TelegraphPrototype, now, detonateAt, args.BlastPrototype, args.Damage, args.Performer, args.BlastSound, args.HitSound);

        var range = Math.Clamp(args.BeamRange, 1, 8);
        foreach (var direction in Cardinals)
        {
            for (var i = 1; i <= range; i++)
            {
                QueueBlast(gridUid, grid, targetTile + direction * i, args.SquaresPrototype, now, detonateAt, args.BlastPrototype, args.Damage, args.Performer, args.BlastSound, args.HitSound);
            }
        }

        args.Handled = true;
    }

    private void OnBurst(EntityUid uid, LavalandHierophantStaffComponent component, LavalandHierophantStaffBurstActionEvent args)
    {
        if (args.Handled ||
            !CanUseStaff(uid, args.Performer) ||
            !TryGetWorldTargetTile(args.Performer, args.Target, out var gridUid, out var grid, out var targetTile, out _))
        {
            return;
        }

        var now = _timing.CurTime;
        var radius = Math.Clamp(args.Radius, 1, 5);
        var stepDelay = ClampDelay(args.StepDelay, TimeSpan.FromSeconds(0.05));
        var telegraphDelay = ClampDelay(args.TelegraphDelay, TimeSpan.FromSeconds(0.25));

        PlayTile(args.TelegraphSound, gridUid, grid, targetTile, -4f);
        for (var distance = 0; distance <= radius; distance++)
        {
            var telegraphAt = now + TimeSpan.FromTicks(stepDelay.Ticks * distance);
            var detonateAt = telegraphAt + telegraphDelay;

            for (var x = -distance; x <= distance; x++)
            {
                for (var y = -distance; y <= distance; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != distance)
                        continue;

                    QueueBlast(gridUid, grid, targetTile + new Vector2i(x, y), args.SquaresPrototype, telegraphAt, detonateAt, args.BlastPrototype, args.Damage, args.Performer, args.BlastSound, args.HitSound);
                }
            }
        }

        args.Handled = true;
    }

    private void OnBlink(EntityUid uid, LavalandHierophantStaffComponent component, LavalandHierophantStaffBlinkActionEvent args)
    {
        if (args.Handled || !CanUseStaff(uid, args.Performer))
            return;

        args.Handled = TryBlink(args.Performer, args.Target, args);
    }

    /// <summary>Teleport to the target tile with blasts at both ends.</summary>
    private bool TryBlink(EntityUid performer, EntityCoordinates target, LavalandHierophantStaffBlinkActionEvent args)
    {
        if (HasComp<LavalandNoTeleportZoneComponent>(Transform(performer).MapUid) ||
            !TryGetWorldTargetTile(performer, target, out var gridUid, out var grid, out var targetTile, out var performerTile) ||
            !CanTeleportTo(gridUid, grid, targetTile))
        {
            return false;
        }

        var now = _timing.CurTime;
        var blinkAt = now + ClampDelay(args.BlinkDelay, TimeSpan.FromSeconds(0.1));
        var detonateAt = blinkAt + ClampDelay(args.TelegraphDelay, TimeSpan.FromSeconds(0.25));
        var radius = Math.Clamp(args.BlastRadius, 0, 2);

        PlayTile(args.BlinkSourceSound, gridUid, grid, performerTile, -2f);
        PlayTile(args.BlinkDestinationSound, gridUid, grid, targetTile, -2f);

        QueueRadiusBlasts(gridUid, grid, performerTile, radius, args.SquaresPrototype, now, detonateAt, args.BlastPrototype, args.BlinkDamage, performer, args.BlastSound, args.HitSound);
        QueueRadiusBlasts(gridUid, grid, targetTile, radius, args.SquaresPrototype, now, detonateAt, args.BlastPrototype, args.BlinkDamage, performer, args.BlastSound, args.HitSound);

        _pendingTeleports.Add(new PendingTeleport
        {
            Performer = performer,
            Grid = gridUid,
            Destination = targetTile,
            ExecuteAt = blinkAt,
        });

        return true;
    }

    private void OnChaser(EntityUid uid, LavalandHierophantStaffComponent component, LavalandHierophantStaffChaserActionEvent args)
    {
        if (args.Handled ||
            !CanUseStaff(uid, args.Performer) ||
            args.Target == args.Performer ||
            !TryGetEntityTargetTiles(args.Performer, args.Target, out var gridUid, out var grid, out var performerTile, out _))
        {
            return;
        }

        _pendingChasers.Add(new PendingChaser
        {
            Performer = args.Performer,
            Target = args.Target,
            Grid = gridUid,
            Tile = performerTile,
            NextStep = _timing.CurTime,
            StepDelay = ClampDelay(args.StepDelay, TimeSpan.FromSeconds(0.08)),
            MaxSteps = Math.Clamp(args.MaxSteps, 1, 12),
            TelegraphDelay = ClampDelay(args.TelegraphDelay, TimeSpan.FromSeconds(0.2)),
            SquaresPrototype = args.SquaresPrototype,
            BlastPrototype = args.BlastPrototype,
            Damage = new(args.Damage),
            BlastSound = args.BlastSound,
            HitSound = args.HitSound,
        });

        args.Handled = true;
    }

    /// <summary>Vortex talisman on a crusher: a chaser with the staff's look but the trophy's damage.</summary>
    private void OnAttachmentChaser(ref AttachmentHierophantChaserEvent args)
    {
        if (args.Target == args.User ||
            IsDead(args.Target) ||
            !TryGetEntityTargetTiles(args.User, args.Target, out var gridUid, out _, out var performerTile, out _))
        {
            return;
        }

        var defaults = new LavalandHierophantStaffChaserActionEvent();
        _pendingChasers.Add(new PendingChaser
        {
            Performer = args.User,
            Target = args.Target,
            Grid = gridUid,
            Tile = performerTile,
            NextStep = _timing.CurTime,
            StepDelay = defaults.StepDelay,
            MaxSteps = Math.Clamp(args.MaxSteps, 1, 12),
            TelegraphDelay = defaults.TelegraphDelay,
            SquaresPrototype = defaults.SquaresPrototype,
            BlastPrototype = defaults.BlastPrototype,
            Damage = new(args.Damage),
            BlastSound = defaults.BlastSound,
            HitSound = defaults.HitSound,
        });
    }

    private void ProcessTeleports(TimeSpan now)
    {
        for (var i = _pendingTeleports.Count - 1; i >= 0; i--)
        {
            var pending = _pendingTeleports[i];
            if (pending.ExecuteAt > now)
                continue;

            if (Exists(pending.Performer) &&
                !IsDead(pending.Performer) &&
                TryComp<MapGridComponent>(pending.Grid, out var grid) &&
                TryComp(pending.Performer, out TransformComponent? xform) &&
                xform.GridUid == pending.Grid &&
                CanTeleportTo(pending.Grid, grid, pending.Destination))
            {
                _transform.SetCoordinates(pending.Performer, _map.GridTileToLocal(pending.Grid, grid, pending.Destination));
            }

            _pendingTeleports.RemoveAt(i);
        }
    }

    private void ProcessChasers(TimeSpan now)
    {
        for (var i = _pendingChasers.Count - 1; i >= 0; i--)
        {
            var chaser = _pendingChasers[i];
            if (chaser.NextStep > now)
                continue;

            if (chaser.StepsTaken >= chaser.MaxSteps ||
                !Exists(chaser.Target) ||
                IsDead(chaser.Target) ||
                !TryComp<MapGridComponent>(chaser.Grid, out var grid) ||
                !TryGetEntityTile(chaser.Target, chaser.Grid, grid, out var targetTile))
            {
                _pendingChasers.RemoveAt(i);
                continue;
            }

            var direction = GetChaserDirection(chaser.Tile, targetTile);
            if (direction == Vector2i.Zero)
            {
                _pendingChasers.RemoveAt(i);
                continue;
            }

            var nextTile = chaser.Tile + direction;
            if (!CanAffectTile(chaser.Grid, grid, nextTile))
            {
                _pendingChasers.RemoveAt(i);
                continue;
            }

            chaser.Tile = nextTile;
            chaser.StepsTaken++;
            chaser.NextStep = now + chaser.StepDelay;
            QueueBlast(chaser.Grid, grid, nextTile, chaser.SquaresPrototype, now, now + chaser.TelegraphDelay, chaser.BlastPrototype, chaser.Damage, chaser.Performer, chaser.BlastSound, chaser.HitSound);
        }
    }

    private void ProcessPendingBlasts(TimeSpan now)
    {
        if (_pendingBlasts.Count == 0)
            return;

        _detonatingTiles.Clear();
        _detonatingBlasts.Clear();
        PendingBlast? soundSource = null;

        for (var i = _pendingBlasts.Count - 1; i >= 0; i--)
        {
            var pending = _pendingBlasts[i];
            if (!TryComp<MapGridComponent>(pending.Grid, out var grid))
            {
                _pendingBlasts.RemoveAt(i);
                continue;
            }

            if (!pending.Telegraphed && pending.TelegraphAt <= now)
            {
                if (pending.TelegraphPrototype != null)
                    SpawnAnchored(pending.TelegraphPrototype, pending.Grid, grid, pending.Tile);

                pending.Telegraphed = true;
            }

            if (pending.DetonateAt > now)
                continue;

            if (CanAffectTile(pending.Grid, grid, pending.Tile))
            {
                SpawnAnchored(pending.BlastPrototype, pending.Grid, grid, pending.Tile);
                _detonatingTiles.Add((pending.Grid, pending.Tile));
                _detonatingBlasts[(pending.Grid, pending.Tile)] = pending;
                soundSource ??= pending;
            }

            _pendingBlasts.RemoveAt(i);
        }

        if (_detonatingTiles.Count == 0)
            return;

        if (soundSource != null && TryComp<MapGridComponent>(soundSource.Grid, out var soundGrid))
            PlayTile(soundSource.BlastSound, soundSource.Grid, soundGrid, soundSource.Tile, -4f);

        DamageDetonatingTiles();
        _detonatingTiles.Clear();
        _detonatingBlasts.Clear();
    }

    private void QueueRadiusBlasts(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i origin,
        int radius,
        string telegraphPrototype,
        TimeSpan telegraphAt,
        TimeSpan detonateAt,
        string blastPrototype,
        DamageSpecifier damage,
        EntityUid performer,
        SoundSpecifier blastSound,
        SoundSpecifier hitSound)
    {
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                QueueBlast(gridUid, grid, origin + new Vector2i(x, y), telegraphPrototype, telegraphAt, detonateAt, blastPrototype, damage, performer, blastSound, hitSound);
            }
        }
    }

    private void QueueBlast(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        string? telegraphPrototype,
        TimeSpan telegraphAt,
        TimeSpan detonateAt,
        string blastPrototype,
        DamageSpecifier damage,
        EntityUid performer,
        SoundSpecifier blastSound,
        SoundSpecifier hitSound)
    {
        if (_pendingBlasts.Count >= MaxPendingBlasts ||
            !CanAffectTile(gridUid, grid, tile) ||
            HasSimilarPendingBlast(gridUid, tile, detonateAt))
        {
            return;
        }

        _pendingBlasts.Add(new PendingBlast
        {
            Grid = gridUid,
            Tile = tile,
            TelegraphAt = telegraphAt,
            DetonateAt = detonateAt,
            TelegraphPrototype = telegraphPrototype,
            Telegraphed = telegraphPrototype == null,
            BlastPrototype = blastPrototype,
            Damage = new(damage),
            Performer = performer,
            BlastSound = blastSound,
            HitSound = hitSound,
        });
    }

    private bool HasSimilarPendingBlast(EntityUid gridUid, Vector2i tile, TimeSpan detonateAt)
    {
        foreach (var pending in _pendingBlasts)
        {
            if (pending.Grid != gridUid ||
                pending.Tile != tile)
            {
                continue;
            }

            if (Math.Abs((pending.DetonateAt - detonateAt).TotalSeconds) <= DuplicateBlastWindow)
                return true;
        }

        return false;
    }

    private void DamageDetonatingTiles()
    {
        var query = EntityQueryEnumerator<DamageableComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var damageable, out var mobState, out var xform))
        {
            if (mobState.CurrentState == MobState.Dead ||
                xform.GridUid is not { } gridUid ||
                !TryComp<MapGridComponent>(gridUid, out var grid))
            {
                continue;
            }

            var tile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
            if (!_detonatingTiles.Contains((gridUid, tile)) ||
                !_detonatingBlasts.TryGetValue((gridUid, tile), out var blast) ||
                uid == blast.Performer)
            {
                continue;
            }

            var origin = Exists(blast.Performer)
                ? blast.Performer
                : (EntityUid?) null;

            _damageable.TryChangeDamage((uid, damageable), blast.Damage, origin: origin);
            _audio.PlayPvs(blast.HitSound, uid, AudioParams.Default.WithVolume(-5f));
        }
    }

    private bool CanUseStaff(EntityUid staff, EntityUid performer)
    {
        return Exists(staff) &&
               Exists(performer) &&
               !IsDead(performer) &&
               TryComp<HandsComponent>(performer, out var hands) &&
               _hands.IsHolding((performer, hands), staff);
    }

    private bool TryGetWorldTargetTile(
        EntityUid performer,
        EntityCoordinates target,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i targetTile,
        out Vector2i performerTile)
    {
        targetTile = default;
        if (!TryGetPerformerTile(performer, out gridUid, out grid, out performerTile))
            return false;

        if (_transform.GetGrid(target) != gridUid)
            return false;

        targetTile = _map.LocalToTile(gridUid, grid, target);
        return CanAffectTile(gridUid, grid, targetTile);
    }

    private bool TryGetEntityTargetTiles(
        EntityUid performer,
        EntityUid target,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i performerTile,
        out Vector2i targetTile)
    {
        targetTile = default;
        if (!TryGetPerformerTile(performer, out gridUid, out grid, out performerTile))
            return false;

        return TryGetEntityTile(target, gridUid, grid, out targetTile);
    }

    private bool TryGetPerformerTile(EntityUid performer, out EntityUid gridUid, out MapGridComponent grid, out Vector2i tile)
    {
        gridUid = default;
        grid = default!;
        tile = default;

        if (!TryComp(performer, out TransformComponent? xform) ||
            xform.GridUid is not { } performerGrid ||
            !TryComp<MapGridComponent>(performerGrid, out var performerGridComp))
        {
            return false;
        }

        gridUid = performerGrid;
        grid = performerGridComp;
        tile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        return CanAffectTile(gridUid, grid, tile);
    }

    private bool TryGetEntityTile(EntityUid uid, EntityUid gridUid, MapGridComponent grid, out Vector2i tile)
    {
        tile = default;
        if (!TryComp(uid, out TransformComponent? xform) ||
            xform.GridUid != gridUid)
        {
            return false;
        }

        tile = _map.LocalToTile(gridUid, grid, xform.Coordinates);
        return CanAffectTile(gridUid, grid, tile);
    }

    private bool CanTeleportTo(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        return _map.TryGetTileRef(gridUid, grid, tile, out var tileRef) &&
               !tileRef.Tile.IsEmpty &&
               !_turf.IsSpace(tileRef) &&
               !_turf.IsTileBlocked(tileRef, CollisionGroup.MobMask);
    }

    private bool CanAffectTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        return _map.TryGetTileRef(gridUid, grid, tile, out var tileRef) &&
               !tileRef.Tile.IsEmpty &&
               !_turf.IsSpace(tileRef);
    }

    private bool IsDead(EntityUid uid)
    {
        return TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead;
    }

    private void SpawnAnchored(string prototype, EntityUid gridUid, MapGridComponent grid, Vector2i index)
    {
        var uid = Spawn(prototype, _map.GridTileToLocal(gridUid, grid, index));
        if (!TryComp(uid, out TransformComponent? xform) || xform.Anchored)
            return;

        _transform.AnchorEntity((uid, xform), (gridUid, grid), index);
    }

    private void PlayTile(SoundSpecifier sound, EntityUid gridUid, MapGridComponent grid, Vector2i index, float volume = 0f)
    {
        _audio.PlayPvs(sound, _map.GridTileToLocal(gridUid, grid, index), AudioParams.Default.WithVolume(volume));
    }

    private static Vector2i GetChaserDirection(Vector2i origin, Vector2i target)
    {
        var delta = target - origin;
        if (delta == Vector2i.Zero)
            return Vector2i.Zero;

        return Math.Abs(delta.X) > Math.Abs(delta.Y)
            ? new Vector2i(Math.Sign(delta.X), 0)
            : new Vector2i(0, Math.Sign(delta.Y));
    }

    private static TimeSpan ClampDelay(TimeSpan delay, TimeSpan fallback)
    {
        return delay <= TimeSpan.Zero
            ? fallback
            : delay;
    }

    private sealed class PendingBlast
    {
        public EntityUid Grid;
        public Vector2i Tile;
        public TimeSpan TelegraphAt;
        public TimeSpan DetonateAt;
        public string? TelegraphPrototype;
        public bool Telegraphed;
        public string BlastPrototype = string.Empty;
        public DamageSpecifier Damage = new();
        public EntityUid Performer;
        public SoundSpecifier BlastSound = default!;
        public SoundSpecifier HitSound = default!;
    }

    private sealed class PendingTeleport
    {
        public EntityUid Performer;
        public EntityUid Grid;
        public Vector2i Destination;
        public TimeSpan ExecuteAt;
    }

    private sealed class PendingChaser
    {
        public EntityUid Performer;
        public EntityUid Target;
        public EntityUid Grid;
        public Vector2i Tile;
        public TimeSpan NextStep;
        public TimeSpan StepDelay;
        public int MaxSteps;
        public int StepsTaken;
        public TimeSpan TelegraphDelay;
        public string SquaresPrototype = string.Empty;
        public string BlastPrototype = string.Empty;
        public DamageSpecifier Damage = new();
        public SoundSpecifier BlastSound = default!;
        public SoundSpecifier HitSound = default!;
    }
}
