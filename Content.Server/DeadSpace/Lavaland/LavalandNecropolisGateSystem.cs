// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Necropolis gates (open / close by hand, the Legion gate) and two-part necropolis structures.</summary>
public sealed class LavalandNecropolisGateSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly LavalandLegionMegafaunaSystem _legion = default!;
    [Dependency] private readonly LavalandBossArenaSystem _arenas = default!;
    [Dependency] private readonly OccluderSystem _occluder = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandNecropolisGateComponent, MapInitEvent>(OnGateMapInit);
        SubscribeLocalEvent<LavalandNecropolisGateComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<LavalandStructureFrontComponent, MapInitEvent>(OnFrontMapInit);
        SubscribeLocalEvent<LavalandStructureFrontComponent, ComponentShutdown>(OnFrontShutdown);
    }

    private void OnGateMapInit(Entity<LavalandNecropolisGateComponent> ent, ref MapInitEvent args)
    {
        SetBlocking(ent, ent.Comp.State != LavalandNecropolisGateState.Open);
        _appearance.SetData(ent, LavalandNecropolisGateVisuals.State, ent.Comp.State);
    }

    private void OnInteractHand(Entity<LavalandNecropolisGateComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var gate = ent.Comp;
        if (gate.State is LavalandNecropolisGateState.Opening or LavalandNecropolisGateState.Closing || gate.KnockEnd != null)
            return;

        if (gate.Locked)
        {
            _popup.PopupEntity(Loc.GetString(gate.State == LavalandNecropolisGateState.Open
                ? "lavaland-necropolis-gate-stuck"
                : "lavaland-necropolis-gate-locked"), ent, args.User);
            return;
        }

        if (gate.LegionGate)
        {
            if (gate.State != LavalandNecropolisGateState.Closed)
                return;

            gate.KnockEnd = _timing.CurTime + gate.KnockTime;
            gate.Knocker = args.User;
            _audio.PlayPvs(gate.KnockSound, ent);
            _popup.PopupEntity(Loc.GetString("lavaland-necropolis-gate-knock"), ent, args.User, PopupType.MediumCaution);
            return;
        }

        if (gate.State == LavalandNecropolisGateState.Open)
            StartClosing(ent);
        else
            StartOpening(ent);
    }

    private void StartOpening(Entity<LavalandNecropolisGateComponent> ent)
    {
        ent.Comp.State = LavalandNecropolisGateState.Opening;
        ent.Comp.StageEnd = _timing.CurTime + ent.Comp.OpenTime;
        _audio.PlayPvs(ent.Comp.OpenSound, ent);
        _popup.PopupEntity(Loc.GetString("lavaland-necropolis-gate-opening"), ent);
        _appearance.SetData(ent, LavalandNecropolisGateVisuals.State, ent.Comp.State);
    }

    /// <summary>Burst open by force (the Legion): open at once and for good.</summary>
    public void Smash(Entity<LavalandNecropolisGateComponent> ent)
    {
        if (ent.Comp.State == LavalandNecropolisGateState.Open)
            return;

        ent.Comp.KnockEnd = null;
        ent.Comp.StageEnd = null;
        ent.Comp.BlockAt = null;
        ent.Comp.Locked = true;
        ent.Comp.State = LavalandNecropolisGateState.Open;
        SetBlocking(ent, false);
        _audio.PlayPvs(ent.Comp.OpenSound, ent);
        _appearance.SetData(ent, LavalandNecropolisGateVisuals.State, ent.Comp.State);
    }

    private void StartClosing(Entity<LavalandNecropolisGateComponent> ent)
    {
        ent.Comp.State = LavalandNecropolisGateState.Closing;
        ent.Comp.StageEnd = _timing.CurTime + ent.Comp.CloseTime;
        ent.Comp.BlockAt = _timing.CurTime + ent.Comp.CloseBlockDelay;
        _audio.PlayPvs(ent.Comp.CloseSound, ent);
        _popup.PopupEntity(Loc.GetString("lavaland-necropolis-gate-closing"), ent, PopupType.MediumCaution);
        _appearance.SetData(ent, LavalandNecropolisGateVisuals.State, ent.Comp.State);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandNecropolisGateComponent>();
        while (query.MoveNext(out var uid, out var gate))
        {
            var ent = (uid, gate);
            if (gate.KnockEnd is { } knockEnd && now >= knockEnd)
            {
                gate.KnockEnd = null;
                gate.Locked = true;
                ReleaseLegion(ent);

                // The knock is answered: the arena plays its gate-break intro (it breaks the gate itself),
                // or the Legion appears behind the gate as it grinds open.
                var gateGrid = Transform(uid).GridUid;
                if (gate.LegionGate && gateGrid is { } introGrid && _arenas.TryBeginGateIntro(introGrid, ent, gate.Knocker))
                    continue;

                if (gate.LegionGate && gateGrid is { } summonGrid)
                    _arenas.TrySummonBoss(summonGrid);

                StartOpening(ent);
            }

            if (gate.BlockAt is { } blockAt && now >= blockAt)
            {
                gate.BlockAt = null;
                SetBlocking(ent, true);
            }

            if (gate.StageEnd is not { } stageEnd || now < stageEnd)
                continue;

            gate.StageEnd = null;
            if (gate.State == LavalandNecropolisGateState.Opening)
            {
                gate.State = LavalandNecropolisGateState.Open;
                SetBlocking(ent, false);

                // The way is open: the Legion waiting behind it lunges at whoever knocked.
                if (gate.LegionGate)
                    _legion.WakeNear(_transform.GetMapCoordinates(uid), gate.WakeRange, gate.Knocker);
            }
            else if (gate.State == LavalandNecropolisGateState.Closing)
            {
                gate.State = LavalandNecropolisGateState.Closed;
                SetBlocking(ent, true);
            }

            _appearance.SetData(uid, LavalandNecropolisGateVisuals.State, gate.State);
        }
    }

    /// <summary>Everyone on the map hears that something horrible was released.</summary>
    private void ReleaseLegion(Entity<LavalandNecropolisGateComponent> ent)
    {
        var mapId = Transform(ent).MapID;
        var filter = Filter.Empty().AddInMap(mapId);
        _audio.PlayGlobal(ent.Comp.ReleaseSound, filter, true);

        var players = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (players.MoveNext(out var player, out _, out var xform))
        {
            if (xform.MapID == mapId)
                _popup.PopupEntity(Loc.GetString("lavaland-necropolis-gate-released"), player, player, PopupType.LargeCaution);
        }
    }

    /// <summary>A closed gate blocks the way and the view.</summary>
    private void SetBlocking(Entity<LavalandNecropolisGateComponent> ent, bool blocking)
    {
        if (TryComp<PhysicsComponent>(ent, out var body))
            _physics.SetCanCollide(ent, blocking, body: body);
        if (TryComp<OccluderComponent>(ent, out var occluder))
            _occluder.SetEnabled(ent, blocking, occluder);
    }

    private void OnFrontMapInit(Entity<LavalandStructureFrontComponent> ent, ref MapInitEvent args)
    {
        var front = Spawn(ent.Comp.Front, Transform(ent).Coordinates);
        _transform.SetLocalRotation(front, Transform(ent).LocalRotation);
        ent.Comp.Spawned = front;
    }

    private void OnFrontShutdown(Entity<LavalandStructureFrontComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Spawned is { } front && !TerminatingOrDeleted(front))
            QueueDel(front);
    }
}
