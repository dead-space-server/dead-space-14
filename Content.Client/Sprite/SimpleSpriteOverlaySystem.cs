using Content.Shared.Sprite;
using Robust.Client.GameObjects;

namespace Content.Client.Sprite;

public sealed class SimpleSpriteOverlaySystem : EntitySystem // DS14 - pre-v288 event/IoC style
{
    [Dependency] private readonly SpriteSystem _sprite = default!; // DS14 - pre-v288 IoC

    // DS14-Start - pre-v288 explicit event subscriptions
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SimpleSpriteOverlayComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<SimpleSpriteOverlayComponent, ComponentShutdown>(OnCompShutdown);
    }
    // DS14-End

    private void OnAfterHandleState(Entity<SimpleSpriteOverlayComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var sprite = Comp<SpriteComponent>(ent);

        var index = _sprite.LayerMapReserve((ent.Owner, sprite), ent.Comp.LayerMap);

        _sprite.LayerSetSprite((ent.Owner, sprite), index, ent.Comp.OverlaySprite);
        _sprite.LayerSetVisible((ent.Owner, sprite), index, true);

        if (ent.Comp.Shader is not null)
            sprite.LayerSetShader(index, ent.Comp.Shader);
    }

    private void OnCompShutdown(Entity<SimpleSpriteOverlayComponent> ent, ref ComponentShutdown args)
    {
        var sprite = Comp<SpriteComponent>(ent);

        if (_sprite.LayerMapTryGet((ent.Owner, sprite), ent.Comp.LayerMap, out var index, true))
            _sprite.LayerSetVisible((ent.Owner, sprite), index, false);
    }
}
