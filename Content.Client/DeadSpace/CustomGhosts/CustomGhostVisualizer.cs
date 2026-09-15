using Content.Shared.DeadSpace.CustomGhosts;
using Content.Shared.Ghost;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.CustomGhosts;

public sealed class CustomGhostVisualizer : VisualizerSystem<GhostComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, GhostComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<string>(uid, CustomGhostAppearance.Sprite, out var rsiPath, args.Component))
            SpriteSystem.LayerSetRsi((uid, args.Sprite), 0, new ResPath(rsiPath));

        var color = AppearanceSystem.TryGetData<Color>(uid,
            CustomGhostAppearance.ColorOverride,
            out var colorOverride,
            args.Component)
            ? colorOverride
            : Color.White;

        SpriteSystem.SetColor((uid, args.Sprite), color);
    }
}
