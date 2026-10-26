// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NameIdentifier;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Server.Stack;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.NPC;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <inheritdoc cref="LavalandRockMimicComponent"/>
public sealed class LavalandRockMimicSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StackSystem _stack = default!;

    private readonly List<EntityUid> _watchers = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandRockMimicComponent, MapInitEvent>(OnMapInit, after: [typeof(NameIdentifierSystem)]);
        SubscribeLocalEvent<LavalandRockMimicComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<LavalandRockMimicComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMapInit(Entity<LavalandRockMimicComponent> ent, ref MapInitEvent args)
    {
        SetState(ent, LavalandRockMimicState.Rock);
        // A boulder cannot be dragged: neither can it, or it gives itself away.
        RemComp<PullableComponent>(ent);
        // Nor has a boulder a number after its name, as the lavaland's beasts get.
        if (Prototype(ent) is { } prototype)
            _metaData.SetEntityName(ent, prototype.Name);
    }

    /// <summary>Hit: it gets up and fights back (whom - its retaliation remembers).</summary>
    private void OnDamageChanged(Entity<LavalandRockMimicComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.Angry || !args.DamageIncreased || args.Origin is not { } origin || origin == ent.Owner ||
            !_mobState.IsAlive(ent))
        {
            return;
        }

        ent.Comp.Angry = true;
        RemComp<LavalandRockDisguiseComponent>(ent);
        StopFollowing(ent);
        EntityManager.AddComponents(ent, ent.Comp.AngryComponents, removeExisting: true);
        EnsureComp<ActiveNPCComponent>(ent);
        _npc.WakeNPC(ent);
        Wake(ent);
    }

    /// <summary>Broken: a dead mimic, and a handful of one ore.</summary>
    private void OnMobStateChanged(Entity<LavalandRockMimicComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.State == LavalandRockMimicState.Dead)
            return;

        StopFollowing(ent);
        SetState(ent, LavalandRockMimicState.Dead);
        if (ent.Comp.Ores.Count == 0)
            return;

        var ore = _random.Pick(ent.Comp.Ores);
        var amount = _random.Next(ent.Comp.OreMin, ent.Comp.OreMax + 1);
        _stack.SpawnMultipleAtPosition(ore, amount, Transform(ent).Coordinates);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandRockMimicComponent>();
        while (query.MoveNext(out var uid, out var mimic))
        {
            var ent = (uid, mimic);
            if (mimic.State == LavalandRockMimicState.Waking && now >= mimic.WakeUntil)
                SetState(ent, mimic.Angry ? LavalandRockMimicState.Awake : LavalandRockMimicState.Walking);

            if (mimic.State == LavalandRockMimicState.Dead)
                continue;

            // Angry, it fights as any beast: walking while it moves, standing up while it does not.
            if (mimic.Angry)
            {
                if (mimic.State != LavalandRockMimicState.Waking)
                {
                    var moving = TryComp<PhysicsComponent>(uid, out var body) && body.LinearVelocity.LengthSquared() > 0.01f;
                    var state = moving ? LavalandRockMimicState.Walking : LavalandRockMimicState.Awake;
                    if (state != mimic.State)
                        SetState(ent, state);
                }

                continue;
            }

            if (now < mimic.NextUpdate)
                continue;

            mimic.NextUpdate = now + mimic.UpdateInterval;
            UpdateDisguised(ent, now);
        }
    }

    /// <summary>
    /// Not yet hit: a boulder while anyone looks; seen anew, it may take a liking to one of those who see it; and
    /// while nobody looks, it creeps after them.
    /// </summary>
    private void UpdateDisguised(Entity<LavalandRockMimicComponent> ent, TimeSpan now)
    {
        var mimic = ent.Comp;
        GetWatchers(ent);
        var seen = _watchers.Count > 0;

        if (seen)
        {
            // Just come into someone's sight: maybe it will follow one of them.
            if (!mimic.WasSeen && mimic.Following == null && _random.Prob(mimic.FollowChance))
                mimic.Following = _random.Pick(_watchers);

            mimic.WasSeen = true;
            Freeze(ent);
            return;
        }

        mimic.WasSeen = false;
        if (mimic.Following is not { } target)
            return;

        if (TerminatingOrDeleted(target) || !_mobState.IsAlive(target) ||
            Transform(target).MapID != Transform(ent).MapID ||
            (_transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent)).Length() > mimic.LoseRange)
        {
            StopFollowing(ent);
            return;
        }

        // Nobody looks: up it gets and creeps after them.
        if (mimic.State == LavalandRockMimicState.Rock)
        {
            Wake(ent);
            return;
        }

        if (mimic.State != LavalandRockMimicState.Walking)
            return;

        EnsureComp<ActiveNPCComponent>(ent);
        // Relative to the one it follows, so the goal stays the same as they walk: re-registering a new spot every
        // check cancels the path still being searched, and it never set off.
        var steering = _steering.Register(ent, new EntityCoordinates(target, System.Numerics.Vector2.Zero));
        steering.Range = mimic.FollowDistance;
        steering.Status = SteeringStatus.Moving;
        steering.FailedPathCount = 0;
    }

    /// <summary>Players (alive, not ghosts) who see it now.</summary>
    private void GetWatchers(Entity<LavalandRockMimicComponent> ent)
    {
        _watchers.Clear();
        var mapId = Transform(ent).MapID;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { Valid: true } player ||
                HasComp<GhostComponent>(player) ||
                !_mobState.IsAlive(player) ||
                Transform(player).MapID != mapId ||
                !_examine.InRangeUnOccluded(player, ent.Owner, ent.Comp.SightRange))
            {
                continue;
            }

            _watchers.Add(player);
        }
    }

    private void Wake(Entity<LavalandRockMimicComponent> ent)
    {
        ent.Comp.WakeUntil = _timing.CurTime + ent.Comp.WakeTime;
        SetState(ent, LavalandRockMimicState.Waking);
    }

    /// <summary>Seen: a boulder again, right where it stands.</summary>
    private void Freeze(Entity<LavalandRockMimicComponent> ent)
    {
        if (ent.Comp.State == LavalandRockMimicState.Rock)
            return;

        _steering.Unregister(ent);
        RemComp<ActiveNPCComponent>(ent);
        if (TryComp<PhysicsComponent>(ent, out var body))
            _physics.SetLinearVelocity(ent, System.Numerics.Vector2.Zero, body: body);
        SetState(ent, LavalandRockMimicState.Rock);
    }

    private void StopFollowing(Entity<LavalandRockMimicComponent> ent)
    {
        ent.Comp.Following = null;
        if (!ent.Comp.Angry)
            Freeze(ent);
    }

    private void SetState(Entity<LavalandRockMimicComponent> ent, LavalandRockMimicState state)
    {
        ent.Comp.State = state;
        _appearance.SetData(ent, LavalandRockMimicVisuals.State, state);
    }
}
