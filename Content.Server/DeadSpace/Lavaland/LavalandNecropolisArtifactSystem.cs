// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Clothing;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.DeadSpace.ThermalVision;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Necropolis chest artifacts ported from tgstation.
/// </summary>
public sealed class LavalandNecropolisArtifactSystem : EntitySystem
{
    private const string HoleContainer = "reality-hole";

    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly LavalandDayCycleSystem _dayCycle = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly List<EntityUid> _toRelease = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LavalandWispLanternComponent, UseInHandEvent>(OnLanternUse);
        SubscribeLocalEvent<LavalandWarpCubeComponent, MapInitEvent>(OnCubeMapInit);
        SubscribeLocalEvent<LavalandWarpCubeComponent, UseInHandEvent>(OnCubeUse);
        SubscribeLocalEvent<LavalandWarpCubeComponent, ComponentShutdown>(OnCubeShutdown);
        SubscribeLocalEvent<LavalandImmortalityTalismanComponent, UseInHandEvent>(OnTalismanUse);
        SubscribeLocalEvent<LavalandParadoxBagComponent, MapInitEvent>(OnParadoxMapInit);
        SubscribeLocalEvent<LavalandParadoxBagComponent, ComponentShutdown>(OnParadoxShutdown);
        Subs.BuiEvents<LavalandParadoxBagComponent>(StorageComponent.StorageUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnParadoxOpened);
        });
        SubscribeLocalEvent<LavalandRealityHoleComponent, ComponentShutdown>(OnHoleShutdown);
        SubscribeLocalEvent<LavalandShipInBottleComponent, UseInHandEvent>(OnBottleUse);
        SubscribeLocalEvent<LavalandShipInBottleComponent, AfterInteractEvent>(OnBottleInteract);
        SubscribeLocalEvent<LavalandPotionComponent, UseInHandEvent>(OnPotionUse);

        SubscribeLocalEvent<LavalandRodOfAsclepiusComponent, UseInHandEvent>(OnRodUse);
        SubscribeLocalEvent<LavalandRodOfAsclepiusComponent, LavalandAsclepiusOathDoAfterEvent>(OnOathLine);

        SubscribeLocalEvent<LavalandMementoMoriComponent, LavalandMementoMoriActionEvent>(OnMementoAction);
        SubscribeLocalEvent<LavalandMementoMoriComponent, LavalandMementoMoriDoAfterEvent>(OnMementoLinked);
        SubscribeLocalEvent<LavalandMementoMoriComponent, GotUnequippedEvent>(OnMementoUnequipped);
        SubscribeLocalEvent<LavalandMementoMoriComponent, ComponentShutdown>(OnMementoShutdown);

        SubscribeLocalEvent<LavalandBerserkerSuitComponent, GotEquippedEvent>(OnBerserkerEquipped);
        SubscribeLocalEvent<LavalandBerserkerSuitComponent, GotUnequippedEvent>(OnBerserkerUnequipped);
        SubscribeLocalEvent<LavalandBerserkerSuitComponent, GetItemActionsEvent>(OnBerserkerGetActions);
        SubscribeLocalEvent<LavalandBerserkerSuitComponent, LavalandBerserkActionEvent>(OnBerserkAction);
        SubscribeLocalEvent<LavalandBerserkerSuitComponent, ExaminedEvent>(OnBerserkerExamined);
        SubscribeLocalEvent<LavalandBerserkerWearerComponent, DamageChangedEvent>(OnBerserkerDamaged);
        SubscribeLocalEvent<LavalandBerserkerWearerComponent, DamageModifyEvent>(OnBerserkerDamageModify);
        SubscribeLocalEvent<LavalandBerserkerWearerComponent, RefreshMovementSpeedModifiersEvent>(OnBerserkerSpeed);
        SubscribeLocalEvent<LavalandBerserkerWearerComponent, ShotAttemptedEvent>(OnBerserkerShot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        UpdateWispGuides(frameTime);

        _toRelease.Clear();
        var holes = EntityQueryEnumerator<LavalandRealityHoleComponent>();
        while (holes.MoveNext(out var uid, out var hole))
        {
            if (now >= hole.ReleaseAt)
                _toRelease.Add(uid);
        }
        foreach (var hole in _toRelease)
        {
            // Out first: deleting the hole deletes whatever its container still holds - the user too.
            if (TryComp<LavalandRealityHoleComponent>(hole, out var holeComp))
                ReleaseFromHole((hole, holeComp));
            QueueDel(hole);
        }

        var rods = EntityQueryEnumerator<LavalandRodOfAsclepiusComponent>();
        while (rods.MoveNext(out _, out var rod))
            UpdateRod(rod, now);

        var suits = EntityQueryEnumerator<LavalandBerserkerSuitComponent>();
        while (suits.MoveNext(out var uid, out var suit))
        {
            if (!suit.Active)
                continue;

            suit.Charge = Math.Max(0f, suit.Charge - suit.DrainPerSecond * frameTime);
            if (suit.Charge <= 0f)
                SetBerserk((uid, suit), false);
        }
    }

    #region Wisp lantern

    private void OnLanternUse(Entity<LavalandWispLanternComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Wisp is { } wisp && !TerminatingOrDeleted(wisp))
        {
            QueueDel(wisp);
            ent.Comp.Wisp = null;
            ent.Comp.Guiding = false;
            ent.Comp.GuideWaiting = false;
            if (ent.Comp.AddedThermalVision && ent.Comp.Holder is { } holder)
                RemComp<ThermalVisionComponent>(holder);
            ent.Comp.AddedThermalVision = false;
            ent.Comp.Holder = null;
            _appearance.SetData(ent, LavalandArtifactVisuals.Charged, true);
            _item.SetHeldPrefix(ent, null);
            _popup.PopupEntity(Loc.GetString("lavaland-wisp-lantern-return"), args.User, args.User);
            return;
        }

        var user = args.User;
        // Free floating, not parented to the owner: it drifts after them by itself.
        var userCoords = _transform.GetMapCoordinates(user);
        ent.Comp.Wisp = Spawn(ent.Comp.WispPrototype, userCoords.Offset(new Vector2(0f, 0.6f)));
        _transform.SetMapCoordinates(ent.Comp.Wisp.Value, userCoords.Offset(new Vector2(0f, 0.6f)));
        ent.Comp.FollowPhase = _random.NextFloat(0f, MathF.Tau);
        ent.Comp.Holder = user;
        if (ent.Comp.GrantThermalVision && !HasComp<ThermalVisionComponent>(user))
        {
            var thermal = new ThermalVisionComponent { IsActive = true };
            AddComp(user, thermal);
            Dirty(user, thermal);
            ent.Comp.AddedThermalVision = true;
        }

        _appearance.SetData(ent, LavalandArtifactVisuals.Charged, false);
        _item.SetHeldPrefix(ent, "empty");
        _popup.PopupEntity(Loc.GetString("lavaland-wisp-lantern-release"), user, user);
    }

    /// <summary>In guiding day phases (deep night) released wisps fly toward the outpost, waiting for their owner.</summary>
    private void UpdateWispGuides(float frameTime)
    {
        var query = EntityQueryEnumerator<LavalandWispLanternComponent>();
        while (query.MoveNext(out var uid, out var lantern))
        {
            if (lantern.Wisp is not { } wisp || TerminatingOrDeleted(wisp) ||
                lantern.Holder is not { } holder || TerminatingOrDeleted(holder))
            {
                continue;
            }

            var holderXform = Transform(holder);
            if (!_dayCycle.IsWispGuiding(holderXform.MapUid) ||
                !TryGetOutpostCenter(holderXform.MapUid, out var outpost))
            {
                if (lantern.Guiding)
                    ReturnWisp(lantern);
                FollowHolder(lantern, wisp, holderXform, frameTime);
                continue;
            }

            var holderPos = _transform.GetWorldPosition(holderXform);
            if (!lantern.Guiding)
            {
                _transform.SetMapCoordinates(wisp, _transform.GetMapCoordinates(wisp));
                lantern.Guiding = true;
                lantern.GuideWaiting = false;
                _popup.PopupEntity(Loc.GetString("lavaland-wisp-guide-start"), holder, holder, PopupType.Medium);
            }

            var wispPos = _transform.GetWorldPosition(wisp);
            var toHolder = (holderPos - wispPos).Length();
            if (toHolder > lantern.GuideLostDistance || Transform(wisp).MapID != holderXform.MapID)
            {
                _transform.SetMapCoordinates(wisp, new MapCoordinates(holderPos + new Vector2(0f, 0.6f), holderXform.MapID));
                continue;
            }

            if (lantern.GuideWaiting)
            {
                if (toHolder > lantern.GuideResumeDistance)
                    continue;
                lantern.GuideWaiting = false;
            }
            else if (toHolder > lantern.GuideWaitDistance)
            {
                lantern.GuideWaiting = true;
                continue;
            }

            var toBase = outpost - wispPos;
            var distance = toBase.Length();
            if (distance <= lantern.GuideArriveDistance)
                continue;

            var step = MathF.Min(lantern.GuideSpeed * frameTime, distance);
            _transform.SetWorldPosition(wisp, wispPos + toBase / distance * step);
        }
    }

    private void ReturnWisp(LavalandWispLanternComponent lantern)
    {
        lantern.Guiding = false;
        lantern.GuideWaiting = false;
    }

    /// <summary>
    /// The wisp hovers around its owner on its own: it slowly circles them, bobs, and lags behind when they move,
    /// catching up smoothly. Lost (teleport, another map) - it pops back next to them.
    /// </summary>
    private void FollowHolder(LavalandWispLanternComponent lantern, EntityUid wisp, TransformComponent holderXform, float frameTime)
    {
        var holderPos = _transform.GetWorldPosition(holderXform);
        var wispXform = Transform(wisp);

        lantern.FollowPhase = (lantern.FollowPhase + lantern.FollowOrbitSpeed * frameTime) % MathF.Tau;
        var orbit = new Vector2(MathF.Cos(lantern.FollowPhase), MathF.Sin(lantern.FollowPhase) * 0.6f) * lantern.FollowRadius;
        var bob = new Vector2(0f, 0.4f + MathF.Sin(lantern.FollowPhase * 3f) * lantern.FollowBob);
        var target = holderPos + orbit + bob;

        if (wispXform.MapID != holderXform.MapID || wispXform.ParentUid != wispXform.MapUid ||
            (target - _transform.GetWorldPosition(wispXform)).Length() > lantern.GuideLostDistance)
        {
            _transform.SetMapCoordinates(wisp, new MapCoordinates(target, holderXform.MapID));
            return;
        }

        var wispPos = _transform.GetWorldPosition(wispXform);
        var t = 1f - MathF.Exp(-lantern.FollowSharpness * frameTime);
        _transform.SetWorldPosition(wisp, wispPos + (target - wispPos) * t);
    }

    private bool TryGetOutpostCenter(EntityUid? map, out Vector2 center)
    {
        center = default;
        if (map == null)
            return false;

        var outposts = EntityQueryEnumerator<LavalandOutpostComponent, MapGridComponent, TransformComponent>();
        while (outposts.MoveNext(out var gridUid, out _, out var grid, out var xform))
        {
            if (xform.MapUid != map)
                continue;

            center = Vector2.Transform(grid.LocalAABB.Center, _transform.GetWorldMatrix(xform));
            return true;
        }

        return false;
    }

    #endregion

    #region Warp cubes

    private void OnCubeMapInit(Entity<LavalandWarpCubeComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SpawnPair is not { } pair || ent.Comp.Linked != null)
            return;

        var other = Spawn(pair, Transform(ent).Coordinates);
        var otherComp = EnsureComp<LavalandWarpCubeComponent>(other);
        otherComp.Linked = ent.Owner;
        ent.Comp.Linked = other;
    }

    private void OnCubeUse(Entity<LavalandWarpCubeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Linked is not { } linked || TerminatingOrDeleted(linked) ||
            HasComp<LavalandNoTeleportZoneComponent>(Transform(args.User).MapUid) ||
            HasComp<LavalandNoTeleportZoneComponent>(Transform(linked).MapUid))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-warp-cube-fizzle"), args.User, args.User);
            return;
        }

        // Only on Lavaland - its surface or the chromite caves - and both ends on the same one:
        // not on the station, and no way out of the caves without a hook.
        var userMap = Transform(args.User).MapUid;
        var cubeMap = Transform(linked).MapUid;
        if (userMap != cubeMap || !IsLavalandOrCaves(userMap))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-warp-cube-not-lavaland"), args.User, args.User);
            return;
        }

        var target = _transform.GetMapCoordinates(linked);
        if (target.MapId == MapId.Nullspace)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-warp-cube-fizzle"), args.User, args.User);
            return;
        }

        Spawn(ent.Comp.Effect, Transform(args.User).Coordinates);
        _transform.SetMapCoordinates(args.User, target);
        Spawn(ent.Comp.Effect, Transform(args.User).Coordinates);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, args.User);
    }

    private bool IsLavalandOrCaves(EntityUid? map)
    {
        return HasComp<LavalandMapComponent>(map) || HasComp<LavalandChromiteCavesComponent>(map);
    }

    private void OnCubeShutdown(Entity<LavalandWarpCubeComponent> ent, ref ComponentShutdown args)
    {
        // The cubes only exist as a pair.
        if (ent.Comp.Linked is not { } linked || !TryComp<LavalandWarpCubeComponent>(linked, out var other))
            return;

        other.Linked = null;
        ent.Comp.Linked = null;
        QueueDel(linked);
    }

    #endregion

    #region Paradox bag

    private void OnParadoxMapInit(Entity<LavalandParadoxBagComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.SpawnPair is not { } pair || ent.Comp.Linked != null)
            return;

        // Link before the twin's own map init runs, or it spawns a twin of its own, and so on forever.
        var other = EntityManager.CreateEntityUninitialized(pair, Transform(ent).Coordinates);
        EnsureComp<LavalandParadoxBagComponent>(other).Linked = ent.Owner;
        EntityManager.InitializeAndStartEntity(other);
        ent.Comp.Linked = other;
    }

    /// <summary>The paradox collapses with either bag: the twin goes too, spilling what it held.</summary>
    private void OnParadoxShutdown(Entity<LavalandParadoxBagComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Linked is not { } linked || TerminatingOrDeleted(linked) ||
            !TryComp<LavalandParadoxBagComponent>(linked, out var twin))
        {
            return;
        }

        twin.Linked = null;
        if (TryComp<StorageComponent>(linked, out var storage))
            _containers.EmptyContainer(storage.Container, true);
        QueueDel(linked);
    }

    private void OnParadoxOpened(Entity<LavalandParadoxBagComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (ent.Comp.Linked is not { } linked || TerminatingOrDeleted(linked) ||
            !TryComp<StorageComponent>(linked, out var twin))
        {
            return;
        }

        foreach (var item in twin.Container.ContainedEntities.ToArray())
            _storage.Insert(ent, item, out _, playSound: false);
    }

    #endregion

    #region Immortality talisman

    private void OnTalismanUse(Entity<LavalandImmortalityTalismanComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextUse)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-talisman-not-ready"), args.User, args.User);
            return;
        }

        var user = args.User;
        var hole = Spawn(ent.Comp.HolePrototype, Transform(user).Coordinates);
        var holeComp = EnsureComp<LavalandRealityHoleComponent>(hole);
        var container = _containers.EnsureContainer<Container>(hole, HoleContainer);
        _popup.PopupEntity(Loc.GetString("lavaland-talisman-vanish", ("user", user)), user, PopupType.MediumCaution);
        if (!_containers.Insert(user, container))
        {
            QueueDel(hole);
            return;
        }

        holeComp.User = user;
        holeComp.ReleaseAt = now + ent.Comp.Duration;
        ent.Comp.NextUse = now + ent.Comp.Cooldown;
        _godmode.EnableGodmode(user);
    }

    private void OnHoleShutdown(Entity<LavalandRealityHoleComponent> ent, ref ComponentShutdown args)
    {
        ReleaseFromHole(ent);
    }

    /// <summary>The user steps back out of the hole, mortal again.</summary>
    private void ReleaseFromHole(Entity<LavalandRealityHoleComponent> ent)
    {
        if (ent.Comp.User is not { } user || TerminatingOrDeleted(user))
            return;

        ent.Comp.User = null;
        _godmode.DisableGodmode(user);
        if (_containers.TryGetContainer(ent, HoleContainer, out var container))
            _containers.Remove(user, container, destination: Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("lavaland-talisman-return", ("user", user)), user, PopupType.MediumCaution);
    }

    #endregion

    #region Ship in a bottle

    private void OnBottleUse(Entity<LavalandShipInBottleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Boat is { } boat && !TerminatingOrDeleted(boat))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-ship-bottle-empty"), args.User, args.User);
            return;
        }

        var xform = Transform(args.User);
        var offset = _transform.GetWorldRotation(xform).ToWorldVec();
        ent.Comp.Boat = Spawn(ent.Comp.BoatPrototype, xform.Coordinates.Offset(offset));
        _appearance.SetData(ent, LavalandArtifactVisuals.Charged, false);
        _popup.PopupEntity(Loc.GetString("lavaland-ship-bottle-release"), args.User, args.User);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, args.User);
    }

    private void OnBottleInteract(Entity<LavalandShipInBottleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target != ent.Comp.Boat)
            return;

        args.Handled = true;
        QueueDel(target);
        ent.Comp.Boat = null;
        _appearance.SetData(ent, LavalandArtifactVisuals.Charged, true);
        _popup.PopupEntity(Loc.GetString("lavaland-ship-bottle-return"), args.User, args.User);
    }

    #endregion

    #region Potion

    private void OnPotionUse(Entity<LavalandPotionComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var user = args.User;
        if (ent.Comp.HumanoidOnly && !HasComp<HumanoidAppearanceComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-potion-nothing"), user, user);
            QueueDel(ent);
            return;
        }

        EntityManager.AddComponents(user, ent.Comp.AddedComponents, false);
        if (!ent.Comp.Damage.Empty)
            _damageable.TryChangeDamage(user, ent.Comp.Damage, true, origin: user);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Message), user, user, PopupType.LargeCaution);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, user);
        QueueDel(ent);
    }

    #endregion

    #region Rod of Asclepius

    private void OnRodUse(Entity<LavalandRodOfAsclepiusComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Activated)
            return;
        args.Handled = true;

        if (!HasComp<HumanoidAppearanceComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-asclepius-unworthy"), args.User, args.User);
            return;
        }

        var rods = EntityQueryEnumerator<LavalandRodOfAsclepiusComponent>();
        while (rods.MoveNext(out var uid, out var rod))
        {
            if (uid != ent.Owner && rod.Activated && rod.Bearer == args.User)
            {
                _popup.PopupEntity(Loc.GetString("lavaland-asclepius-already"), args.User, args.User);
                return;
            }
        }

        ent.Comp.OathProgress = 0;
        _popup.PopupEntity(Loc.GetString("lavaland-asclepius-start"), args.User, args.User);
        StartOathLine(ent, args.User);
    }

    private void StartOathLine(Entity<LavalandRodOfAsclepiusComponent> ent, EntityUid user)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.LineDelay,
            new LavalandAsclepiusOathDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
        });
    }

    private void OnOathLine(Entity<LavalandRodOfAsclepiusComponent> ent, ref LavalandAsclepiusOathDoAfterEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (args.Cancelled)
        {
            ent.Comp.OathProgress = 0;
            _popup.PopupEntity(Loc.GetString("lavaland-asclepius-interrupted"), args.User, args.User);
            return;
        }

        if (ent.Comp.OathProgress < ent.Comp.OathLines.Count)
        {
            _chat.TrySendInGameICMessage(args.User, Loc.GetString(ent.Comp.OathLines[ent.Comp.OathProgress]),
                InGameICChatType.Speak, false);
        }

        ent.Comp.OathProgress++;
        if (ent.Comp.OathProgress < ent.Comp.OathLines.Count)
        {
            StartOathLine(ent, args.User);
            return;
        }

        ent.Comp.Activated = true;
        ent.Comp.Bearer = args.User;
        EnsureComp<UnremoveableComponent>(ent);
        EnsureComp<PacifiedComponent>(args.User);
        _appearance.SetData(ent, LavalandArtifactVisuals.Active, true);
        _item.SetHeldPrefix(ent, "active");
        _popup.PopupEntity(Loc.GetString("lavaland-asclepius-bound"), args.User, args.User, PopupType.Large);
    }

    private void UpdateRod(LavalandRodOfAsclepiusComponent rod, TimeSpan now)
    {
        if (!rod.Activated || rod.Bearer is not { } bearer || TerminatingOrDeleted(bearer) || _mobState.IsDead(bearer))
            return;

        if (now >= rod.NextSelfHeal)
        {
            rod.NextSelfHeal = now + TimeSpan.FromSeconds(1);
            _damageable.TryChangeDamage(bearer, rod.SelfHeal, true, false, origin: bearer);
        }

        if (now < rod.NextAura)
            return;

        rod.NextAura = now + rod.AuraInterval;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(Transform(bearer).Coordinates, rod.AuraRange, _mobs);
        foreach (var mob in _mobs)
        {
            if (!_mobState.IsDead(mob, mob.Comp))
                _damageable.TryChangeDamage(mob.Owner, rod.AuraHeal, true, false, origin: bearer);
        }
    }

    #endregion

    #region Memento mori

    private void OnMementoAction(Entity<LavalandMementoMoriComponent> ent, ref LavalandMementoMoriActionEvent args)
    {
        if (args.Handled || ent.Comp.LinkedUser != null || !HasComp<HumanoidAppearanceComponent>(args.Performer))
            return;
        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("lavaland-memento-draining"), args.Performer, args.Performer, PopupType.MediumCaution);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Performer, ent.Comp.LinkTime,
            new LavalandMementoMoriDoAfterEvent(), ent.Owner, used: ent.Owner)
        {
            BreakOnMove = true,
            NeedHand = false,
        });
    }

    private void OnMementoLinked(Entity<LavalandMementoMoriComponent> ent, ref LavalandMementoMoriDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || ent.Comp.LinkedUser != null)
            return;
        args.Handled = true;

        var user = args.User;
        if (!TryComp<MobThresholdsComponent>(user, out var thresholds))
            return;

        var bound = EnsureComp<LavalandMementoBoundComponent>(user);
        bound.Pendant = ent.Owner;
        if (_thresholds.TryGetThresholdForState(user, MobState.Critical, out var crit, thresholds))
            bound.OldCritThreshold = crit;
        if (_thresholds.TryGetThresholdForState(user, MobState.Dead, out var dead, thresholds))
            bound.OldDeadThreshold = dead;

        // While linked, no amount of damage puts the wearer down.
        var never = FixedPoint2.New(1000000);
        _thresholds.SetMobStateThreshold(user, never, MobState.Critical, thresholds);
        _thresholds.SetMobStateThreshold(user, never + 1, MobState.Dead, thresholds);

        ent.Comp.LinkedUser = user;
        _appearance.SetData(ent, LavalandArtifactVisuals.Active, true);
        _popup.PopupEntity(Loc.GetString("lavaland-memento-linked"), user, user, PopupType.Large);
    }

    private void OnMementoUnequipped(Entity<LavalandMementoMoriComponent> ent, ref GotUnequippedEvent args)
    {
        Mori(ent);
    }

    private void OnMementoShutdown(Entity<LavalandMementoMoriComponent> ent, ref ComponentShutdown args)
    {
        Mori(ent);
    }

    /// <summary>The wearer of a linked memento mori dies as if they took it off: to ash. False if not linked.</summary>
    public bool TryMori(EntityUid wearer)
    {
        if (!TryComp<LavalandMementoBoundComponent>(wearer, out var bound) ||
            !TryComp<LavalandMementoMoriComponent>(bound.Pendant, out var pendant) ||
            pendant.LinkedUser != wearer)
        {
            return false;
        }

        Mori((bound.Pendant, pendant));
        return true;
    }

    private void Mori(Entity<LavalandMementoMoriComponent> ent)
    {
        if (ent.Comp.LinkedUser is not { } owner)
            return;

        ent.Comp.LinkedUser = null;
        if (!TerminatingOrDeleted(ent))
            _appearance.SetData(ent, LavalandArtifactVisuals.Active, false);
        if (TerminatingOrDeleted(owner))
            return;

        if (TryComp<LavalandMementoBoundComponent>(owner, out var bound))
        {
            if (bound.OldCritThreshold is { } crit)
                _thresholds.SetMobStateThreshold(owner, crit, MobState.Critical);
            if (bound.OldDeadThreshold is { } dead)
                _thresholds.SetMobStateThreshold(owner, dead, MobState.Dead);
            RemComp<LavalandMementoBoundComponent>(owner);
        }

        _popup.PopupEntity(Loc.GetString("lavaland-memento-mori", ("user", owner)), owner, PopupType.LargeCaution);
        Spawn(ent.Comp.AshPrototype, Transform(owner).Coordinates);
        _gibbing.Gib(owner);
    }

    #endregion

    #region Berserker armour

    private void OnBerserkerExamined(Entity<LavalandBerserkerSuitComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("lavaland-berserker-charge",
            ("charge", (int) (ent.Comp.Charge / Math.Max(1f, ent.Comp.MaxCharge) * 100))));
    }

    private void OnBerserkerEquipped(Entity<LavalandBerserkerSuitComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.OUTERCLOTHING) == 0)
            return;

        ent.Comp.Wearer = args.Equipee;
        EnsureComp<LavalandBerserkerWearerComponent>(args.Equipee).Suit = ent.Owner;
    }

    private void OnBerserkerUnequipped(Entity<LavalandBerserkerSuitComponent> ent, ref GotUnequippedEvent args)
    {
        if (ent.Comp.Wearer != args.Equipee)
            return;

        SetBerserk(ent, false);
        ent.Comp.Wearer = null;
        RemComp<LavalandBerserkerWearerComponent>(args.Equipee);
    }

    private void OnBerserkerGetActions(Entity<LavalandBerserkerSuitComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is { } flags && (flags & SlotFlags.OUTERCLOTHING) != 0)
            args.AddAction(ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnBerserkAction(Entity<LavalandBerserkerSuitComponent> ent, ref LavalandBerserkActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Active || ent.Comp.Charge < ent.Comp.MaxCharge)
        {
            _popup.PopupEntity(Loc.GetString("lavaland-berserker-not-charged"), args.Performer, args.Performer);
            return;
        }

        SetBerserk(ent, true);
    }

    private void SetBerserk(Entity<LavalandBerserkerSuitComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        ent.Comp.Active = active;
        if (ent.Comp.Wearer is not { } wearer)
            return;

        _movement.RefreshMovementSpeedModifiers(wearer);
        _popup.PopupEntity(Loc.GetString(active ? "lavaland-berserker-on" : "lavaland-berserker-off", ("user", wearer)),
            wearer, active ? PopupType.LargeCaution : PopupType.Medium);
        if (active && ent.Comp.ActivateSound != null)
            _audio.PlayPvs(ent.Comp.ActivateSound, wearer);
    }

    private void OnBerserkerDamaged(Entity<LavalandBerserkerWearerComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta ||
            !TryComp<LavalandBerserkerSuitComponent>(ent.Comp.Suit, out var suit) || suit.Active)
        {
            return;
        }

        var wasCharged = suit.Charge >= suit.MaxCharge;
        suit.Charge = Math.Min(suit.MaxCharge, suit.Charge + delta.GetTotal().Float() * suit.DamageToCharge);
        if (!wasCharged && suit.Charge >= suit.MaxCharge)
            _popup.PopupEntity(Loc.GetString("lavaland-berserker-charged"), ent, ent, PopupType.Medium);
    }

    private void OnBerserkerDamageModify(Entity<LavalandBerserkerWearerComponent> ent, ref DamageModifyEvent args)
    {
        if (!TryComp<LavalandBerserkerSuitComponent>(ent.Comp.Suit, out var suit) || !suit.Active)
            return;

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, suit.ActiveModifiers);
    }

    private void OnBerserkerSpeed(Entity<LavalandBerserkerWearerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (TryComp<LavalandBerserkerSuitComponent>(ent.Comp.Suit, out var suit) && suit.Active)
            args.ModifySpeed(suit.SpeedMultiplier, suit.SpeedMultiplier);
    }

    private void OnBerserkerShot(Entity<LavalandBerserkerWearerComponent> ent, ref ShotAttemptedEvent args)
    {
        if (TryComp<LavalandBerserkerSuitComponent>(ent.Comp.Suit, out var suit) && suit.Active)
            args.Cancel();
    }

    #endregion
}
