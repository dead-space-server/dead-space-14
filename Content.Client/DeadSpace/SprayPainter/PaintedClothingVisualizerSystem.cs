using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Item;
using Content.Shared.SprayPainter.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.SprayPainter;

/// <summary>
/// Applies the visuals of the selected paint style to spray painted clothing.
/// </summary>
public sealed class PaintedClothingVisualizerSystem : VisualizerSystem<ClothingComponent>
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;

    protected override void OnAppearanceChange(EntityUid uid, ClothingComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, PaintableVisuals.Prototype, out var prototypeId, args.Component))
            return;

        if (!_prototype.TryIndex(prototypeId, out var prototype))
            return;

        var dummy = Spawn(prototypeId);

        SpriteSystem.CopySprite(dummy, uid);

        QueueDel(dummy);

        if (prototype.TryGetComponent<ItemComponent>(out var prototypeItem, _factory) &&
            TryComp<ItemComponent>(uid, out var item))
        {
            _item.CopyVisuals(uid, prototypeItem, item);
        }

        if (prototype.TryGetComponent<ClothingComponent>(out var prototypeClothing, _factory))
            _clothing.CopyVisuals(uid, prototypeClothing, component);
    }
}
