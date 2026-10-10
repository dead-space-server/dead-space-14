using Content.Shared.Standing;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;
using JetBrains.Annotations;

namespace Content.Shared.Sectants;

/// <summary>Shared helpers for sectant alpha entities.</summary>
[UsedImplicitly]
public sealed class SectantSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public bool IsAlive(EntityUid uid) => _mobState.IsAlive(uid);

    public bool IsDown(EntityUid uid) => _standing.IsDown(uid);

    public bool CanTeleport(EntityUid uid, SectantRandomTeleportOnDamageComponent comp)
        => _timing.CurTime >= comp.LastTeleport + TimeSpan.FromSeconds(comp.Cooldown);
}
