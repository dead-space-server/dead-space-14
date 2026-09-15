using System.Linq;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.Ghost.Roles;
using Content.Server.Tiles;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Chasm;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DeadSpace.AshWalkers;
using Content.Shared.DeadSpace.CCCCVars;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Kitchen.Components;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.AshWalkers;

public sealed class AshWalkerNestSystem : SharedAshWalkerNestSystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRoles = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AshWalkerNestComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AshWalkerNestComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<AshWalkerNestComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<AshWalkerNestComponent, AshWalkerSacrificeDoAfterEvent>(OnSacrifice);
        SubscribeLocalEvent<AshWalkerNestComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<AshWalkerNestComponent, DestructionEventArgs>(OnDestroyed);
        SubscribeLocalEvent<AshWalkerNestComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AshWalkerIncubatingEggComponent, ExaminedEvent>(OnEggExamine);
    }

    private void OnMapInit(Entity<AshWalkerNestComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.HomeMap ??= Transform(ent).MapUid;
    }

    private void OnGetVerbs(Entity<AshWalkerNestComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !CanUseNest(ent, args.User) ||
            !TryComp<PullerComponent>(args.User, out var puller) || puller.Pulling is not { } body)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("ash-walker-nest-sacrifice"),
            Act = () => TryStartSacrifice(ent, user, body),
            Disabled = !CanSacrifice(ent, user, body, out _),
            Message = Loc.GetString("ash-walker-nest-sacrifice-hint"),
        });
    }

    protected override bool CanDrop(Entity<AshWalkerNestComponent> nest, EntityUid user, EntityUid body)
    {
        return CanSacrifice(nest, user, body, out _);
    }

    private void OnDragDrop(Entity<AshWalkerNestComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStartSacrifice(ent, args.User, args.Dragged);
    }

    public bool TryStartSacrifice(Entity<AshWalkerNestComponent> nest, EntityUid user, EntityUid body)
    {
        if (!CanSacrifice(nest, user, body, out _))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, nest.Comp.SacrificeDelay,
            new AshWalkerSacrificeDoAfterEvent(), nest, target: body, used: nest)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = false,
            BreakOnDropItem = false,
        });
    }

    private void OnSacrifice(Entity<AshWalkerNestComponent> ent, ref AshWalkerSacrificeDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } body ||
            !CanSacrifice(ent, args.User, body, out var value))
            return;

        args.Handled = true;
        // Retire butchering before queueing deletion so another completed do-after cannot harvest the same body.
        RemComp<ButcherableComponent>(body);

        DropEquipment(body);
        QueueDel(body);
        ent.Comp.Flesh += value;
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Healing, ignoreResistances: true);
        _audio.PlayPvs(ent.Comp.SacrificeSound, ent);
        _popup.PopupEntity(Loc.GetString("ash-walker-nest-consumed", ("body", body)), ent, PopupType.MediumCaution);
        _adminLog.Add(LogType.Gib, LogImpact.High,
            $"{ToPrettyString(args.User):user} sacrificed {ToPrettyString(body):body} to {ToPrettyString(ent):nest} for {value} flesh.");
    }

    private bool CanSacrifice(Entity<AshWalkerNestComponent> nest, EntityUid user, EntityUid body, out int value)
    {
        value = 0;
        if (!CanUseNest(nest, user) || TerminatingOrDeleted(body) || EntityManager.IsQueuedForDeletion(body) ||
            !base.CanDrop(nest, user, body) ||
            HasComp<KitchenSpikeVictimComponent>(body) || HasComp<LavalandLegionInfestedComponent>(body) ||
            _container.IsEntityInContainer(body) ||
            !_blocker.CanInteract(user, nest) ||
            !_interaction.InRangeUnobstructed(user, nest.Owner) ||
            !_interaction.InRangeUnobstructed(user, body) ||
            !_interaction.InRangeUnobstructed(nest.Owner, body, range: 1.5f))
            return false;

        if (MetaData(body).EntityPrototype is { } prototype && nest.Comp.SacrificeValues.TryGetValue(prototype.ID, out var configured))
            value = configured;
        else if (HasComp<HumanoidAppearanceComponent>(body) && HasComp<BloodstreamComponent>(body) && HasComp<BodyComponent>(body))
            value = nest.Comp.HumanoidValue;

        return value > 0 && value <= nest.Comp.FleshCapacity - nest.Comp.Flesh;
    }

    private bool CanUseNest(Entity<AshWalkerNestComponent> nest, EntityUid user)
    {
        return _cfg.GetCVar(CCCCVars.AshWalkersEnabled) && IsNestIntact(nest) &&
               TryComp<AshWalkerTribeMemberComponent>(user, out var member) &&
               member.HomeMap == nest.Comp.HomeMap;
    }

    private bool IsNestIntact(Entity<AshWalkerNestComponent> nest)
    {
        return !nest.Comp.Destroyed && !TerminatingOrDeleted(nest) && !EntityManager.IsQueuedForDeletion(nest) &&
               Transform(nest).Anchored && Transform(nest).MapUid == nest.Comp.HomeMap &&
               HasComp<LavalandMapComponent>(nest.Comp.HomeMap);
    }

    private void DropEquipment(EntityUid body)
    {
        if (_inventory.TryGetSlots(body, out var slots))
        {
            foreach (var slot in slots)
                _inventory.TryUnequip(body, slot.Name, silent: true, force: true);
        }

        foreach (var item in _inventory.GetHandOrInventoryEntities((body, null, null)).ToArray())
        {
            _container.TryRemoveFromContainer(item, force: true);
            _transform.DropNextTo(item, body);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(1);
        var eggs = EntityQueryEnumerator<AshWalkerIncubatingEggComponent, AshWalkerEggComponent, TransformComponent>();
        while (eggs.MoveNext(out var uid, out var incubation, out var egg, out var xform))
        {
            if (!TryComp<AshWalkerNestComponent>(egg.Nest, out var nest) ||
                !IsNestIntact((egg.Nest.Value, nest)) || xform.MapUid != nest.HomeMap)
            {
                QueueDel(uid);
                continue;
            }

            if (_cfg.GetCVar(CCCCVars.AshWalkersEnabled) && !MetaData(egg.Nest.Value).EntityPaused &&
                _timing.CurTime >= incubation.ReadyAt)
            {
                RemCompDeferred<AshWalkerIncubatingEggComponent>(uid);
                _popup.PopupEntity(Loc.GetString("ash-walker-egg-matured"), uid, PopupType.Medium);
                _ghostRoles.UpdateAllEui();
            }
        }

        if (!_cfg.GetCVar(CCCCVars.AshWalkersEnabled))
            return;

        var nests = EntityQueryEnumerator<AshWalkerNestComponent>();
        while (nests.MoveNext(out var uid, out var nest))
        {
            if (!IsNestIntact((uid, nest)) || nest.EggCost <= 0 || nest.Flesh < nest.EggCost ||
                CountEggs((uid, nest)) >= nest.MaxEggs || !TryGetEggCoordinates(uid, out var coordinates))
                continue;

            var egg = Spawn(nest.EggPrototype, coordinates);
            Comp<AshWalkerEggComponent>(egg).Nest = uid;
            Comp<AshWalkerIncubatingEggComponent>(egg).ReadyAt = _timing.CurTime + nest.IncubationTime;
            nest.Flesh -= nest.EggCost;
        }
    }

    private int CountEggs(Entity<AshWalkerNestComponent> nest)
    {
        var count = 0;
        var eggs = AllEntityQuery<AshWalkerEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg))
        {
            if (!TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid) &&
                (egg.Nest == nest.Owner || egg.HomeMap == nest.Comp.HomeMap))
                count++;
        }
        return count;
    }

    private bool TryGetEggCoordinates(EntityUid nest, out EntityCoordinates coordinates)
    {
        coordinates = default;
        var xform = Transform(nest);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var center))
            return false;

        var candidates = new List<EntityCoordinates>();
        for (var x = -2; x <= 2; x++)
        for (var y = -2; y <= 2; y++)
        {
            if (x == 0 && y == 0 || x * x + y * y > 4 ||
                !_map.TryGetTileRef(gridUid, grid, center.GridIndices + new Vector2i(x, y), out var tile) ||
                tile.Tile.IsEmpty || _turf.IsSpace(tile) || _turf.IsTileBlocked(tile, CollisionGroup.MobMask))
                continue;

            var unsafeTile = false;
            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile.GridIndices);
            while (anchored.MoveNext(out var uid))
            {
                if (HasComp<ChasmComponent>(uid) || HasComp<TileEntityEffectComponent>(uid))
                {
                    unsafeTile = true;
                    break;
                }
            }

            var candidate = _map.ToCenterCoordinates(tile, grid);
            if (!unsafeTile && _lookup.GetEntitiesInRange<AshWalkerEggComponent>(candidate, 0.5f).Count == 0 &&
                _interaction.InRangeUnobstructed(nest, candidate, range: 2.5f))
                candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return false;

        coordinates = _random.Pick(candidates);
        return true;
    }

    private void OnExamine(Entity<AshWalkerNestComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("ash-walker-nest-status", ("flesh", ent.Comp.Flesh),
            ("capacity", ent.Comp.FleshCapacity), ("cost", ent.Comp.EggCost),
            ("eggs", CountEggs(ent)), ("limit", ent.Comp.MaxEggs)));
        args.PushMarkup(Loc.GetString("ash-walker-nest-instructions"));
    }

    private void OnEggExamine(Entity<AshWalkerIncubatingEggComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("ash-walker-egg-incubating"));
    }

    private void OnDestroyed(Entity<AshWalkerNestComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.Destroyed)
            return;

        ent.Comp.Destroyed = true;
        DestroyIncubatingEggs(ent);
        Spawn(ent.Comp.CollapsePrototype, Transform(ent).Coordinates);
    }

    private void OnShutdown(Entity<AshWalkerNestComponent> ent, ref ComponentShutdown args)
    {
        DestroyIncubatingEggs(ent);
    }

    private void DestroyIncubatingEggs(EntityUid nest)
    {
        var eggs = AllEntityQuery<AshWalkerEggComponent, AshWalkerIncubatingEggComponent>();
        while (eggs.MoveNext(out var uid, out var egg, out _))
        {
            if (egg.Nest == nest)
                QueueDel(uid);
        }
        _ghostRoles.UpdateAllEui();
    }
}
