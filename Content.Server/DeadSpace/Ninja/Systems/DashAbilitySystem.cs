using Content.Server.Beam;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;

namespace Content.Server.DeadSpace.Ninja.Systems;

public sealed class DashAbilitySystem : SharedDashAbilitySystem
{
    [Dependency] private readonly BeamSystem _beam = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DashRequestEvent>(OnDashRequest);
    }

    private void OnDashRequest(DashRequestEvent msg, EntitySessionEventArgs args)
    {
        var item = GetEntity(msg.Item);
        if (args.SenderSession.AttachedEntity is not { } user ||
            !TryComp<DashAbilityComponent>(item, out var comp))
            return;

        TryDash((item, comp), user, GetCoordinates(msg.Target), predicted: false);
    }

    protected override void DoTeleport(EntityUid user, EntityCoordinates target, string? beam = null, EntityUid? pulled = null, SoundSpecifier? sound = null)
    {
        var userXform = Transform(user);
        var point = Spawn(null, userXform.Coordinates);

        _transform.SetCoordinates(user, userXform, target);
        _transform.AttachToGridOrMap(user, userXform);

        _audio.PlayPvs(sound, user);

        if (beam != null)
            _beam.TryCreateBeam(point, user, beam);

        Del(point);

        if (pulled is { } pulledUid)
            _transform.SetCoordinates(pulledUid, target);
    }
}