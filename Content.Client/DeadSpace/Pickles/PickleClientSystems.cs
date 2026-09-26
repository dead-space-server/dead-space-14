// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Pickles;
using Content.Shared.DeadSpace.Pickles.Components;
using Robust.Client.GameObjects;
using System.Numerics;

namespace Content.Client.DeadSpace.Pickles;

public sealed class FermentationBarrelSystem : SharedFermentationSystem;

public sealed class PickleJarSystem : SharedPickleJarSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PickleJarComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAppearance(Entity<PickleJarComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!_sprite.LayerMapTryGet((ent.Owner, sprite), PickleJarVisuals.HasProduce, out var layer, false))
            return;

        if (!_appearance.TryGetData(ent, PickleJarVisuals.ProduceCount, out int count) || count <= 0)
        {
            _sprite.LayerSetVisible((ent.Owner, sprite), layer, false);
            return;
        }

        var style = ent.Comp.ContentsStyle;
        if (_appearance.TryGetData(ent, PickleJarVisuals.ContentsStyle, out string? styleData) &&
            !string.IsNullOrEmpty(styleData))
            style = styleData;

        var clamped = Math.Clamp(count, 1, 4);
        var state = $"{style}-{clamped}";
        if (sprite.BaseRSI != null && !sprite.BaseRSI.TryGetState(state, out _))
            state = $"cucumber-{clamped}";

        _sprite.LayerSetVisible((ent.Owner, sprite), layer, true);
        _sprite.LayerSetRsiState((ent.Owner, sprite), layer, state);
        _sprite.LayerSetColor((ent.Owner, sprite), layer, Color.White);
    }
}

public sealed class PickledProduceSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PickledProduceComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PickledProduceComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<PickledProduceComponent, AppearanceChangeEvent>(OnAppearance);
        SubscribeLocalEvent<PickledProduceComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnStartup(Entity<PickledProduceComponent> ent, ref ComponentStartup args) => ApplyTint(ent);
    private void OnState(Entity<PickledProduceComponent> ent, ref AfterAutoHandleStateEvent args) => ApplyTint(ent);
    private void OnAppearance(Entity<PickledProduceComponent> ent, ref AppearanceChangeEvent args) => ApplyTint(ent);
    private void OnParentChanged(Entity<PickledProduceComponent> ent, ref EntParentChangedMessage args) => ApplyTint(ent);

    private void ApplyTint(Entity<PickledProduceComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetColor((ent.Owner, sprite), Color.White);
        _sprite.SetScale((ent.Owner, sprite), new Vector2(ent.Comp.SpriteScale, ent.Comp.SpriteScale));

        var i = 0;
        foreach (var _ in sprite.AllLayers)
        {
            _sprite.LayerSetColor((ent.Owner, sprite), i, ent.Comp.Tint);
            i++;
        }
    }
}
