using Content.Server.Beam;
using Robust.Shared.Map;
using Content.Shared.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class DashAbilitySystem : SharedDashAbilitySystem
{
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void DoTeleport(EntityUid user, EntityCoordinates target, string? beam = null, EntityUid? pulled = null)
    {
        var userXform = Transform(user);
        var point = Spawn(null, userXform.Coordinates);

        _transform.SetCoordinates(user, userXform, target);
        _transform.AttachToGridOrMap(user, userXform);

        if (beam != null)
            _beam.TryCreateBeam(point, user, beam);

        Del(point);

        if (pulled is { } pulledUid)
            _transform.SetCoordinates(pulledUid, target);
    }
}