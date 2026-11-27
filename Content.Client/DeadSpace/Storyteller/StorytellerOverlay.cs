// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Storyteller;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Storyteller;

public sealed class StorytellerOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Unshaded = "unshaded";

    private readonly IEntityManager _entities;
    private readonly IPrototypeManager _prototypes;
    private readonly IGameTiming _timing;
    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _transform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public StorytellerOverlay(IEntityManager entities, IPrototypeManager prototypes, IGameTiming timing)
    {
        _entities = entities;
        _prototypes = prototypes;
        _timing = timing;
        _sprites = entities.System<SpriteSystem>();
        _transform = entities.System<SharedTransformSystem>();
        _shader = prototypes.Index(Unshaded).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var settings = StoryStage.Settings(_prototypes);
        if (settings.TellerIcon is not { } icon)
            return;

        var handle = args.WorldHandle;
        var rotation = Matrix3Helpers.CreateRotation(-(args.Viewport.Eye?.Rotation ?? default));
        var time = (float) _timing.RealTime.TotalSeconds;
        var texture = _sprites.Frame0(icon);
        var size = (Vector2) texture.Size / EyeManager.PixelsPerMeter * settings.TellerIconScale;

        handle.UseShader(_shader);
        var query = _entities.AllEntityQueryEnumerator<StorytellerComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var teller, out var sprite, out var xform))
        {
            if (!teller.Telling || !sprite.Visible || xform.MapID != args.MapId)
                continue;

            var position = _transform.GetWorldPosition(xform);
            var bounds = _sprites.GetLocalBounds((uid, sprite));
            if (!bounds.Translated(position).Enlarged(1f).Intersects(args.WorldAABB))
                continue;

            var hop = MathF.Abs(MathF.Sin((time + uid.Id * 0.37f) * settings.TellerIconSpeed)) * settings.TellerIconHeight;
            var squash = 1f - (1f - hop / MathF.Max(0.01f, settings.TellerIconHeight)) * 0.12f;
            var bottom = bounds.Height / 2f + settings.TellerIconOffset + hop;
            var drawn = new Vector2(size.X * (2f - squash), size.Y * squash);

            handle.SetTransform(rotation * Matrix3Helpers.CreateTranslation(position));
            handle.DrawTextureRect(texture, Box2.FromDimensions(new Vector2(-drawn.X / 2f, bottom), drawn));
        }

        handle.UseShader(null);
        handle.SetTransform(Matrix3x2.Identity);
    }
}
