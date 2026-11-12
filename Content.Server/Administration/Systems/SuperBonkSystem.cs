using Content.Server.Administration.Components;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Administration.Systems;

public sealed partial class SuperBonkSystem : EntitySystem
{
<<<<<<< ours
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly ClimbSystem _climbSystem = default!; // DS14: current engine IoC requires readonly fields.
    [Dependency] private readonly IGameTiming _timing = default!;
||||||| base
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly ClumsySystem _clumsySystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
=======
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private ClimbSystem _climbSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
>>>>>>> theirs

<<<<<<< ours
    private EntityQuery<TransformComponent> _transformQuery;

    // DS14-start: current engine baseline uses explicit event subscriptions and query initialization.
    public override void Initialize()
    {
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<SuperBonkComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SuperBonkComponent, MobStateChangedEvent>(OnMobStateChanged);
    }
    // DS14-end
||||||| base
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperBonkComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<SuperBonkComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<SuperBonkComponent, ComponentShutdown>(OnShutdown);
    }
=======
    [Dependency] private EntityQuery<TransformComponent> _transformQuery;
>>>>>>> theirs

    [SubscribeLocalEvent]
    private void OnStartup(Entity<SuperBonkComponent> ent, ref ComponentStartup args)
    {
        var (uid, component) = ent;

        if (component.StopWhenDead && _mobState.IsDead(uid))
        {
            RemCompDeferred<SuperBonkComponent>(uid);
            return;
        }

        component.NextBonk = _timing.CurTime + component.BonkCooldown;

        var tables = EntityQueryEnumerator<BonkableComponent>();
        var bonks = new List<EntityUid>();
        while (tables.MoveNext(out var table, out _))
        {
            bonks.Add(table);
        }

        component.Tables = bonks.GetEnumerator();
        if (!component.Tables.MoveNext())
            RemCompDeferred<SuperBonkComponent>(uid);
    }

<<<<<<< ours
||||||| base
    private void OnShutdown(Entity<SuperBonkComponent> ent, ref ComponentShutdown args)
    {
        var (uid, component) = ent;

        if (component.RemoveClumsy)
            RemComp<ClumsyComponent>(uid);
    }

=======
    [SubscribeLocalEvent]
    private void OnMobStateChanged(Entity<SuperBonkComponent> ent, ref MobStateChangedEvent args)
    {
        var (uid, component) = ent;

        if (component.StopWhenDead && args.NewMobState == MobState.Dead)
            RemCompDeferred<SuperBonkComponent>(uid);
    }

>>>>>>> theirs
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var comps = EntityQueryEnumerator<SuperBonkComponent>();

        while (comps.MoveNext(out var uid, out var comp))
        {
            if (comp.NextBonk > _timing.CurTime)
                continue;

            if (!TryBonk(uid, comp.Tables.Current) || !comp.Tables.MoveNext())
            {
                RemComp<SuperBonkComponent>(uid);
                continue;
            }

            comp.NextBonk += comp.BonkCooldown;
        }
    }

    private bool TryBonk(EntityUid uid, EntityUid tableUid)
    {
        // It would be very weird for something without a transform component to have a bonk component
        // but just in case because I don't want to crash the server.
        if (!_transformQuery.HasComp(tableUid))
            return false;

        _transformSystem.SetCoordinates(uid, Transform(tableUid).Coordinates);
        _climbSystem.Bonk(tableUid, uid);

        return true;
    }
}
