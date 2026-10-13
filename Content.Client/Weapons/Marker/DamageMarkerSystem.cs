using Content.Shared.Weapons.Marker;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.Weapons.Marker;

public sealed class DamageMarkerSystem : SharedDamageMarkerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DamageMarkerComponent, ComponentStartup>(OnMarkerStartup);
        SubscribeLocalEvent<DamageMarkerComponent, AfterAutoHandleStateEvent>(OnMarkerAutoHandleState); // DS14
        SubscribeLocalEvent<DamageMarkerComponent, ComponentShutdown>(OnMarkerShutdown);
    }

    private void OnMarkerStartup(EntityUid uid, DamageMarkerComponent component, ComponentStartup args)
    {
        // DS14-start
        if (!_timing.ApplyingState)
            return;

        ApplyMarkerSprite(uid, component);
    }

    private void OnMarkerAutoHandleState(EntityUid uid, DamageMarkerComponent component, ref AfterAutoHandleStateEvent args)
    {
        ApplyMarkerSprite(uid, component);
    }

    private void ApplyMarkerSprite(EntityUid uid, DamageMarkerComponent component)
    {
        if (component.Effect == null || !TryComp<SpriteComponent>(uid, out var sprite))
        // DS14-end
            return;

        var layer = _sprite.LayerMapReserve((uid, sprite), DamageMarkerKey.Key);
        _sprite.LayerSetRsi((uid, sprite), layer, component.Effect.RsiPath, component.Effect.RsiState);
    }

    private void OnMarkerShutdown(EntityUid uid, DamageMarkerComponent component, ComponentShutdown args)
    {
        if (!_timing.ApplyingState || !TryComp<SpriteComponent>(uid, out var sprite) || !_sprite.LayerMapTryGet((uid, sprite), DamageMarkerKey.Key, out var weh, false))
            return;

        _sprite.RemoveLayer((uid, sprite), weh);
    }

    private enum DamageMarkerKey : byte
    {
        Key
    }
}
