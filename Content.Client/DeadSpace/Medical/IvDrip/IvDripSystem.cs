// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Medical.IvDrip;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Medical.IvDrip;

public sealed class IvDripSystem : SharedIvDripSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<IvDripComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnAppearance(Entity<IvDripComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (_sprite.LayerMapTryGet((ent.Owner, sprite), IvDripVisuals.HasBag, out var bagLayer, false))
        {
            var hasBag = _appearance.TryGetData(ent, IvDripVisuals.HasBag, out bool bag) && bag;
            _sprite.LayerSetVisible((ent.Owner, sprite), bagLayer, hasBag);

            if (hasBag && _appearance.TryGetData(ent, IvDripVisuals.BagColor, out Color color))
                _sprite.LayerSetColor((ent.Owner, sprite), bagLayer, color);
            else
                _sprite.LayerSetColor((ent.Owner, sprite), bagLayer, Color.White);
        }

        if (_sprite.LayerMapTryGet((ent.Owner, sprite), IvDripVisuals.Speed, out var sliderLayer, false))
        {
            var speed = IvDripSpeed.Off;
            if (_appearance.TryGetData(ent, IvDripVisuals.Speed, out int speedInt))
                speed = (IvDripSpeed) speedInt;

            var state = speed switch
            {
                IvDripSpeed.Slow => "slider_slow",
                IvDripSpeed.Medium => "slider_medium",
                IvDripSpeed.Fast => "slider_fast",
                _ => "slider_off",
            };
            _sprite.LayerSetRsiState((ent.Owner, sprite), sliderLayer, state);
        }
    }
}
