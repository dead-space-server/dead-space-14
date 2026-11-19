// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.Stylesheets.Colorspace;
using Content.Shared.DeadSpace.Humanoid.Markings;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Humanoid;

public sealed class MarkingDyeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public static readonly ProtoId<ShaderPrototype> ShaderId = "DSMarkingDye";

    private static readonly Vector2 HeadRegion = new(0.45f, 0.95f);
    private static readonly Vector2 FullRegion = new(0f, 1f);

    private static readonly HashSet<HumanoidVisualLayers> HeadLayers =
    [
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.SnoutCover,
        HumanoidVisualLayers.Eyes,
    ];

    public void Apply(Entity<SpriteComponent> sprite, MarkingPrototype proto, Marking marking)
    {
        foreach (var spriteSpecifier in proto.Sprites)
        {
            if (spriteSpecifier is not SpriteSpecifier.Rsi rsi
                || !_sprite.LayerMapTryGet(sprite.AsNullable(), $"{proto.ID}-{rsi.RsiState}", out var index, false)
                || sprite.Comp[index] is not SpriteComponent.Layer layer)
                continue;

            if (marking.Dye is not { } dye || proto.ForcedColoring)
            {
                Remove(sprite, index);
                continue;
            }

            if (layer.Visible)
                ApplyToLayer(sprite, index, layer, proto, rsi, dye.Sanitized());
        }
    }

    public void Remove(Entity<SpriteComponent> sprite, int index)
    {
        if (sprite.Comp[index] is not SpriteComponent.Layer { Shader: { } shader } layer
            || !IsDyeLayer(layer))
            return;

        sprite.Comp.LayerSetShader(index, (ShaderInstance?) null);
        shader.Dispose();
    }

    public static bool IsDyeLayer(SpriteComponent.Layer layer)
    {
        return layer.ShaderPrototype == ShaderId.Id;
    }

    private void ApplyToLayer(
        Entity<SpriteComponent> sprite,
        int index,
        SpriteComponent.Layer layer,
        MarkingPrototype proto,
        SpriteSpecifier.Rsi rsi,
        MarkingDye dye)
    {
        var baseColor = layer.Color;
        var state = _sprite.GetState(rsi);

        var shader = IsDyeLayer(layer) && layer.Shader is { } existing
            ? existing
            : _proto.Index(ShaderId).InstanceUnique();

        shader.SetParameter("baseLab", ToOklab(baseColor));
        shader.SetParameter("dyeLab", ToOklab(dye.Color));
        shader.SetParameter("dyeStyle", (int) dye.Style);
        shader.SetParameter("amount", dye.Offset);
        shader.SetParameter("region", HeadLayers.Contains(proto.BodyPart) ? HeadRegion : FullRegion);
        shader.SetParameter("frameSize", (Vector2) state.Size);

        var directional = state.RsiDirections != RsiDirectionType.Dir1 && !state.IsAnimated;
        shader.SetParameter("frameBack", directional ? FrameUv(state, RsiDirection.North) : Vector4.Zero);
        shader.SetParameter("frameEast", directional ? FrameUv(state, RsiDirection.East) : Vector4.Zero);
        shader.SetParameter("frameWest", directional ? FrameUv(state, RsiDirection.West) : Vector4.Zero);

        _sprite.LayerSetColor(sprite.AsNullable(), index, Color.White.WithAlpha(baseColor.A));

        if (layer.Shader != shader)
            sprite.Comp.LayerSetShader(index, shader, ShaderId.Id);
    }

    private static Vector3 ToOklab(Color color)
    {
        var lab = color.LabFromSrgb();
        return new Vector3(lab.X, lab.Y, lab.Z);
    }

    private static Vector4 FrameUv(RSI.State state, RsiDirection direction)
    {
        if (state.GetFrame(direction, 0) is not AtlasTexture frame)
            return Vector4.Zero;

        var (width, height) = frame.SourceTexture.Size;
        var region = frame.SubRegion;
        return new Vector4(
            region.Left / width,
            (height - region.Bottom) / height,
            region.Right / width,
            (height - region.Top) / height);
    }
}
