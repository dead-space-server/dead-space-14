// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Lavaland;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Draws a geist sunk in its hole: everything of it below the ground line is cut away by a shader on each of its
/// layers (clothes included), so only its upper half shows - rising out of it or sinking back in.
/// </summary>
public sealed class LavalandGeistHoleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;

    private static readonly ProtoId<ShaderPrototype> CutShader = "LavalandGeistHoleCut";

    /// <summary>Waist-deep: how far the sprite is lowered while it stands in the hole.</summary>
    private const float Waist = 0.3f;

    /// <summary>All the way down: the whole sprite is under the ground line.</summary>
    private const float Gone = 0.9f;

    /// <summary>
    /// The ground line, relative to the entity's origin (tiles). The sprite is one tile tall and centred, so at
    /// <see cref="Waist"/> depth it spans -0.8..0.2 and this line goes across its waist.
    /// </summary>
    private const float Ground = -0.35f;

    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandGeistHoleComponent, ComponentShutdown>(OnShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<LavalandGeistHoleComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var hole, out var sprite))
        {
            var progress = hole.Duration <= 0f
                ? 1f
                : Math.Clamp((float) (now - hole.PhaseStart).TotalSeconds / hole.Duration, 0f, 1f);

            var (depth, alpha) = hole.Phase switch
            {
                LavalandGeistHolePhase.Rising => (Gone + (Waist - Gone) * Ease(progress), 1f),
                LavalandGeistHolePhase.Sinking => (Waist + (Gone - Waist) * progress * progress, 1f),
                LavalandGeistHolePhase.Hidden => (Gone, 0f),
                _ => (Waist, 1f),
            };

            var offset = new Vector2(0f, -depth);
            var rotation = Angle.Zero;

            // Winding up, its traps in hand: shaking where it stands.
            if (hole.Phase == LavalandGeistHolePhase.Holding)
                offset.X += MathF.Sin((float) now.TotalSeconds * 55f) * 0.035f;

            // Shot down: jerked back away from the shooter, tipping over as it drops - never up out of the hole.
            if (hole.Phase == LavalandGeistHolePhase.Sinking && hole.Knock.LengthSquared() > 0.0001f)
            {
                var knock = Ease(Math.Min(1f, progress * 2.5f));
                offset += new Vector2(hole.Knock.X, Math.Min(0f, hole.Knock.Y)) * knock;
                var tilt = hole.Knock.X >= 0f ? -1f : 1f;
                rotation = Angle.FromDegrees(tilt * 25f * knock);
            }

            _sprite.SetOffset((uid, sprite), offset);
            _sprite.SetRotation((uid, sprite), rotation);
            _sprite.SetColor((uid, sprite), sprite.Color.WithAlpha(alpha));

            // What part of the one-tile sprite (its bottom at offset.Y - 0.5) is under the ground line.
            ApplyCut(uid, sprite, Math.Clamp(Ground - (offset.Y - 0.5f), 0f, 1f));
            _spriteTree.QueueTreeUpdate((uid, sprite));
        }
    }

    /// <summary>Puts the cut shader on every drawn layer (clothes put on later too) and sets the cut line.</summary>
    private void ApplyCut(EntityUid uid, SpriteComponent sprite, float cut)
    {
        if (!_shaders.TryGetValue(uid, out var shader))
        {
            shader = _prototype.Index(CutShader).InstanceUnique();
            _shaders[uid] = shader;
        }

        shader.SetParameter("cut", cut);

        var index = 0;
        foreach (var layer in sprite.AllLayers)
        {
            // Displacement maps only feed another layer's shader and are never drawn themselves.
            if (layer is SpriteComponent.Layer { CopyToShaderParameters: null } drawn && drawn.Shader != shader)
                sprite.LayerSetShader(index, shader);

            index++;
        }
    }

    private static float Ease(float t) => 1f - (1f - t) * (1f - t);

    private void OnShutdown(Entity<LavalandGeistHoleComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
        {
            _shaders.Remove(ent.Owner);
            return;
        }

        if (_shaders.Remove(ent.Owner, out var shader))
        {
            var index = 0;
            foreach (var layer in sprite.AllLayers)
            {
                if (layer is SpriteComponent.Layer drawn && drawn.Shader == shader)
                    sprite.LayerSetShader(index, (ShaderInstance?)null);

                index++;
            }
        }

        _sprite.SetOffset((ent.Owner, sprite), Vector2.Zero);
        _sprite.SetRotation((ent.Owner, sprite), Angle.Zero);
        _sprite.SetColor((ent.Owner, sprite), sprite.Color.WithAlpha(1f));
    }
}
