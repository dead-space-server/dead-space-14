// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.Pointing;

public sealed partial class PointingBubbleOverlay : Overlay
{
    private static readonly SpriteSpecifier.Rsi BubbleSprite = new(new ResPath("/Textures/_DeadSpace/Interface/pointing_bubble.rsi"), "bubble");
    private static readonly Vector2 Tail = new(11.5f, 32f);
    private static readonly Vector2 IconSpot = new(32f, 13f);
    private const float IconFit = 22f;
    private const float BubbleWidth = 0.5f;
    private const float GrowTime = 0.14f;
    private const float SettleTime = 0.08f;
    private const float FadeTime = 0.25f;

    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly PointingBubbleSystem _system;
    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _xform;
    private readonly ExamineSystemShared _examine;
    private readonly Texture _bubble;
    private readonly IRenderTexture _iconTarget;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PointingBubbleOverlay(PointingBubbleSystem system)
    {
        IoCManager.InjectDependencies(this);
        _system = system;
        _sprites = _ent.System<SpriteSystem>();
        _xform = _ent.System<SharedTransformSystem>();
        _examine = _ent.System<ExamineSystemShared>();
        _bubble = _sprites.Frame0(BubbleSprite);
        _iconTarget = _clyde.CreateRenderTarget(
            (128, 128),
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb, true),
            new TextureSampleParameters
            {
                Filter = false
            },
            nameof(PointingBubbleOverlay));
        ZIndex = 2;
    }

    protected override void DisposeBehavior()
    {
        base.DisposeBehavior();
        _iconTarget.Dispose();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        _system.Cull();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _system.Cull();

        if (!_system.ShowBubbles
            || _system.Bubbles.Count == 0
            || args.ViewportControl == null
            || _player.LocalEntity is not { } local
            || !_ent.TryGetComponent(local, out EyeComponent? eyeComp)
            || args.Viewport.Eye != eyeComp.Eye)
            return;

        var screen = args.ScreenHandle;
        screen.SetTransform(Matrix3x2.Identity);

        var uiScale = _cfg.GetCVar(CVars.DisplayUIScale);
        if (uiScale == 0f)
            uiScale = _ui.DefaultUIScale;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var eyeRot = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var now = _timing.CurTime;
        var ppm = EyeManager.PixelsPerMeter;

        foreach (var bubble in _system.Bubbles)
        {
            var pointer = _ent.GetEntity(bubble.Pointer);
            var target = _ent.GetEntity(bubble.Target);
            if (!_ent.EntityExists(pointer) || !_ent.EntityExists(target))
                continue;

            if (!_ent.TryGetComponent(target, out SpriteComponent? sprite))
                continue;

            var iconBounds = IconBounds(sprite);
            if (iconBounds.Width < 0.01f && iconBounds.Height < 0.01f)
                continue;

            var map = _xform.GetMapCoordinates(pointer);
            if (map.MapId != args.MapId || !args.WorldBounds.Contains(map.Position))
                continue;

            if (args.Viewport.Eye is { DrawFov: true } eye
                && !_examine.InRangeUnOccluded(eye.Position, map, 0f, uid => uid == pointer || uid == local))
                continue;

            var age = MathF.Max(0f, (float)(now - bubble.Start).TotalSeconds);
            var left = (float)(bubble.End - now).TotalSeconds;
            var alpha = left < FadeTime ? Math.Clamp(left / FadeTime, 0f, 1f) : 1f;
            if (alpha <= 0f)
                continue;

            var pop = Pop(age);
            var pixel = new Vector2(uiScale * pop * BubbleWidth, uiScale * pop);
            var head = HeadHeight(pointer);
            var headWorld = map.Position + (-eyeRot).ToWorldVec() * -head;
            var headScreen = Vector2.Transform(headWorld, matrix);
            var topLeft = headScreen - Tail * pixel;
            var color = Color.White.WithAlpha(alpha);

            screen.DrawTextureRect(_bubble, UIBox2.FromDimensions(topLeft, (Vector2)_bubble.Size * pixel), color);

            var nativePx = MathF.Max(iconBounds.Width, iconBounds.Height) * ppm;
            if (nativePx < 1f)
                continue;

            var drawScale = uiScale * MathF.Min(1f, IconFit / nativePx);
            var rtCenter = (Vector2)_iconTarget.Size * 0.5f;
            var origin = rtCenter - new Vector2(iconBounds.Center.X, -iconBounds.Center.Y) * ppm * drawScale;
            var direction = sprite.EnableDirectionOverride
                ? sprite.DirectionOverride
                : _xform.GetWorldRotation(target).GetDir();

            screen.RenderInRenderTarget(_iconTarget, () =>
            {
                screen.DrawEntity(target, origin, new Vector2(drawScale), Angle.Zero, Angle.Zero, direction, sprite);
            }, Color.Transparent);

            var blit = (Vector2)_iconTarget.Size * pop;
            var iconCenter = topLeft + IconSpot * pixel;
            screen.DrawTextureRect(_iconTarget.Texture, UIBox2.FromDimensions(iconCenter - blit * 0.5f, blit), color);
            screen.SetTransform(Matrix3x2.Identity);
        }
    }

    private float HeadHeight(EntityUid pointer)
    {
        const float fallback = 0.35f;
        if (!_ent.TryGetComponent(pointer, out SpriteComponent? sprite))
            return fallback;

        var top = _sprites.GetLocalBounds((pointer, sprite)).Top + sprite.Offset.Y;
        return top > 0.2f ? top * 0.7f : fallback;
    }

    private Box2 IconBounds(SpriteComponent sprite)
    {
        var bounds = new Box2();
        var any = false;
        foreach (var layer in sprite.AllLayers)
        {
            if (!layer.Visible || layer is not SpriteComponent.Layer concrete || concrete.Blank)
                continue;

            var layerBounds = _sprites.GetLocalBounds(concrete);
            bounds = any ? bounds.Union(layerBounds) : layerBounds;
            any = true;
        }

        if (!any)
            return bounds;

        return bounds.Scale(sprite.Scale).Translated(sprite.Offset);
    }

    private static float Pop(float age)
    {
        if (age < GrowTime)
        {
            var t = age / GrowTime;
            var ease = 1f - MathF.Pow(1f - t, 3f);
            return MathHelper.Lerp(0.2f, 1.08f, ease);
        }

        if (age < GrowTime + SettleTime)
            return MathHelper.Lerp(1.08f, 1f, (age - GrowTime) / SettleTime);

        return 1f;
    }
}
