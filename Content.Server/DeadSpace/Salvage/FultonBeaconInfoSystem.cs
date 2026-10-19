// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Server.EUI;
using Content.Server.Pinpointer;
using Content.Server.Salvage;
using Content.Server.Stack;
using Content.Shared.DeadSpace.UI;
using Content.Shared.Eui;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Salvage.Fulton;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Salvage;

/// <summary>
/// Where fultons go: examining a fulton beacon shows where it stands, examining a fulton shows the beacon (or, for a
/// merged stack, the beacons in order) it is linked to - by the nearest station map beacon, or the grid or map name.
/// A fulton stack that finds no room in a bag, while the bag holds fultons linked to another beacon, offers to merge
/// them: each fulton keeps its own beacon.
/// </summary>
public sealed class FultonBeaconInfoSystem : EntitySystem
{
    [Dependency] private readonly EuiManager _eui = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly FultonSystem _fulton = default!;
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<ICommonSession> _asking = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FultonBeaconComponent, ExaminedEvent>(OnBeaconExamined);
        SubscribeLocalEvent<FultonComponent, ExaminedEvent>(OnFultonExamined);
        SubscribeLocalEvent<FultonComponent, StorageStackInsertFailedEvent>(OnInsertFailed);
    }

    /// <summary>The nearest station map beacon's name; failing that, the grid's or the map's name.</summary>
    public string PlaceOf(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid))
            return Loc.GetString("fulton-place-unknown");

        var coords = _transform.GetMapCoordinates(uid);
        if (_navMap.TryGetNearestBeacon(coords, out var beacon, out _) && beacon.Value.Comp.Text is { } text)
            return text;

        var xform = Transform(uid);
        if (xform.GridUid is { } grid && grid != xform.MapUid)
            return MetaData(grid).EntityName;

        return xform.MapUid is { } map ? MetaData(map).EntityName : Loc.GetString("fulton-place-unknown");
    }

    private void OnBeaconExamined(Entity<FultonBeaconComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fulton-beacon-examine-place", ("place", PlaceOf(ent))));
    }

    private void OnFultonExamined(Entity<FultonComponent> ent, ref ExaminedEvent args)
    {
        var beacons = _fulton.ExpandBeacons(ent);
        var groups = new List<(EntityUid? Beacon, int Count)>();
        foreach (var beacon in beacons)
        {
            if (groups.Count > 0 && groups[^1].Beacon == beacon)
                groups[^1] = (beacon, groups[^1].Count + 1);
            else
                groups.Add((beacon, 1));
        }

        if (groups.Count == 0)
            return;

        if (groups.Count == 1)
        {
            args.PushMarkup(groups[0].Beacon is { } single && !TerminatingOrDeleted(single)
                ? Loc.GetString("fulton-examine-linked", ("place", PlaceOf(single)))
                : Loc.GetString("fulton-examine-unlinked"));
            return;
        }

        // A merged stack: used one after another.
        args.PushMarkup(Loc.GetString("fulton-examine-queue"));
        for (var i = 0; i < groups.Count; i++)
        {
            var (beacon, count) = groups[i];
            var place = beacon is { } b && !TerminatingOrDeleted(b)
                ? PlaceOf(b)
                : Loc.GetString("fulton-place-unknown");
            args.PushMarkup(Loc.GetString("fulton-examine-queue-entry", ("index", i + 1), ("count", count), ("place", place)));
        }
    }

    /// <summary>No room in the bag: offer to merge with a stack linked to other beacons.</summary>
    private void OnInsertFailed(Entity<FultonComponent> ent, ref StorageStackInsertFailedEvent args)
    {
        if (args.User is not { } user ||
            !_players.TryGetSessionByEntity(user, out var session) ||
            _asking.Contains(session) ||
            !TryComp<StorageComponent>(args.Storage, out var storage))
        {
            return;
        }

        foreach (var contained in storage.Container.ContainedEntities)
        {
            if (!TryComp<FultonComponent>(contained, out var other) ||
                !TryComp<StackComponent>(contained, out var otherStack) ||
                !TryComp<StackComponent>(ent, out var stack) ||
                otherStack.StackTypeId != stack.StackTypeId ||
                _stack.GetAvailableSpace(otherStack) <= 0 ||
                _fulton.SameBeacons(ent, (contained, other)))
            {
                continue;
            }

            _asking.Add(session);
            _eui.OpenEui(new FultonMergeEui(this, ent.Owner, contained, args.Storage, user), session);
            return;
        }
    }

    /// <summary>The answer to «merge two fultons linked to different beacons?».</summary>
    public void Answer(FultonMergeEui eui, bool yes)
    {
        _asking.Remove(eui.Player);
        if (!yes || TerminatingOrDeleted(eui.Fulton) || TerminatingOrDeleted(eui.Target))
            return;

        _fulton.ForceMerge(eui.Fulton, eui.Target);
        _popup.PopupEntity(Loc.GetString("fulton-merge-done"), eui.User, eui.User);

        // What did not fit into that stack still tries the bag.
        if (!TerminatingOrDeleted(eui.Fulton) && TryComp<StackComponent>(eui.Fulton, out var left) && left.Count > 0)
            _storage.Insert(eui.Storage, eui.Fulton, out _, user: eui.User, playSound: false);
    }
}

public sealed class FultonMergeEui : BaseEui
{
    private readonly FultonBeaconInfoSystem _system;
    private bool _answered;

    public EntityUid Fulton { get; }
    public EntityUid Target { get; }
    public EntityUid Storage { get; }
    public EntityUid User { get; }

    public FultonMergeEui(FultonBeaconInfoSystem system, EntityUid fulton, EntityUid target, EntityUid storage, EntityUid user)
    {
        _system = system;
        Fulton = fulton;
        Target = target;
        Storage = storage;
        User = user;
    }

    public override void Opened() => StateDirty();

    public override EuiStateBase GetNewState()
    {
        return new YesNoEuiState(Loc.GetString("fulton-merge-title"), Loc.GetString("fulton-merge-text"));
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is not YesNoChoiceMessage choice || _answered)
            return;

        _answered = true;
        _system.Answer(this, choice.Button == YesNoUiButton.Yes);
        Close();
    }

    public override void Closed()
    {
        base.Closed();
        if (_answered)
            return;

        _answered = true;
        _system.Answer(this, false);
    }
}
