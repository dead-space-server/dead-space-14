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
    [Dependency] private MarkingManager _markingManager = default!;
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

    private static readonly HashSet<HumanoidVisualLayers> BodyLayers =
    [
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    ];

    public void ApplyMarkingDye(Entity<HumanoidAppearanceComponent, SpriteComponent> ent, MarkingPrototype proto, Marking marking)
    {
        var bodyDye = ent.Comp1.BodyDye?.Sanitized();

        foreach (var spriteSpecifier in proto.Sprites)
        {
            if (spriteSpecifier is not SpriteSpecifier.Rsi rsi
                || !_sprite.LayerMapTryGet((ent.Owner, ent.Comp2), $"{proto.ID}-{rsi.RsiState}", out var index, false)
                || ent.Comp2[index] is not SpriteComponent.Layer layer)
                continue;

            if (marking.Dye is { } dye && !proto.ForcedColoring)
            {
                if (layer.Visible)
                    ApplyToLayer(ent, index, layer, proto, dye.Sanitized(), bodyMapped: false);
                continue;
            }

            if (bodyDye is { } skinDye
                && !proto.ForcedColoring
                && _markingManager.MustMatchSkin(ent.Comp1.Species, proto.BodyPart, out _, _proto))
            {
                if (layer.Visible)
                    ApplyToLayer(ent, index, layer, proto, skinDye, bodyMapped: true);
                continue;
            }

            Remove((ent.Owner, ent.Comp2), index);
        }
    }

    public void ApplyBodyDye(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var dye = ent.Comp1.BodyDye?.Sanitized();

        foreach (var part in BodyLayers)
        {
            if (!_sprite.LayerMapTryGet((ent.Owner, ent.Comp2), part, out var index, false))
                continue;

            if (ent.Comp2[index] is not SpriteComponent.Layer layer)
                continue;

            if (dye is not { } value || !layer.Visible)
            {
                Remove((ent.Owner, ent.Comp2), index);
                continue;
            }

            ApplyToLayer(ent, index, layer, null, value, bodyMapped: true);
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
        Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        int index,
        SpriteComponent.Layer layer,
        MarkingPrototype? proto,
        MarkingDye dye,
        bool bodyMapped)
    {
        var baseColor = layer.Color;
        var region = bodyMapped || proto == null || !HeadLayers.Contains(proto.BodyPart)
            ? FullRegion
            : HeadRegion;
        var state = layer.ActualState;

        var shader = IsDyeLayer(layer) && layer.Shader is { } existing
            ? existing
            : _proto.Index(ShaderId).InstanceUnique();

        shader.SetParameter("baseLab", ToOklab(baseColor));
        shader.SetParameter("dyeLab", DyeLabArray(dye));
        shader.SetParameter("dyeCount", dye.Colors.Count);
        shader.SetParameter("dyeStyle", (int) dye.Style);
        shader.SetParameter("amount", dye.Offset);
        shader.SetParameter("region", region);

        if (state is { } rsiState)
        {
            shader.SetParameter("frameSize", (Vector2) rsiState.Size);
            var directional = rsiState.RsiDirections != RsiDirectionType.Dir1 && !rsiState.IsAnimated;
            shader.SetParameter("frameBack", directional ? FrameUv(rsiState, RsiDirection.North) : Vector4.Zero);
            shader.SetParameter("frameEast", directional ? FrameUv(rsiState, RsiDirection.East) : Vector4.Zero);
            shader.SetParameter("frameWest", directional ? FrameUv(rsiState, RsiDirection.West) : Vector4.Zero);
        }
        else
        {
            shader.SetParameter("frameSize", Vector2.One);
            shader.SetParameter("frameBack", Vector4.Zero);
            shader.SetParameter("frameEast", Vector4.Zero);
            shader.SetParameter("frameWest", Vector4.Zero);
        }

        _sprite.LayerSetColor((ent.Owner, ent.Comp2), index, Color.White.WithAlpha(baseColor.A));

        if (layer.Shader != shader)
            ent.Comp2.LayerSetShader(index, shader, ShaderId.Id);
    }

    private static float[] DyeLabArray(MarkingDye dye)
    {
        var lab = new float[MarkingDye.MaxColors * 3];
        for (var i = 0; i < dye.Colors.Count && i < MarkingDye.MaxColors; i++)
        {
            var oklab = dye.Colors[i].LabFromSrgb();
            lab[i * 3] = oklab.X;
            lab[i * 3 + 1] = oklab.Y;
            lab[i * 3 + 2] = oklab.Z;
        }

        return lab;
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
