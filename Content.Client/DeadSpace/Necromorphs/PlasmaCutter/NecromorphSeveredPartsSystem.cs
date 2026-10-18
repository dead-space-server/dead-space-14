// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Necromorphs.PlasmaCutter;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Necromorphs.PlasmaCutter;

/// <summary>
/// Cuts the legs and head a plasma cutter took off a necromorph out of every layer of its sprite: the body part
/// layers go with the parts, but a suit or a helmet (or a one-piece sprite like the mad miner's) keeps drawing them.
/// Where each part is drawn depends on the sprite's layout and on which way it faces.
/// </summary>
public sealed class NecromorphSeveredPartsSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ShaderPrototype> MaskShader = "NecromorphSeveredMask";

    private static readonly NecromorphSeveredPart[] Parts =
    {
        NecromorphSeveredPart.LegRight,
        NecromorphSeveredPart.LegLeft,
        NecromorphSeveredPart.Head,
    };

    /// <summary>
    /// Where each part is drawn in the 32x32 frame of each direction, as pixel columns and rows (from the top),
    /// inclusive: x0, row0, x1, row1, in the order of <see cref="Parts"/>. Humanoid ones are the body parts' own
    /// bounds (Mobs/Species/Human/parts.rsi) a little wider, for clothes.
    /// </summary>
    private static readonly Dictionary<NecromorphSpriteLayout, Dictionary<Direction, (int X0, int Row0, int X1, int Row1)[]>> Rects = new()
    {
        [NecromorphSpriteLayout.Humanoid] = new()
        {
            [Direction.South] = new[] { (8, 23, 15, 31), (16, 23, 23, 31), (9, 0, 22, 10) },
            [Direction.North] = new[] { (16, 23, 23, 31), (8, 23, 15, 31), (9, 0, 22, 10) },
            [Direction.East] = new[] { (12, 23, 19, 31), (13, 23, 20, 31), (10, 0, 23, 11) },
            [Direction.West] = new[] { (11, 23, 19, 31), (12, 23, 20, 31), (9, 0, 22, 11) },
        },
        [NecromorphSpriteLayout.MadMiner] = new()
        {
            [Direction.South] = new[] { (9, 25, 15, 31), (16, 25, 22, 31), (10, 0, 20, 8) },
            [Direction.North] = new[] { (16, 25, 22, 31), (9, 25, 15, 31), (9, 0, 22, 8) },
            [Direction.East] = new[] { (15, 25, 20, 31), (12, 25, 15, 31), (11, 0, 21, 9) },
            [Direction.West] = new[] { (11, 25, 16, 31), (16, 25, 19, 31), (10, 0, 20, 9) },
        },
    };

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NecromorphSeveredPartsComponent, ComponentShutdown>(OnShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eyeRotation = _eye.CurrentEye.Rotation;
        var query = EntityQueryEnumerator<NecromorphSeveredPartsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var severed, out var sprite))
        {
            if (severed.Severed == NecromorphSeveredPart.None)
                continue;

            if (!_shaders.TryGetValue(uid, out var shader))
            {
                shader = _prototype.Index(MaskShader).InstanceUnique();
                _shaders[uid] = shader;
            }

            var direction = (_transform.GetWorldRotation(uid) + eyeRotation).GetCardinalDir();
            var layout = Rects.GetValueOrDefault(severed.Layout) ?? Rects[NecromorphSpriteLayout.Humanoid];
            var rects = layout.GetValueOrDefault(direction) ?? layout[Direction.South];
            for (var i = 0; i < Parts.Length; i++)
            {
                shader.SetParameter($"rect{i}", ToQuad(rects[i]));
                shader.SetParameter($"hidden{i}", (severed.Severed & Parts[i]) != 0 ? 1f : 0f);
            }

            // Every drawn layer, clothes put on later too. Displacement maps only feed another layer's shader.
            var index = 0;
            foreach (var layer in sprite.AllLayers)
            {
                if (layer is SpriteComponent.Layer { CopyToShaderParameters: null } drawn && drawn.Shader != shader)
                    sprite.LayerSetShader(index, shader);

                index++;
            }
        }
    }

    /// <summary>Pixel rect (rows from the top) to the layer quad's 0..1 space from its bottom-left corner.</summary>
    private static Vector4 ToQuad((int X0, int Row0, int X1, int Row1) rect)
    {
        const float size = 32f;
        return new Vector4(rect.X0 / size, 1f - (rect.Row1 + 1) / size, (rect.X1 + 1) / size, 1f - rect.Row0 / size);
    }

    private void OnShutdown(Entity<NecromorphSeveredPartsComponent> ent, ref ComponentShutdown args)
    {
        if (!_shaders.Remove(ent.Owner, out var shader) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer is SpriteComponent.Layer drawn && drawn.Shader == shader)
                sprite.LayerSetShader(index, (ShaderInstance?)null);

            index++;
        }
    }
}
