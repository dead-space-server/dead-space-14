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

public sealed partial class MarkingDyeSystem : EntitySystem
{
    public static readonly ProtoId<ShaderPrototype> ShaderId = "DSMarkingDye";

    private const float HeadBandBottom = 0.45f;
    private const float HeadBandTop = 0.95f;

    private static readonly Vector2 FullBand = new(0f, 1f);

    private static readonly HumanoidVisualLayers[] TorsoParts =
    {
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
    };

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private MarkingManager _markings = default!;
    [Dependency] private SpriteSystem _sprites = default!;

    public void ApplyMarkingDye(Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        MarkingPrototype proto,
        Marking marking)
    {
        var skin = ent.Comp1.BodyDye?.Cleaned();

        foreach (var specifier in proto.Sprites)
        {
            if (specifier is not SpriteSpecifier.Rsi rsi
                || !_sprites.LayerMapTryGet((ent.Owner, ent.Comp2), $"{proto.ID}-{rsi.RsiState}", out var index, false)
                || ent.Comp2[index] is not SpriteComponent.Layer layer)
            {
                continue;
            }

            if (proto.ForcedColoring)
            {
                Remove((ent.Owner, ent.Comp2), index);
                continue;
            }

            if (marking.Dye is { } own)
            {
                if (layer.Visible)
                    Paint(ent, index, layer, own.Cleaned(), IsHeadPart(proto.BodyPart));

                continue;
            }

            if (skin is { } inherited
                && _markings.MustMatchSkin(ent.Comp1.Species, proto.BodyPart, out _, _prototypes))
            {
                if (layer.Visible)
                    Paint(ent, index, layer, inherited, false);

                continue;
            }

            Remove((ent.Owner, ent.Comp2), index);
        }
    }

    public void ApplyBodyDye(Entity<HumanoidAppearanceComponent, SpriteComponent> ent)
    {
        var dye = ent.Comp1.BodyDye?.Cleaned();

        foreach (var part in TorsoParts)
        {
            if (!_sprites.LayerMapTryGet((ent.Owner, ent.Comp2), part, out var index, false)
                || ent.Comp2[index] is not SpriteComponent.Layer layer)
            {
                continue;
            }

            if (dye is not { } value || !layer.Visible)
            {
                Remove((ent.Owner, ent.Comp2), index);
                continue;
            }

            Paint(ent, index, layer, value, false);
        }
    }

    public void Remove(Entity<SpriteComponent> sprite, int index)
    {
        if (sprite.Comp[index] is not SpriteComponent.Layer layer)
            return;

        if (!IsDyeLayer(layer) || layer.Shader is not { } shader)
            return;

        sprite.Comp.LayerSetShader(index, (ShaderInstance?) null);
        shader.Dispose();
    }

    public static bool IsDyeLayer(SpriteComponent.Layer layer)
    {
        return layer.ShaderPrototype == ShaderId.Id;
    }

    private void Paint(Entity<HumanoidAppearanceComponent, SpriteComponent> ent,
        int index,
        SpriteComponent.Layer layer,
        MarkingDye dye,
        bool headBand)
    {
        var colors = layer.Color;
        var shader = ShaderOf(layer);

        shader.SetParameter("baseLab", Oklab(colors));
        shader.SetParameter("dyeStack", Stack(dye));
        shader.SetParameter("dyeSize", dye.Colors.Count);
        shader.SetParameter("dyePattern", (int) dye.Style);
        shader.SetParameter("dyeSpread", dye.Offset);
        shader.SetParameter("dyeBand", headBand ? new Vector2(HeadBandBottom, HeadBandTop) : FullBand);
        FeedFrames(shader, layer.ActualState);

        _sprites.LayerSetColor((ent.Owner, ent.Comp2), index, Color.White.WithAlpha(colors.A));

        if (layer.Shader != shader)
            ent.Comp2.LayerSetShader(index, shader, ShaderId.Id);
    }

    private ShaderInstance ShaderOf(SpriteComponent.Layer layer)
    {
        if (IsDyeLayer(layer) && layer.Shader is { } existing)
            return existing;

        return _prototypes.Index(ShaderId).InstanceUnique();
    }

    private static void FeedFrames(ShaderInstance shader, RSI.State? state)
    {
        if (state is not { } rsi)
        {
            shader.SetParameter("frameSize", Vector2.One);
            shader.SetParameter("dirBack", Vector4.Zero);
            shader.SetParameter("dirEast", Vector4.Zero);
            shader.SetParameter("dirWest", Vector4.Zero);
            return;
        }

        shader.SetParameter("frameSize", (Vector2) rsi.Size);

        var sided = rsi.RsiDirections != RsiDirectionType.Dir1 && !rsi.IsAnimated;
        shader.SetParameter("dirBack", sided ? Quad(rsi, RsiDirection.North) : Vector4.Zero);
        shader.SetParameter("dirEast", sided ? Quad(rsi, RsiDirection.East) : Vector4.Zero);
        shader.SetParameter("dirWest", sided ? Quad(rsi, RsiDirection.West) : Vector4.Zero);
    }

    private static float[] Stack(MarkingDye dye)
    {
        var lab = new float[MarkingDye.MaxColors * 3];

        for (var i = 0; i < dye.Colors.Count && i < MarkingDye.MaxColors; i++)
        {
            var color = dye.Colors[i].LabFromSrgb();
            var offset = i * 3;

            lab[offset] = color.X;
            lab[offset + 1] = color.Y;
            lab[offset + 2] = color.Z;
        }

        return lab;
    }

    private static Vector3 Oklab(Color color)
    {
        var lab = color.LabFromSrgb();
        return new Vector3(lab.X, lab.Y, lab.Z);
    }

    private static bool IsHeadPart(HumanoidVisualLayers part)
    {
        return part switch
        {
            HumanoidVisualLayers.Hair
                or HumanoidVisualLayers.FacialHair
                or HumanoidVisualLayers.Head
                or HumanoidVisualLayers.HeadSide
                or HumanoidVisualLayers.HeadTop
                or HumanoidVisualLayers.Snout
                or HumanoidVisualLayers.SnoutCover
                or HumanoidVisualLayers.Eyes => true,
            _ => false,
        };
    }

    private static Vector4 Quad(RSI.State state, RsiDirection direction)
    {
        if (state.GetFrame(direction, 0) is not AtlasTexture frame)
            return Vector4.Zero;

        var size = frame.SourceTexture.Size;
        var rect = frame.SubRegion;

        var quad = new Vector4(rect.Left, size.Y - rect.Bottom, rect.Right, size.Y - rect.Top);
        return quad / new Vector4(size.X, size.Y, size.X, size.Y);
    }
}
