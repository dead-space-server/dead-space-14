// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// The mad miner's sprite is one piece with its arms and legs drawn in: the limbs it has lost are cut out of it by a
/// shader, the rect of each limb depending on which way the sprite faces. A torn-off limb is the same sprite with
/// everything but that limb cut away.
/// </summary>
public sealed class LavalandMadMinerLimbsSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ShaderPrototype> MaskShader = "LavalandMadMinerLimbMask";

    /// <summary>The base (one-piece) layer of the miner's sprite.</summary>
    private const int BaseLayer = 0;

    private static readonly LavalandMadMinerLimb[] Limbs =
    {
        LavalandMadMinerLimb.ArmRight,
        LavalandMadMinerLimb.ArmLeft,
        LavalandMadMinerLimb.LegRight,
        LavalandMadMinerLimb.LegLeft,
    };

    /// <summary>
    /// Where each limb is drawn in the 32x32 frame of each direction, as pixel columns and rows (from the top),
    /// inclusive: x0, row0, x1, row1. In the order of <see cref="Limbs"/>.
    /// </summary>
    private static readonly Dictionary<Direction, (int X0, int Row0, int X1, int Row1)[]> Rects = new()
    {
        [Direction.South] = new[] { (6, 9, 10, 26), (20, 10, 28, 24), (9, 25, 15, 31), (16, 25, 22, 31) },
        [Direction.North] = new[] { (20, 14, 28, 26), (3, 10, 10, 24), (16, 25, 22, 31), (9, 25, 15, 31) },
        [Direction.East] = new[] { (17, 16, 23, 24), (10, 10, 12, 17), (15, 25, 20, 31), (12, 25, 15, 31) },
        [Direction.West] = new[] { (6, 19, 11, 26), (19, 10, 21, 17), (11, 25, 16, 31), (16, 25, 19, 31) },
    };

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMadMinerLimbsComponent, ComponentShutdown>(OnLimbsShutdown);
        SubscribeLocalEvent<LavalandMadMinerSeveredLimbComponent, ComponentShutdown>(OnSeveredShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var eyeRotation = _eye.CurrentEye.Rotation;
        var miners = EntityQueryEnumerator<LavalandMadMinerLimbsComponent, SpriteComponent>();
        while (miners.MoveNext(out var uid, out var limbs, out var sprite))
        {
            var shader = EnsureShader(uid, sprite);
            var direction = (_transform.GetWorldRotation(uid) + eyeRotation).GetCardinalDir();
            var rects = Rects.GetValueOrDefault(direction) ?? Rects[Direction.South];
            for (var i = 0; i < Limbs.Length; i++)
            {
                shader.SetParameter($"rect{i}", ToQuad(rects[i]));
                shader.SetParameter($"hidden{i}", (limbs.Severed & Limbs[i]) != 0 ? 1f : 0f);
            }

            shader.SetParameter("invert", 0f);
        }

        // A torn-off limb is always drawn as seen from the south, where its rect is.
        var severed = EntityQueryEnumerator<LavalandMadMinerSeveredLimbComponent, SpriteComponent>();
        while (severed.MoveNext(out var uid, out var limb, out var sprite))
        {
            var shader = EnsureShader(uid, sprite);
            sprite.EnableDirectionOverride = true;
            sprite.DirectionOverride = Direction.South;
            var index = Array.IndexOf(Limbs, limb.Limb);
            shader.SetParameter("rect0", ToQuad(Rects[Direction.South][Math.Max(0, index)]));
            shader.SetParameter("invert", 1f);
        }
    }

    private ShaderInstance EnsureShader(EntityUid uid, SpriteComponent sprite)
    {
        if (!_shaders.TryGetValue(uid, out var shader))
        {
            shader = _prototype.Index(MaskShader).InstanceUnique();
            _shaders[uid] = shader;
        }

        if (sprite.TryGetLayer(BaseLayer, out var layer) && layer.Shader != shader)
            sprite.LayerSetShader(BaseLayer, shader);

        return shader;
    }

    /// <summary>Pixel rect (rows from the top) to the layer quad's 0..1 space from its bottom-left corner.</summary>
    private static Vector4 ToQuad((int X0, int Row0, int X1, int Row1) rect)
    {
        const float size = 32f;
        return new Vector4(rect.X0 / size, 1f - (rect.Row1 + 1) / size, (rect.X1 + 1) / size, 1f - rect.Row0 / size);
    }

    private void OnLimbsShutdown(Entity<LavalandMadMinerLimbsComponent> ent, ref ComponentShutdown args)
    {
        RemoveShader(ent.Owner);
    }

    private void OnSeveredShutdown(Entity<LavalandMadMinerSeveredLimbComponent> ent, ref ComponentShutdown args)
    {
        RemoveShader(ent.Owner);
    }

    private void RemoveShader(EntityUid uid)
    {
        if (!_shaders.Remove(uid, out var shader) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (sprite.TryGetLayer(BaseLayer, out var layer) && layer.Shader == shader)
            sprite.LayerSetShader(BaseLayer, (ShaderInstance?)null);
    }
}
