// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Inventory;
using Content.Shared.DeadSpace.Wizard.Runes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneMirrorImageSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RuneMirrorImageComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RuneMirrorImageComponent, ComponentShutdown>(OnShutdown);
        _overlays.AddOverlay(new RuneMirrorOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<RuneMirrorOverlay>();
    }

    private void OnStartup(Entity<RuneMirrorImageComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.HiddenColor = sprite.Color;
        _sprite.SetColor((ent, sprite), sprite.Color.WithAlpha(0f));
    }

    private void OnShutdown(Entity<RuneMirrorImageComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.HiddenColor is { } color && TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), color);
    }
}

public sealed class RuneMirrorOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;

    private static readonly string[] HiddenSlots = { "back", "belt" };

    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;
    private readonly List<(string Key, bool Visible)> _hidden = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public RuneMirrorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entity.System<SpriteSystem>();
        _transform = _entity.System<SharedTransformSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eye = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var query = _entity.EntityQueryEnumerator<RuneMirrorImageComponent, TransformComponent>();
        while (query.MoveNext(out _, out var mirror, out var xform))
        {
            if (xform.MapID != args.MapId ||
                mirror.Source is not { } source ||
                !_entity.TryGetComponent(source, out SpriteComponent? sprite))
                continue;

            var (position, rotation) = _transform.GetWorldPositionRotation(xform);
            if (!args.WorldAABB.Contains(position))
                continue;

            HideSlots(source, sprite);
            _sprite.RenderSprite((source, sprite), handle, eye, rotation, position);
            RestoreSlots(source, sprite);
        }

        handle.SetTransform(System.Numerics.Matrix3x2.Identity);
    }

    private void HideSlots(EntityUid source, SpriteComponent sprite)
    {
        _hidden.Clear();
        if (!_entity.TryGetComponent(source, out InventorySlotsComponent? slots))
            return;

        foreach (var slot in HiddenSlots)
        {
            if (!slots.VisualLayerKeys.TryGetValue(slot, out var keys))
                continue;

            foreach (var key in keys)
            {
                if (!_sprite.LayerMapTryGet((source, sprite), key, out var index, false))
                    continue;

                _hidden.Add((key, sprite[index].Visible));
                _sprite.LayerSetVisible((source, sprite), index, false);
            }
        }
    }

    private void RestoreSlots(EntityUid source, SpriteComponent sprite)
    {
        foreach (var (key, visible) in _hidden)
        {
            if (_sprite.LayerMapTryGet((source, sprite), key, out var index, false))
                _sprite.LayerSetVisible((source, sprite), index, visible);
        }

        _hidden.Clear();
    }
}
