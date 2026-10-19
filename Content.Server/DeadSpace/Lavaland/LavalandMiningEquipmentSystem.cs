// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.Player;
using Content.Server.Administration;
using System.Linq;
using System.Numerics;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Mining.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// Mining shop equipment ported from tgstation: marker beacons, the lazarus injector and minebots.
/// </summary>
public sealed class LavalandMiningEquipmentSystem : EntitySystem
{
    [Dependency] private readonly Content.Shared.Tools.Systems.SharedToolSystem _tool = default!;
    [Dependency] private readonly Content.Server.Administration.QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly Content.Shared.Interaction.SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly LavalandTamingSystem _taming = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<StackComponent>> _ores = new();
    private readonly HashSet<Entity<OreVeinComponent>> _veins = new();
    private readonly HashSet<Entity<MobStateComponent>> _inLine = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMarkerBeaconStackComponent, MapInitEvent>(OnStackMapInit);
        SubscribeLocalEvent<LavalandMarkerBeaconStackComponent, UseInHandEvent>(OnStackUse);
        SubscribeLocalEvent<LavalandMarkerBeaconStackComponent, GetVerbsEvent<AlternativeVerb>>(OnStackVerbs);
        SubscribeLocalEvent<LavalandMarkerBeaconStackComponent, ExaminedEvent>(OnStackExamined);
        SubscribeLocalEvent<LavalandMarkerBeaconComponent, MapInitEvent>(OnBeaconMapInit);
        SubscribeLocalEvent<LavalandMarkerBeaconComponent, InteractHandEvent>(OnBeaconPickup);
        SubscribeLocalEvent<LavalandMarkerBeaconComponent, InteractUsingEvent>(OnBeaconInteractUsing);
        SubscribeLocalEvent<LavalandMarkerBeaconComponent, ExaminedEvent>(OnBeaconExamined);

        SubscribeLocalEvent<LavalandLazarusInjectorComponent, AfterInteractEvent>(OnLazarusInteract);
        SubscribeLocalEvent<LavalandLazarusInjectorComponent, EmpPulseEvent>(OnLazarusEmp);
        SubscribeLocalEvent<LavalandLazarusInjectorComponent, ExaminedEvent>(OnLazarusExamined);

        SubscribeLocalEvent<LavalandMinebotComponent, MapInitEvent>(OnMinebotMapInit);
        SubscribeLocalEvent<LavalandMinebotComponent, GetVerbsEvent<AlternativeVerb>>(OnMinebotVerbs);
        SubscribeLocalEvent<LavalandMinebotComponent, InteractHandEvent>(OnMinebotInteract);
        SubscribeLocalEvent<LavalandMinebotComponent, ExaminedEvent>(OnMinebotExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var minebots = EntityQueryEnumerator<LavalandMinebotComponent, TransformComponent>();
        while (minebots.MoveNext(out var uid, out var minebot, out var xform))
        {
            if (!minebot.CollectOre || _mobState.IsDead(uid))
                continue;

            if (now >= minebot.NextCollect)
            {
                minebot.NextCollect = now + minebot.CollectInterval;
                CollectOre((uid, minebot), xform);
            }

            if (minebot.MineOre && now >= minebot.NextMine)
            {
                minebot.NextMine = now + minebot.MineInterval;
                MineOre((uid, minebot), xform);
            }
        }
    }

    #region Marker beacons

    private void OnStackMapInit(Entity<LavalandMarkerBeaconStackComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, LavalandArtifactVisuals.Color, ent.Comp.Color);
    }

    private void OnStackExamined(Entity<LavalandMarkerBeaconStackComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("lavaland-marker-beacon-examine",
            ("color", Loc.GetString($"lavaland-marker-beacon-color-{ent.Comp.Color}"))));
    }

    private void OnStackVerbs(Entity<LavalandMarkerBeaconStackComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Colors.Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("lavaland-marker-beacon-color-verb"),
            Act = () =>
            {
                var index = (ent.Comp.Colors.IndexOf(ent.Comp.Color) + 1) % ent.Comp.Colors.Count;
                ent.Comp.Color = ent.Comp.Colors[index];
                _appearance.SetData(ent, LavalandArtifactVisuals.Color, ent.Comp.Color);
                _popup.PopupEntity(Loc.GetString("lavaland-marker-beacon-color-set",
                    ("color", Loc.GetString($"lavaland-marker-beacon-color-{ent.Comp.Color}"))), user, user);
            },
        });
    }

    private void OnStackUse(Entity<LavalandMarkerBeaconStackComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        var xform = Transform(user);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var existing))
        {
            if (existing is { } uid && HasComp<LavalandMarkerBeaconComponent>(uid))
            {
                _popup.PopupEntity(Loc.GetString("lavaland-marker-beacon-occupied"), user, user);
                return;
            }
        }

        if (!_stack.TryUse(ent.Owner, 1))
            return;

        var beacon = Spawn(ent.Comp.BeaconPrototype, _map.GridTileToLocal(gridUid, grid, tile));
        var beaconComp = EnsureComp<LavalandMarkerBeaconComponent>(beacon);
        beaconComp.Color = ent.Comp.Color;
        ApplyBeaconColor((beacon, beaconComp), ent.Comp.LightColors);
        _popup.PopupEntity(Loc.GetString("lavaland-marker-beacon-placed"), user, user);
    }

    private void OnBeaconMapInit(Entity<LavalandMarkerBeaconComponent> ent, ref MapInitEvent args)
    {
        _appearance.SetData(ent, LavalandArtifactVisuals.Color, ent.Comp.Color);
    }

    private void ApplyBeaconColor(Entity<LavalandMarkerBeaconComponent> beacon, Dictionary<string, Color> colors)
    {
        _appearance.SetData(beacon, LavalandArtifactVisuals.Color, beacon.Comp.Color);
        if (colors.TryGetValue(beacon.Comp.Color, out var color) && _light.TryGetLight(beacon, out var light))
            _light.SetColor(beacon, color, light);
    }

    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.Tools.ToolQualityPrototype> ScrewingQuality = "Screwing";

    /// <summary>A screwdriver scratches a message on the beacon: it shows on examine and on the sensor tablet.</summary>
    private void OnBeaconInteractUsing(Entity<LavalandMarkerBeaconComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, ScrewingQuality) ||
            !TryComp<ActorComponent>(args.User, out var actor))
        {
            return;
        }

        args.Handled = true;
        var beacon = ent.Owner;
        var user = args.User;
        _quickDialog.OpenDialog<LongString>(actor.PlayerSession,
            Loc.GetString("lavaland-marker-beacon-message-title"),
            Loc.GetString("lavaland-marker-beacon-message-prompt"),
            text =>
            {
                if (TerminatingOrDeleted(beacon) || TerminatingOrDeleted(user) ||
                    !TryComp<LavalandMarkerBeaconComponent>(beacon, out var comp) ||
                    !_interaction.InRangeUnobstructed(user, beacon))
                {
                    return;
                }

                var message = ((string) text).Trim();
                if (message.Length > comp.MaxMessageLength)
                    message = message[..comp.MaxMessageLength];

                comp.Message = message.Length == 0 ? null : message;
                _popup.PopupEntity(Loc.GetString(comp.Message == null
                    ? "lavaland-marker-beacon-message-cleared"
                    : "lavaland-marker-beacon-message-set"), beacon, user);
            });
    }

    private void OnBeaconExamined(Entity<LavalandMarkerBeaconComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Message is { } message)
            args.PushMarkup(Loc.GetString("lavaland-marker-beacon-message-examine", ("message", FormattedMessage.EscapeText(message))));
        else
            args.PushMarkup(Loc.GetString("lavaland-marker-beacon-message-hint"));
    }

    private void OnBeaconPickup(Entity<LavalandMarkerBeaconComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var stack = Spawn(ent.Comp.StackPrototype, Transform(args.User).Coordinates);
        if (TryComp<LavalandMarkerBeaconStackComponent>(stack, out var stackComp))
        {
            stackComp.Color = ent.Comp.Color;
            _appearance.SetData(stack, LavalandArtifactVisuals.Color, stackComp.Color);
        }

        _hands.PickupOrDrop(args.User, stack);
        QueueDel(ent);
    }

    #endregion

    #region Lazarus injector

    private void OnLazarusExamined(Entity<LavalandLazarusInjectorComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Loaded)
            args.PushMarkup(Loc.GetString("lavaland-lazarus-empty"));
        if (ent.Comp.Malfunctioning)
            args.PushMarkup(Loc.GetString("lavaland-lazarus-malfunction"));
    }

    private void OnLazarusEmp(Entity<LavalandLazarusInjectorComponent> ent, ref EmpPulseEvent args)
    {
        ent.Comp.Malfunctioning = true;
        args.Affected = true;
    }

    private void OnLazarusInteract(Entity<LavalandLazarusInjectorComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !ent.Comp.Loaded)
            return;

        args.Handled = true;
        // Only simple creatures: crew and other humanoids are far too complex for the nanomachines.
        if (HasComp<HumanoidAppearanceComponent>(target) || !TryComp<MobStateComponent>(target, out var mob) ||
            !HasComp<NpcFactionMemberComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-lazarus-invalid"), target, args.User);
            return;
        }

        // Megafauna and mini-bosses are far too much for the nanomachines.
        if (HasComp<LavalandBossComponent>(target) || HasComp<LavalandEliteComponent>(target) ||
            HasComp<LavalandMadMinerComponent>(target) || HasComp<LavalandLegionMegafaunaComponent>(target) ||
            HasComp<Content.Server.DeadSpace.Lavaland.Celestial.CelestialComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-lazarus-too-strong"), target, args.User);
            return;
        }

        if (!_mobState.IsDead(target, mob))
        {
            _popup.PopupEntity(Loc.GetString("lavaland-lazarus-not-dead"), target, args.User);
            return;
        }

        _rejuvenate.PerformRejuvenate(target);
        // It serves whoever brought it back (its side changes too): they get the orders to follow, stay and attack.
        if (!ent.Comp.Malfunctioning)
            _taming.Tame(target, args.User, ent.Comp.Factions);

        ent.Comp.Loaded = false;
        _appearance.SetData(ent, LavalandArtifactVisuals.Charged, false);
        _popup.PopupEntity(Loc.GetString("lavaland-lazarus-revived", ("user", args.User), ("target", target)), target);
        if (ent.Comp.Sound != null)
            _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    #endregion

    #region Minebot

    private void OnMinebotMapInit(Entity<LavalandMinebotComponent> ent, ref MapInitEvent args)
    {
        // A built-in borg ore bag: the ore it picks up goes there.
        var slot = _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.BagSlot);
        if (slot.ContainedEntity == null)
            SpawnInContainerOrDrop(ent.Comp.BagPrototype, ent, ent.Comp.BagSlot);
    }

    private bool TryGetBag(Entity<LavalandMinebotComponent> ent, out Entity<StorageComponent> bag)
    {
        bag = default;
        if (!_containers.TryGetContainer(ent, ent.Comp.BagSlot, out var container) ||
            container.ContainedEntities.Count == 0 ||
            !TryComp<StorageComponent>(container.ContainedEntities[0], out var storage))
        {
            return false;
        }

        bag = (container.ContainedEntities[0], storage);
        return true;
    }

    private void OnMinebotExamined(Entity<LavalandMinebotComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.CollectOre ? "lavaland-minebot-collecting" : "lavaland-minebot-not-collecting"));
    }

    private void OnMinebotVerbs(Entity<LavalandMinebotComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        // Alt-click: the bag is emptied at its feet.
        if (TryGetBag(ent, out var bag) && bag.Comp.Container.ContainedEntities.Count > 0)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("lavaland-minebot-unload-verb"),
                Act = () => UnloadMinebot(ent, user),
                Priority = 3,
            });
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("lavaland-minebot-bind"),
            Act = () => BindMinebot(ent, user),
            Priority = 2,
        });
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.CollectOre ? "lavaland-minebot-collect-off" : "lavaland-minebot-collect-on"),
            Act = () => ent.Comp.CollectOre = !ent.Comp.CollectOre,
            Priority = 1,
        });
    }

    /// <summary>Its owner, another minebot or anyone else stands between it and the ore: it does not shoot through them.</summary>
    private bool IsLineOfFireBlocked(EntityUid bot, Vector2 from, Vector2 to)
    {
        var line = to - from;
        var length = line.Length();
        if (length < 0.01f)
            return false;

        var direction = line / length;
        foreach (var mob in _inLine)
        {
            if (mob.Owner == bot)
                continue;

            var offset = _transform.GetWorldPosition(mob) - from;
            var along = Vector2.Dot(offset, direction);
            if (along <= 0f || along >= length)
                continue;

            if ((offset - direction * along).LengthSquared() < MinebotLineOfFireWidth * MinebotLineOfFireWidth)
                return true;
        }

        return false;
    }

    private const float MinebotLineOfFireWidth = 0.6f;

    private void BindMinebot(Entity<LavalandMinebotComponent> ent, EntityUid user)
    {
        ent.Comp.BoundUser = user;
        // Each its own spot around the owner: several minebots do not move about as one, shooting into each other.
        var spot = _random.NextAngle().ToVec() * _random.NextFloat(1f, 2.2f);
        _npc.SetBlackboard(ent, NPCBlackboard.FollowTarget, new EntityCoordinates(user, spot));
        _popup.PopupEntity(Loc.GetString("lavaland-minebot-bound"), ent, user);
    }

    private void OnMinebotInteract(Entity<LavalandMinebotComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !TryGetBag(ent, out var bag) || bag.Comp.Container.ContainedEntities.Count == 0)
            return;

        args.Handled = true;
        UnloadMinebot(ent, args.User);
    }

    private void UnloadMinebot(Entity<LavalandMinebotComponent> ent, EntityUid user)
    {
        if (!TryGetBag(ent, out var bag))
            return;

        _containers.EmptyContainer(bag.Comp.Container, destination: Transform(ent).Coordinates);
        _popup.PopupEntity(Loc.GetString("lavaland-minebot-unloaded"), ent, user);
    }

    private void CollectOre(Entity<LavalandMinebotComponent> ent, TransformComponent xform)
    {
        if (!TryGetBag(ent, out var bag))
            return;

        _ores.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.CollectRange, _ores, LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var ore in _ores)
        {
            if (!_tag.HasTag(ore, ent.Comp.OreTag) || _containers.IsEntityInContainer(ore))
                continue;

            // A full bag: the rest stays on the floor until it is unloaded.
            if (!_storage.Insert(bag, ore, out _, playSound: false, storageComp: bag.Comp))
                break;
        }
    }

    /// <summary>
    /// No fauna around: it shoots the nearest ore vein with its kinetic accelerator, digging the ore out.
    /// A rock in the way gets dug through as well.
    /// </summary>
    private void MineOre(Entity<LavalandMinebotComponent> ent, TransformComponent xform)
    {
        if (!TryComp<GunComponent>(ent, out var gun) ||
            _faction.GetNearbyHostiles(ent.Owner, ent.Comp.MineHostileRange).Any())
        {
            return;
        }

        var position = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        _veins.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.MineRange, _veins, LookupFlags.Static);
        _inLine.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, ent.Comp.MineRange, _inLine);
        foreach (var vein in _veins)
        {
            // Not the outpost's own walls: ore showing on the base grid is not for digging.
            if (vein.Comp.CurrentOre == null || HasComp<LavalandOutpostComponent>(Transform(vein).GridUid))
                continue;

            var veinPosition = _transform.GetWorldPosition(vein);
            var distance = (veinPosition - position).LengthSquared();
            if (distance >= bestDistance || IsLineOfFireBlocked(ent.Owner, position, veinPosition))
                continue;

            best = vein;
            bestDistance = distance;
        }

        if (best is not { } target)
            return;

        _gun.AttemptShoot(ent, (ent.Owner, gun), Transform(target).Coordinates, target);
    }

    #endregion
}
