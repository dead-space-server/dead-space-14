// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.HTN;
using Content.Shared.Camera;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Bosses;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The Legion's hands (<see cref="LavalandLegionHandsComponent"/>): they float by its sides trailing behind the head,
/// share its health, slam the floor to bring basalt down from the sky and hurl fauna or rocks at players who run.
/// </summary>
public sealed class LavalandLegionHandsSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementModStatusSystem _movementMod = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _camera = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly TimeSpan ShakeInterval = TimeSpan.FromSeconds(0.08);

    private readonly HashSet<Entity<LavalandFaunaComponent>> _fauna = new();
    private readonly HashSet<EntityUid> _nearby = new();
    private readonly List<EntityUid> _victims = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandLegionHandsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<LavalandLegionHandsComponent, LavalandBossResetEvent>(OnReset);
        SubscribeLocalEvent<LavalandLegionHandComponent, DamageChangedEvent>(OnHandDamaged);
        SubscribeLocalEvent<LavalandLegionHandComponent, ComponentShutdown>(OnHandShutdown);
    }

    #region Public

    /// <summary>A hand attack is going on: the Legion stands and lets it play out.</summary>
    public bool IsBusy(EntityUid legion)
    {
        return TryComp<LavalandLegionHandsComponent>(legion, out var hands) &&
               (_timing.CurTime < hands.BusyUntil || hands.Throw != null || hands.SlamAt != null || hands.Punch != null);
    }

    /// <summary>Tries one of the hand attacks at the target: the throw if it runs off, otherwise maybe the slam.</summary>
    public bool TryAttack(EntityUid legion, EntityUid target)
    {
        if (!TryComp<LavalandLegionHandsComponent>(legion, out var hands) || IsBusy(legion) ||
            hands.Left == null && hands.Right == null)
        {
            return false;
        }

        var distance = Vector2.Distance(_transform.GetWorldPosition(legion), _transform.GetWorldPosition(target));
        if (distance <= hands.PunchRange && _random.Prob(hands.PunchChance))
            return StartPunch((legion, hands), target);

        if (distance > hands.ThrowMinDistance && _random.Prob(hands.ThrowChance))
            return StartThrow((legion, hands), target);

        if (_random.Prob(hands.SlamChance))
        {
            StartSlam((legion, hands));
            return true;
        }

        return false;
    }

    /// <summary>
    /// The Legion splits: its hands stay as they are (their own health and damage) and go to the body that
    /// carries on the fight. A hand already destroyed stays destroyed.
    /// </summary>
    public void TransferHands(EntityUid from, EntityUid to)
    {
        if (!TryComp<LavalandLegionHandsComponent>(from, out var old) || TerminatingOrDeleted(to))
            return;

        ClearAttacks(old);
        if (TryComp<LavalandLegionHandsComponent>(to, out var existing))
            RemoveHands(existing);
        else
            existing = CopyComp(from, to, old);

        existing.Left = old.Left;
        existing.Right = old.Right;
        existing.LeftLost = old.LeftLost;
        existing.RightLost = old.RightLost;
        existing.BusyUntil = TimeSpan.Zero;

        foreach (var hand in new[] { existing.Left, existing.Right })
        {
            if (hand is { } uid && TryComp<LavalandLegionHandComponent>(uid, out var handComp))
                handComp.Legion = to;
        }

        // Handed over: the old body must not take them with it when it goes.
        old.Left = null;
        old.Right = null;
    }

    #endregion

    #region Hands

    private void EnsureHands(Entity<LavalandLegionHandsComponent> ent)
    {
        var coords = Transform(ent).Coordinates;
        if (ent.Comp.Left == null && !ent.Comp.LeftLost)
            ent.Comp.Left = SpawnHand(ent, ent.Comp.LeftHand, coords, true);
        if (ent.Comp.Right == null && !ent.Comp.RightLost)
            ent.Comp.Right = SpawnHand(ent, ent.Comp.RightHand, coords, false);
    }

    private EntityUid SpawnHand(Entity<LavalandLegionHandsComponent> ent, string prototype, EntityCoordinates coords, bool left)
    {
        var offset = new Vector2(left ? -ent.Comp.HandOffset.X : ent.Comp.HandOffset.X, ent.Comp.HandOffset.Y);
        var hand = Spawn(prototype, coords.Offset(offset));
        var comp = EnsureComp<LavalandLegionHandComponent>(hand);
        comp.Legion = ent.Owner;
        comp.IsLeft = left;
        return hand;
    }

    /// <summary>
    /// Both hands go up over its head and shake there for <paramref name="duration"/>: whatever they were doing
    /// is dropped.
    /// </summary>
    public void RaiseHandsInFright(EntityUid legion, TimeSpan duration)
    {
        if (!TryComp<LavalandLegionHandsComponent>(legion, out var hands))
            return;

        ClearAttacks(hands);
        hands.HandsUpUntil = _timing.CurTime + duration;
        foreach (var hand in new[] { hands.Left, hands.Right })
        {
            if (hand is { } open)
                SetPose(open, LavalandLegionHandPose.Open);
        }
    }

    private Vector2 RestSpot(Entity<LavalandLegionHandsComponent> ent, bool left, Vector2 legion, TimeSpan now)
    {
        var time = (float) now.TotalSeconds;
        if (ent.Comp.HandsUpUntil is { } upUntil && now < upUntil)
        {
            // Up over the head, trembling.
            var shake = new Vector2(MathF.Sin(time * 47f + (left ? 0f : 1.3f)), MathF.Cos(time * 39f + (left ? 0.7f : 0f))) * 0.09f;
            return legion + new Vector2(left ? -0.95f : 0.95f, 1.7f) + shake;
        }

        var bob = MathF.Sin(time * 2.2f + (left ? 0f : MathF.PI)) * ent.Comp.BobHeight;
        return legion + new Vector2(left ? -ent.Comp.HandOffset.X : ent.Comp.HandOffset.X, ent.Comp.HandOffset.Y + bob);
    }

    /// <summary>Hands not busy with an attack drift after their places by the sides, trailing behind.</summary>
    private void Follow(Entity<LavalandLegionHandsComponent> ent, Vector2 legion, TimeSpan now, float frameTime)
    {
        foreach (var (hand, left) in new[] { (ent.Comp.Left, true), (ent.Comp.Right, false) })
        {
            if (hand is not { } uid || TerminatingOrDeleted(uid) || IsMoving(ent.Comp, uid, now) ||
                ent.Comp.Throw?.Hand == uid || ent.Comp.Punch?.Hand == uid)
            {
                continue;
            }

            var target = RestSpot(ent, left, legion, now);
            var position = _transform.GetWorldPosition(uid);
            // Fell far behind (the Legion leapt): catch up at once.
            if (Vector2.DistanceSquared(position, target) > 12f * 12f)
            {
                SetHandPosition(uid, target);
                continue;
            }

            var k = 1f - MathF.Exp(-ent.Comp.FollowSharpness * frameTime);
            SetHandPosition(uid, Vector2.Lerp(position, target, k));
        }
    }

    private static bool IsMoving(LavalandLegionHandsComponent hands, EntityUid hand, TimeSpan now)
    {
        foreach (var move in hands.Moves)
        {
            if (move.Hand == hand && now >= move.Start && now <= move.End)
                return true;
        }

        return false;
    }

    private void Move(LavalandLegionHandsComponent hands, EntityUid hand, Vector2 from, Vector2 to, TimeSpan start, TimeSpan duration)
    {
        hands.Moves.Add(new LavalandLegionHandMove
        {
            Hand = hand,
            From = from,
            To = to,
            Start = start,
            End = start + duration,
        });
    }

    private void UpdateMoves(LavalandLegionHandsComponent hands, TimeSpan now)
    {
        for (var i = hands.Moves.Count - 1; i >= 0; i--)
        {
            var move = hands.Moves[i];
            if (TerminatingOrDeleted(move.Hand))
            {
                hands.Moves.RemoveAt(i);
                continue;
            }

            if (now < move.Start)
                continue;

            var length = (move.End - move.Start).TotalSeconds;
            var t = length <= 0 ? 1f : (float) Math.Clamp((now - move.Start).TotalSeconds / length, 0, 1);
            // Ease out: quick start, soft stop.
            var eased = 1f - (1f - t) * (1f - t);
            SetHandPosition(move.Hand, Vector2.Lerp(move.From, move.To, eased));
            if (now >= move.End)
                hands.Moves.RemoveAt(i);
        }
    }

    private void SetHandPosition(EntityUid hand, Vector2 position)
    {
        if (!CanMove(hand))
            return;

        _transform.SetWorldPosition(hand, position);
        if (TryComp<PhysicsComponent>(hand, out var body))
            _physics.SetLinearVelocity(hand, Vector2.Zero, body: body);
    }

    /// <summary>Blows at a hand hurt the Legion as well.</summary>
    private void OnHandDamaged(Entity<LavalandLegionHandComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta || TerminatingOrDeleted(ent.Comp.Legion))
            return;

        var forwarded = new DamageSpecifier();
        foreach (var (type, value) in delta.DamageDict)
        {
            if (value > FixedPoint2.Zero)
                forwarded.DamageDict[type] = value;
        }

        if (forwarded.DamageDict.Count > 0)
            _damageable.TryChangeDamage(ent.Comp.Legion, forwarded, true, false, args.Origin);
    }

    private void OnHandShutdown(Entity<LavalandLegionHandComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<LavalandLegionHandsComponent>(ent.Comp.Legion, out var hands) ||
            TerminatingOrDeleted(ent.Comp.Legion))
        {
            return;
        }

        // Destroyed: gone until the fight resets.
        if (hands.Left == ent.Owner)
        {
            hands.Left = null;
            hands.LeftLost = true;
        }

        if (hands.Right == ent.Owner)
        {
            hands.Right = null;
            hands.RightLost = true;
        }

        if (hands.Throw?.Hand == ent.Owner)
            DropThrow(hands);
    }

    private void OnReset(Entity<LavalandLegionHandsComponent> ent, ref LavalandBossResetEvent args)
    {
        ClearAttacks(ent.Comp);
        ent.Comp.LeftLost = false;
        ent.Comp.RightLost = false;
        foreach (var hand in new[] { ent.Comp.Left, ent.Comp.Right })
        {
            if (hand is { } uid && TryComp<DamageableComponent>(uid, out var damageable))
                _damageable.SetAllDamage((uid, damageable), FixedPoint2.Zero);
        }
    }

    private void OnShutdown(Entity<LavalandLegionHandsComponent> ent, ref ComponentShutdown args)
    {
        RemoveHands(ent.Comp);
    }

    private void RemoveHands(LavalandLegionHandsComponent hands)
    {
        ClearAttacks(hands);
        var left = hands.Left;
        var right = hands.Right;
        hands.Left = null;
        hands.Right = null;
        foreach (var hand in new[] { left, right })
        {
            if (hand is { } uid && !TerminatingOrDeleted(uid))
                QueueDel(uid);
        }
    }

    private void ClearAttacks(LavalandLegionHandsComponent hands)
    {
        DropThrow(hands);
        foreach (var rock in hands.Rocks)
        {
            if (rock.Rock is { } uid && !TerminatingOrDeleted(uid))
                QueueDel(uid);
        }

        hands.Rocks.Clear();
        hands.Moves.Clear();
        if (hands.Punch is { } punch)
            SetPose(punch.Hand, LavalandLegionHandPose.Open);
        hands.Punch = null;
        hands.Shaking.Clear();
        hands.SlamAt = null;
        hands.OpenHandsAt = null;
        foreach (var hand in new[] { hands.Left, hands.Right })
        {
            if (hand is { } open)
                SetPose(open, LavalandLegionHandPose.Open);
        }

        hands.BusyUntil = TimeSpan.Zero;
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandLegionHandsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var hands, out var xform))
        {
            var ent = (uid, hands);
            if (_mobState.IsDead(uid))
            {
                // The hands fall apart with the Legion.
                if (hands.Left != null || hands.Right != null)
                    RemoveHands(hands);
                continue;
            }

            EnsureHands(ent);
            var legion = _transform.GetWorldPosition(xform);
            UpdateMoves(hands, now);
            Follow(ent, legion, now, frameTime);

            if (hands.SlamAt is { } slamAt && now >= slamAt)
                Slam(ent, xform, now);

            if (hands.OpenHandsAt is { } openAt && now >= openAt)
            {
                hands.OpenHandsAt = null;
                foreach (var hand in new[] { hands.Left, hands.Right })
                {
                    if (hand is { } open && hands.Punch?.Hand != open)
                        SetPose(open, LavalandLegionHandPose.Open);
                }
            }

            UpdateRocks(hands, uid, now);
            UpdateThrow(ent, legion, now);
            UpdatePunch(ent, legion, now);
            UpdateShake(hands, now);
        }
    }

    #region Slam

    /// <summary>Both fists go up over the head, then down onto the floor.</summary>
    private void StartSlam(Entity<LavalandLegionHandsComponent> ent)
    {
        var now = _timing.CurTime;
        var windup = ent.Comp.SlamWindup;
        var legion = _transform.GetWorldPosition(ent);
        foreach (var (hand, left) in new[] { (ent.Comp.Left, true), (ent.Comp.Right, false) })
        {
            if (hand is not { } uid)
                continue;

            var side = left ? -1f : 1f;
            var from = _transform.GetWorldPosition(uid);
            var raised = legion + new Vector2(side * 1.1f, 1.6f);
            // Clenched into fists for the blow.
            SetPose(uid, LavalandLegionHandPose.Fist);
            var floor = legion + new Vector2(side * 1f, -0.5f);
            Move(ent.Comp, uid, from, raised, now, windup * 0.7);
            Move(ent.Comp, uid, raised, floor, now + windup * 0.7, windup * 0.3);
        }

        ent.Comp.SlamAt = now + windup;
        ent.Comp.BusyUntil = now + windup + TimeSpan.FromSeconds(0.6);
    }

    /// <summary>The floor is hit: screens shake and basalt starts falling by the Legion and by every player.</summary>
    private void Slam(Entity<LavalandLegionHandsComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var hands = ent.Comp;
        hands.SlamAt = null;
        hands.OpenHandsAt = now + TimeSpan.FromSeconds(0.4);
        if (hands.SlamSound != null)
            _audio.PlayPvs(hands.SlamSound, ent);

        var center = _transform.GetMapCoordinates(xform);
        for (var i = 0; i < hands.RocksByLegion; i++)
        {
            var offset = _random.NextAngle().ToVec() * _random.NextFloat(1.5f, MathF.Max(1.5f, hands.RockRadiusByLegion));
            QueueRock(hands, new MapCoordinates(center.Position + offset, center.MapId), now);
        }

        foreach (var player in PlayersNear(center, hands.ShakeRange))
        {
            hands.Shaking.Add((player, now + hands.ShakeTime));
            var spot = _transform.GetMapCoordinates(player);
            for (var i = 0; i < hands.RocksByPlayer; i++)
            {
                // The first one right on the head.
                var offset = i == 0
                    ? Vector2.Zero
                    : _random.NextAngle().ToVec() * _random.NextFloat(0.8f, MathF.Max(0.8f, hands.RockRadiusByPlayer));
                QueueRock(hands, new MapCoordinates(spot.Position + offset, spot.MapId), now);
            }
        }
    }

    private void QueueRock(LavalandLegionHandsComponent hands, MapCoordinates spot, TimeSpan now)
    {
        var start = now + TimeSpan.FromSeconds(_random.NextDouble() * hands.RockSpread.TotalSeconds);
        hands.Rocks.Add(new LavalandLegionFallingRockState
        {
            Spot = spot,
            StartAt = start,
            LandAt = start + hands.RockFallTime,
        });
    }

    private void UpdateRocks(LavalandLegionHandsComponent hands, EntityUid legion, TimeSpan now)
    {
        for (var i = hands.Rocks.Count - 1; i >= 0; i--)
        {
            var rock = hands.Rocks[i];
            if (!rock.Started)
            {
                if (now < rock.StartAt)
                    continue;

                // The mark shows where it lands; the rock drops from the sky onto it.
                rock.Started = true;
                var mark = Spawn(hands.Mark, rock.Spot);
                EnsureComp<TimedDespawnComponent>(mark).Lifetime = (float) hands.RockFallTime.TotalSeconds;

                var falling = Spawn(hands.FallingRock, rock.Spot);
                var drop = EnsureComp<LavalandSkyDropComponent>(falling);
                drop.Start = now;
                drop.Duration = (float) hands.RockFallTime.TotalSeconds;
                drop.Height = 10f;
                drop.Rising = false;
                Dirty(falling, drop);
                rock.Rock = falling;
                continue;
            }

            if (now < rock.LandAt)
                continue;

            hands.Rocks.RemoveAt(i);
            if (rock.Rock is { } rockUid && !TerminatingOrDeleted(rockUid))
                QueueDel(rockUid);

            if (HitPlayers(hands, legion, rock.Spot, hands.RockHitRadius))
            {
                // Broke on someone's head.
                Shatter(hands, rock.Spot);
                continue;
            }

            // Missed: a basalt rock stands where it fell, to be torn out and thrown later -
            // unless it would wall in the Legion or somebody standing there.
            if (!CanRaiseRock(hands, legion, rock.Spot))
            {
                Shatter(hands, rock.Spot);
                continue;
            }

            var wall = Spawn(hands.Boulder, _transform.ToCoordinates(rock.Spot).SnapToGrid(EntityManager));
            if (hands.RockLandSound != null)
                _audio.PlayPvs(hands.RockLandSound, wall);
        }
    }

    private void UpdateShake(LavalandLegionHandsComponent hands, TimeSpan now)
    {
        if (hands.Shaking.Count == 0 || now < hands.NextShake)
            return;

        hands.NextShake = now + ShakeInterval;
        for (var i = hands.Shaking.Count - 1; i >= 0; i--)
        {
            var (player, until) = hands.Shaking[i];
            if (now >= until || TerminatingOrDeleted(player))
            {
                hands.Shaking.RemoveAt(i);
                continue;
            }

            // Dies down towards the end.
            var left = (float) ((until - now).TotalSeconds / Math.Max(0.1, hands.ShakeTime.TotalSeconds));
            _camera.KickCamera(player, _random.NextAngle().ToVec() * hands.ShakeStrength * (0.3f + 0.7f * left));
        }
    }

    #endregion

    #region Punch

    /// <summary>The hand on the target's side clenches into a fist and draws back; the target's spot is marked.</summary>
    private bool StartPunch(Entity<LavalandLegionHandsComponent> ent, EntityUid target)
    {
        var legion = _transform.GetWorldPosition(ent);
        var targetCoords = _transform.GetMapCoordinates(target);
        var useLeft = targetCoords.Position.X < legion.X;
        var hand = useLeft ? ent.Comp.Left ?? ent.Comp.Right : ent.Comp.Right ?? ent.Comp.Left;
        if (hand is not { } handUid)
            return false;

        var now = _timing.CurTime;
        var hands = ent.Comp;
        var left = TryComp<LavalandLegionHandComponent>(handUid, out var handComp) && handComp.IsLeft;
        var away = Vector2.Normalize(legion - targetCoords.Position + new Vector2(0.001f, 0f));
        var drawnBack = legion + new Vector2(left ? -1.3f : 1.3f, 0.6f) + away * 0.8f;

        SetPose(handUid, LavalandLegionHandPose.Fist);
        Move(hands, handUid, _transform.GetWorldPosition(handUid), drawnBack, now, hands.PunchWindup);

        var mark = Spawn(hands.Mark, targetCoords);
        EnsureComp<TimedDespawnComponent>(mark).Lifetime = (float) (hands.PunchWindup + hands.PunchStrike).TotalSeconds;

        hands.Punch = new LavalandLegionPunchState
        {
            Stage = LavalandLegionPunchStage.Windup,
            StageEnd = now + hands.PunchWindup,
            Hand = handUid,
            Spot = targetCoords,
        };
        return true;
    }

    private void UpdatePunch(Entity<LavalandLegionHandsComponent> ent, Vector2 legion, TimeSpan now)
    {
        var hands = ent.Comp;
        if (hands.Punch is not { } punch)
            return;

        if (TerminatingOrDeleted(punch.Hand))
        {
            hands.Punch = null;
            return;
        }

        if (now < punch.StageEnd)
            return;

        switch (punch.Stage)
        {
            case LavalandLegionPunchStage.Windup:
                // Knuckles first, straight into the marked spot.
                SetPose(punch.Hand, LavalandLegionHandPose.Punch);
                Move(hands, punch.Hand, _transform.GetWorldPosition(punch.Hand), punch.Spot.Position, now, hands.PunchStrike);
                punch.Stage = LavalandLegionPunchStage.Strike;
                punch.StageEnd = now + hands.PunchStrike;
                break;

            case LavalandLegionPunchStage.Strike:
                if (hands.PunchSound != null)
                    _audio.PlayPvs(hands.PunchSound, punch.Hand);
                Punch(hands, ent.Owner, punch.Spot, legion);
                punch.Stage = LavalandLegionPunchStage.Hold;
                punch.StageEnd = now + hands.PunchHold;
                break;

            case LavalandLegionPunchStage.Hold:
                // The fist opens and the hand floats back to its place.
                SetPose(punch.Hand, LavalandLegionHandPose.Open);
                hands.Punch = null;
                break;
        }
    }

    /// <summary>Whoever is still on the marked spot takes the blow and is knocked back, away from the Legion.</summary>
    private void Punch(LavalandLegionHandsComponent hands, EntityUid legion, MapCoordinates spot, Vector2 legionPosition)
    {
        foreach (var player in PlayersNear(spot, hands.PunchHitRadius))
        {
            _damageable.TryChangeDamage(player, hands.PunchDamage, origin: legion);
            var away = _transform.GetWorldPosition(player) - legionPosition;
            if (away.LengthSquared() < 0.01f)
                away = _random.NextAngle().ToVec();

            var direction = Vector2.Normalize(away);
            if (TryComp<PhysicsComponent>(player, out var body))
                _physics.ApplyLinearImpulse(player, direction * hands.PunchKnockback * body.Mass, body: body);
            _camera.KickCamera(player, direction * 0.8f);
        }
    }

    private void SetPose(EntityUid hand, LavalandLegionHandPose pose)
    {
        if (!TerminatingOrDeleted(hand))
            _appearance.SetData(hand, LavalandLegionHandVisuals.Pose, pose);
    }

    #endregion

    #region Throw

    /// <summary>A hand reaches for the nearest fauna, else a basalt boulder, else tears a rock out of the floor.</summary>
    private bool StartThrow(Entity<LavalandLegionHandsComponent> ent, EntityUid target)
    {
        var legionCoords = _transform.GetMapCoordinates(ent);
        var (held, isMob) = FindThrowable(ent, legionCoords);
        var heldPosition = held is { } h ? _transform.GetWorldPosition(h) : (Vector2?) null;

        // The hand on the side of what it grabs; for a torn-out rock, the side of the target.
        var towards = heldPosition ?? _transform.GetWorldPosition(target);
        var useLeft = towards.X < legionCoords.Position.X;
        var hand = useLeft ? ent.Comp.Left ?? ent.Comp.Right : ent.Comp.Right ?? ent.Comp.Left;
        if (hand is not { } handUid)
            return false;

        var reachTo = heldPosition ??
                      legionCoords.Position + Vector2.Normalize(towards - legionCoords.Position + new Vector2(0.001f, 0f)) * 1.5f;

        var now = _timing.CurTime;
        var from = _transform.GetWorldPosition(handUid);
        Move(ent.Comp, handUid, from, reachTo, now, ent.Comp.ReachTime);
        ent.Comp.Throw = new LavalandLegionThrowState
        {
            Stage = LavalandLegionThrowStage.Reach,
            StageEnd = now + ent.Comp.ReachTime,
            Hand = handUid,
            Target = target,
            Held = held,
            HeldIsMob = isMob,
            HandTo = reachTo,
        };

        if (held is { } grabbed && isMob)
            SetAi(grabbed, false);
        return true;
    }

    private (EntityUid? Held, bool IsMob) FindThrowable(Entity<LavalandLegionHandsComponent> ent, MapCoordinates legion)
    {
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        _fauna.Clear();
        _lookup.GetEntitiesInRange(legion, ent.Comp.GrabRange, _fauna);
        foreach (var fauna in _fauna)
        {
            if (fauna.Owner == ent.Owner || HasComp<LavalandLegionMegafaunaComponent>(fauna) ||
                HasComp<LavalandBossComponent>(fauna) || _mobState.IsDead(fauna))
            {
                continue;
            }

            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(fauna), legion.Position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = fauna;
        }

        if (best != null)
            return (best, true);

        _nearby.Clear();
        _lookup.GetEntitiesInRange(legion.MapId, legion.Position, ent.Comp.GrabRange, _nearby, LookupFlags.Static);
        foreach (var uid in _nearby)
        {
            // Plain basalt rock only: no ore veins, never the indestructible edge of lavaland.
            if (MetaData(uid).EntityPrototype?.ID != ent.Comp.Boulder.Id)
                continue;

            var distance = Vector2.DistanceSquared(_transform.GetWorldPosition(uid), legion.Position);
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = uid;
        }

        return (best, false);
    }

    private void UpdateThrow(Entity<LavalandLegionHandsComponent> ent, Vector2 legion, TimeSpan now)
    {
        if (ent.Comp.Throw is not { } state)
            return;

        var hands = ent.Comp;
        if (TerminatingOrDeleted(state.Hand) || state.Held is { } gone && TerminatingOrDeleted(gone))
        {
            DropThrow(hands);
            return;
        }

        // Once grabbed, what is held goes with the hand until it is thrown.
        if (state.Held is { } held && state.Stage is LavalandLegionThrowStage.Pull or LavalandLegionThrowStage.Aim)
            HoldAt(held, _transform.GetWorldPosition(state.Hand));

        if (now < state.StageEnd)
        {
            if (state.Stage == LavalandLegionThrowStage.Flight && state.Held is { } flying)
            {
                var t = (float) Math.Clamp(1 - (state.StageEnd - now).TotalSeconds / hands.FlightTime.TotalSeconds, 0, 1);
                HoldAt(flying, Vector2.Lerp(state.FlightFrom, state.AimSpot.Position, t));
            }

            return;
        }

        switch (state.Stage)
        {
            case LavalandLegionThrowStage.Reach:
                // Grabs it - or tears a rock out of the floor or out of a basalt wall - and pulls it back.
                if (state.Held is { } wallRock && !state.HeldIsMob)
                {
                    var rockAt = _transform.GetMapCoordinates(wallRock);
                    QueueDel(wallRock);
                    state.Held = Spawn(hands.FallingRock, rockAt);
                    Shatter(hands, rockAt);
                }
                else if (state.Held == null)
                {
                    state.Held = Spawn(hands.FallingRock, new MapCoordinates(state.HandTo, Transform(ent).MapID));
                }

                // Gripping it tight while pulling back and aiming.
                SetPose(state.Hand, LavalandLegionHandPose.Punch);
                var left = TryComp<LavalandLegionHandComponent>(state.Hand, out var handComp) && handComp.IsLeft;
                var back = legion + new Vector2(left ? -1.3f : 1.3f, 1.4f);
                Move(hands, state.Hand, _transform.GetWorldPosition(state.Hand), back, now, hands.PullTime);
                state.Stage = LavalandLegionThrowStage.Pull;
                state.StageEnd = now + hands.PullTime;
                break;

            case LavalandLegionThrowStage.Pull:
                // The mark lies where it will land: where the target stands now.
                if (TerminatingOrDeleted(state.Target))
                {
                    DropThrow(hands);
                    return;
                }

                state.AimSpot = _transform.GetMapCoordinates(state.Target);
                var mark = Spawn(hands.Mark, state.AimSpot);
                EnsureComp<TimedDespawnComponent>(mark).Lifetime = (float) (hands.AimTime + hands.FlightTime).TotalSeconds;
                state.Stage = LavalandLegionThrowStage.Aim;
                state.StageEnd = now + hands.AimTime;
                break;

            case LavalandLegionThrowStage.Aim:
                if (state.Held is not { } thrown)
                {
                    DropThrow(hands);
                    return;
                }

                state.FlightFrom = _transform.GetWorldPosition(thrown);

                // Lets go: the hand spreads wide and swings forward after it, as a throw does.
                SetPose(state.Hand, LavalandLegionHandPose.Throw);
                var handAt = _transform.GetWorldPosition(state.Hand);
                var swing = state.AimSpot.Position - handAt;
                if (swing.LengthSquared() > 0.01f)
                {
                    Move(hands, state.Hand, handAt, handAt + Vector2.Normalize(swing) * 1.5f, now,
                        TimeSpan.FromSeconds(0.2));
                }
                var motion = EnsureComp<LavalandSpriteMotionComponent>(thrown);
                motion.Start = now;
                motion.Duration = (float) hands.FlightTime.TotalSeconds;
                motion.From = Vector2.Zero;
                motion.To = Vector2.Zero;
                motion.Arc = 1.8f;
                motion.Spin = state.HeldIsMob ? 9f : 6f;
                motion.EndRotation = 0f;
                Dirty(thrown, motion);
                if (hands.ThrowSound != null)
                    _audio.PlayPvs(hands.ThrowSound, ent);

                state.Stage = LavalandLegionThrowStage.Flight;
                state.StageEnd = now + hands.FlightTime;
                break;

            case LavalandLegionThrowStage.Flight:
                Land(ent, state);
                break;
        }
    }

    private void Land(Entity<LavalandLegionHandsComponent> ent, LavalandLegionThrowState state)
    {
        var hands = ent.Comp;
        hands.Throw = null;
        SetPose(state.Hand, LavalandLegionHandPose.Open);
        if (state.Held is not { } held)
            return;

        HoldAt(held, state.AimSpot.Position);
        RemComp<LavalandSpriteMotionComponent>(held);
        HitPlayers(hands, ent.Owner, state.AimSpot, hands.ThrowHitRadius);

        if (state.HeldIsMob)
        {
            // The thrown fauna gets up and goes on.
            SetAi(held, true);
            return;
        }

        QueueDel(held);
        Shatter(hands, state.AimSpot);
    }

    /// <summary>Lets go of whatever was held: the fauna comes to itself, the rock stays where it is.</summary>
    private void DropThrow(LavalandLegionHandsComponent hands)
    {
        if (hands.Throw is not { } state)
            return;

        hands.Throw = null;
        SetPose(state.Hand, LavalandLegionHandPose.Open);
        if (state.Held is not { } held || TerminatingOrDeleted(held))
            return;

        RemComp<LavalandSpriteMotionComponent>(held);
        if (state.HeldIsMob)
            SetAi(held, true);
    }

    /// <summary>Has a valid place in the world: not being deleted, not in nullspace.</summary>
    private bool CanMove(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        var xform = Transform(uid);
        return xform.ParentUid.IsValid() && !TerminatingOrDeleted(xform.ParentUid) && xform.MapID != MapId.Nullspace;
    }

    private void HoldAt(EntityUid uid, Vector2 position)
    {
        if (!CanMove(uid))
            return;

        _transform.SetWorldPosition(uid, position);
        if (TryComp<PhysicsComponent>(uid, out var body))
            _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);
    }

    #endregion

    #region Hits

    /// <summary>30 brute on the head and a slowdown for everyone under the falling thing. True if anyone was hit.</summary>
    private bool HitPlayers(LavalandLegionHandsComponent hands, EntityUid legion, MapCoordinates spot, float radius)
    {
        _victims.Clear();
        foreach (var player in PlayersNear(spot, radius))
        {
            _victims.Add(player);
        }

        foreach (var victim in _victims)
        {
            _damageable.TryChangeDamage(victim, hands.RockDamage, origin: legion);
            _movementMod.TryUpdateMovementSpeedModDuration(victim, hands.SlowEffect, hands.SlowTime, hands.SlowMultiplier);
        }

        return _victims.Count > 0;
    }

    /// <summary>A rock may stand here: away from the Legion, not on a mob, not on another wall.</summary>
    private bool CanRaiseRock(LavalandLegionHandsComponent hands, EntityUid legion, MapCoordinates spot)
    {
        var legionCoords = _transform.GetMapCoordinates(legion);
        if (legionCoords.MapId == spot.MapId &&
            Vector2.DistanceSquared(legionCoords.Position, spot.Position) < hands.NoWallNearLegion * hands.NoWallNearLegion)
        {
            return false;
        }

        var tileCenter = _transform.ToMapCoordinates(_transform.ToCoordinates(spot).SnapToGrid(EntityManager));
        foreach (var uid in _lookup.GetEntitiesInRange(tileCenter, 0.6f, LookupFlags.Static | LookupFlags.Dynamic))
        {
            if (HasComp<MobStateComponent>(uid) ||
                TryComp<PhysicsComponent>(uid, out var body) && body.Hard && body.CanCollide && Transform(uid).Anchored)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The rock bursts: chips fly off to all sides.</summary>
    private void Shatter(LavalandLegionHandsComponent hands, MapCoordinates spot)
    {
        if (hands.RockLandSound != null)
            _audio.PlayPvs(hands.RockLandSound, _transform.ToCoordinates(spot));

        for (var i = 0; i < 7; i++)
        {
            var chip = Spawn(hands.RockParticle, spot);
            var motion = EnsureComp<LavalandSpriteMotionComponent>(chip);
            motion.Start = _timing.CurTime;
            motion.Duration = 0.55f;
            motion.From = Vector2.Zero;
            motion.To = _random.NextAngle().ToVec() * _random.NextFloat(0.8f, 1.6f);
            motion.Arc = _random.NextFloat(0.3f, 0.8f);
            motion.Spin = _random.NextFloat(-14f, 14f);
            motion.FadeOut = true;
            Dirty(chip, motion);
        }
    }

    private IEnumerable<EntityUid> PlayersNear(MapCoordinates spot, float range)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player || HasComp<GhostComponent>(player) ||
                !HasComp<MobStateComponent>(player) || !_mobState.IsAlive(player))
            {
                continue;
            }

            var coords = _transform.GetMapCoordinates(player);
            if (coords.MapId == spot.MapId && Vector2.DistanceSquared(coords.Position, spot.Position) <= range * range)
                yield return player;
        }
    }

    #endregion

    private void SetAi(EntityUid uid, bool enabled)
    {
        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.SetHTNEnabled((uid, htn), enabled, enabled ? 0.5f : 0f);
    }
}
