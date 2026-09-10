// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Linq;
using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Content.Server.Chat.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Map;

namespace Content.Server.DeadSpace.Lavaland;

public sealed class LavalandBluespaceWellSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LavalandBluespaceWellComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, InteractUsingEvent>(OnInsert);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, GetVerbsEvent<AlternativeVerb>>(OnUnloadVerb);
        SubscribeLocalEvent<LavalandBluespaceWellComponent, GetVerbsEvent<Verb>>(OnSendVerb);
    }

    private void OnInit(Entity<LavalandBluespaceWellComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Ore = _containers.EnsureContainer<Container>(ent, "lavaland-well-ore");
        ent.Comp.NextWaveCheck = _timing.CurTime + ent.Comp.WaveCheckInterval;
    }

    private int Count(LavalandBluespaceWellComponent comp)
    {
        var count = 0;
        foreach (var uid in comp.Ore.ContainedEntities)
        {
            if (TryComp<StackComponent>(uid, out var stack))
                count += stack.Count;
        }
        return count;
    }

    private bool Insert(Entity<LavalandBluespaceWellComponent> ent, EntityUid ore)
    {
        return ent.Comp.ReturnAt == null && TryComp<StackComponent>(ore, out var stack) &&
               ent.Comp.OreTypes.Contains(stack.StackTypeId) && _containers.Insert(ore, ent.Comp.Ore);
    }

    private void OnInsert(Entity<LavalandBluespaceWellComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.ReturnAt != null)
            return;
        if (TryComp<StorageComponent>(args.Used, out var storage))
        {
            foreach (var ore in storage.Container.ContainedEntities.ToArray())
                args.Handled |= Insert(ent, ore);
        }
        else
            args.Handled = Insert(ent, args.Used);
    }

    private void OnExamine(Entity<LavalandBluespaceWellComponent> ent, ref ExaminedEvent args)
    {
        var count = Count(ent.Comp);
        var percent = Math.Clamp(count * 100L / Math.Max(1, ent.Comp.RequiredOre), 0, 100);
        args.PushMarkup(Loc.GetString("lavaland-well-progress", ("count", count),
            ("required", ent.Comp.RequiredOre), ("percent", percent)));
    }

    private void OnUnloadVerb(Entity<LavalandBluespaceWellComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.ReturnAt != null)
            return;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("lavaland-well-unload"),
            Act = () => Unload(ent),
        });
    }

    private void Unload(Entity<LavalandBluespaceWellComponent> ent)
    {
        if (Deleted(ent) || ent.Comp.ReturnAt != null)
            return;
        foreach (var ore in ent.Comp.Ore.ContainedEntities.ToArray())
            _containers.Remove(ore, ent.Comp.Ore);
        ent.Comp.GroundSince.Clear();
    }

    private void OnSendVerb(Entity<LavalandBluespaceWellComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.ReturnAt != null || Count(ent.Comp) < ent.Comp.RequiredOre)
            return;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("lavaland-well-send"),
            Act = () =>
            {
                if (Deleted(ent) || ent.Comp.ReturnAt != null || Count(ent.Comp) < ent.Comp.RequiredOre)
                    return;
                foreach (var ore in ent.Comp.Ore.ContainedEntities.ToArray())
                    QueueDel(ore);
                ent.Comp.ReturnAt = _timing.CurTime + ent.Comp.ReturnDelay;
                ent.Comp.GroundSince.Clear();
                ent.Comp.ReturnCoordinates = Transform(ent).Coordinates;
                TeleportEffect(ent);
                _transform.DetachEntity(ent, Transform(ent));
            },
        });
    }

    private void TeleportEffect(Entity<LavalandBluespaceWellComponent> ent)
    {
        Spawn(ent.Comp.TeleportEffect, Transform(ent).Coordinates);
        _audio.PlayPvs(ent.Comp.TeleportSound, Transform(ent).Coordinates);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var wells = EntityQueryEnumerator<LavalandBluespaceWellComponent, TransformComponent>();
        while (wells.MoveNext(out var uid, out var well, out var transform))
        {
            if (well.ReturnAt is { } returnAt)
            {
                if (now < returnAt)
                    continue;
                well.ReturnAt = null;
                if (!Exists(well.ReturnCoordinates.EntityId))
                {
                    QueueDel(uid);
                    continue;
                }
                _transform.SetCoordinates(uid, well.ReturnCoordinates);
                _transform.AnchorEntity((uid, transform));
                TeleportEffect((uid, well));
            }
            UpdateWave((uid, well, transform), now);
            if (well.NextScan > now)
                continue;
            well.NextScan = now + well.ScanInterval;
            if (transform.MapUid is not { } map || !HasComp<LavalandMapComponent>(map) || !transform.Anchored)
            {
                well.GroundSince.Clear();
                continue;
            }

            var candidates = new HashSet<EntityUid>();
            var ores = EntityQueryEnumerator<StackComponent, TransformComponent>();
            while (ores.MoveNext(out var ore, out var stack, out var oreTransform))
            {
                if (oreTransform.MapUid != map || !well.OreTypes.Contains(stack.StackTypeId) ||
                    _containers.IsEntityInContainer(ore))
                    continue;
                candidates.Add(ore);
                if (!well.GroundSince.TryGetValue(ore, out var since))
                    well.GroundSince[ore] = now;
                else if (now - since >= well.CollectionDelay)
                    Insert((uid, well), ore);
            }
            foreach (var ore in well.GroundSince.Keys.ToArray())
            {
                if (!candidates.Contains(ore))
                    well.GroundSince.Remove(ore);
            }
        }
    }

    private void UpdateWave(Entity<LavalandBluespaceWellComponent, TransformComponent> ent, TimeSpan now)
    {
        if (!ent.Comp1.WavesEnabled || ent.Comp1.ReturnAt != null ||
            ent.Comp2.MapUid is not { } mapUid || !HasComp<LavalandMapComponent>(mapUid))
            return;

        var filter = Filter.Empty().AddInMap(ent.Comp2.MapID, EntityManager);
        if (ent.Comp1.WaveStage == LavalandWellWaveStage.Idle)
        {
            if (now < ent.Comp1.NextWaveCheck)
                return;
            ent.Comp1.NextWaveCheck = now + ent.Comp1.WaveCheckInterval;
            if (!_random.Prob(ent.Comp1.WaveChance) || !TryFindOutpost(mapUid, out _))
                return;
            ent.Comp1.WaveStage = LavalandWellWaveStage.Warning;
            ent.Comp1.WaveStageEnd = now + ent.Comp1.WaveWarningDuration;
            ent.Comp1.NextTimerUpdate = TimeSpan.Zero;
            _chat.DispatchGlobalAnnouncement(Loc.GetString("lavaland-well-wave-warning"), playSound: true,
                announcementSound: ent.Comp1.WaveWarningSound,
                colorOverride: Color.Red);
        }

        if (ent.Comp1.WaveStage == LavalandWellWaveStage.Warning && now >= ent.Comp1.WaveStageEnd)
        {
            ent.Comp1.WaveStage = LavalandWellWaveStage.Active;
            ent.Comp1.WaveStageEnd = now + ent.Comp1.WaveDuration;
            ent.Comp1.NextTimerUpdate = TimeSpan.Zero;
            SpawnWave(ent, mapUid);
            _chat.DispatchGlobalAnnouncement(Loc.GetString("lavaland-well-wave-active"), playSound: false,
                colorOverride: Color.CornflowerBlue);
            if (ent.Comp1.WaveMusic.Count > 0)
                _audio.PlayGlobal(_random.Pick(ent.Comp1.WaveMusic), filter, false);
        }

        if (ent.Comp1.WaveStage == LavalandWellWaveStage.Active && now >= ent.Comp1.WaveStageEnd)
        {
            foreach (var waveEntity in ent.Comp1.WaveEntities)
            {
                if (Exists(waveEntity))
                    QueueDel(waveEntity);
            }
            ent.Comp1.WaveEntities.Clear();
            ent.Comp1.WaveStage = LavalandWellWaveStage.Idle;
            RaiseNetworkEvent(new LavalandWellWaveTimerHideEvent(), filter);
            return;
        }

        if (ent.Comp1.NextTimerUpdate > now)
            return;
        ent.Comp1.NextTimerUpdate = now + TimeSpan.FromSeconds(1);
        var seconds = Math.Max(0, (int) Math.Ceiling((ent.Comp1.WaveStageEnd - now).TotalSeconds));
        RaiseNetworkEvent(new LavalandWellWaveTimerEvent(seconds,
            ent.Comp1.WaveStage == LavalandWellWaveStage.Warning), filter);
    }

    private bool TryFindOutpost(EntityUid mapUid, out Entity<TransformComponent> outpost)
    {
        var query = EntityQueryEnumerator<LavalandOutpostComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var marker, out var transform))
        {
            if (marker.Map != mapUid)
                continue;
            outpost = (uid, transform);
            return true;
        }
        outpost = default;
        return false;
    }

    private void SpawnWave(Entity<LavalandBluespaceWellComponent, TransformComponent> ent, EntityUid mapUid)
    {
        if (!TryFindOutpost(mapUid, out var outpost) || ent.Comp1.WaveTendrils.Count == 0)
            return;
        var center = _transform.GetMapCoordinates(outpost);
        foreach (var prototype in ent.Comp1.WaveTendrils)
        {
            var angle = _random.NextAngle();
            var distance = _random.NextFloat(ent.Comp1.WaveMinDistance, ent.Comp1.WaveMaxDistance);
            var coordinates = new EntityCoordinates(mapUid, center.Position + angle.ToWorldVec() * distance);
            var tendril = Spawn(prototype, coordinates);
            var despawn = EnsureComp<TimedDespawnComponent>(tendril);
            despawn.Lifetime = (float) ent.Comp1.WaveDuration.TotalSeconds;
            ent.Comp1.WaveEntities.Add(tendril);
        }
    }
}
