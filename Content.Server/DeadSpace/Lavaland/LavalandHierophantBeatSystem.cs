// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Shared.Ghost;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Movement.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>
/// The beat of the Hierophant: tile-by-tile walking while blessed by the Hierophant staff or standing on an arena
/// with <c>tileMovement</c> (the Hierophant arena).
/// </summary>
public sealed class LavalandHierophantBeatSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan ArenaCheckInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextArenaCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandHierophantBeatComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<LavalandHierophantBeatComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<LavalandHierophantBeatComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<TileStepMovementComponent>(ent);
    }

    private void OnShutdown(Entity<LavalandHierophantBeatComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        RemCompDeferred<TileStepMovementComponent>(ent);
    }

    public bool IsBlessed(EntityUid uid)
    {
        return TryComp<LavalandHierophantBeatComponent>(uid, out var beat) && beat.Blessed;
    }

    /// <summary>Turns the walk from the blessing on or off.</summary>
    public void SetBlessed(EntityUid uid, bool blessed)
    {
        if (blessed)
        {
            var beat = EnsureComp<LavalandHierophantBeatComponent>(uid);
            beat.Blessed = true;
            Dirty(uid, beat);
            return;
        }

        if (!TryComp<LavalandHierophantBeatComponent>(uid, out var current))
            return;

        current.Blessed = false;
        Dirty(uid, current);
        if (!current.InArena)
            RemCompDeferred<LavalandHierophantBeatComponent>(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextArenaCheck)
            return;
        _nextArenaCheck = now + ArenaCheckInterval;

        var query = EntityQueryEnumerator<ActorComponent, InputMoverComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            var inArena = !HasComp<GhostComponent>(uid) &&
                          xform.GridUid is { } grid &&
                          TryComp<LavalandBossArenaComponent>(grid, out var arena) &&
                          arena.TileMovement;

            if (inArena)
            {
                var beat = EnsureComp<LavalandHierophantBeatComponent>(uid);
                if (!beat.InArena)
                {
                    beat.InArena = true;
                    Dirty(uid, beat);
                }
                continue;
            }

            if (!TryComp<LavalandHierophantBeatComponent>(uid, out var current) || !current.InArena)
                continue;

            current.InArena = false;
            Dirty(uid, current);
            if (!current.Blessed)
                RemCompDeferred<LavalandHierophantBeatComponent>(uid);
        }
    }
}
