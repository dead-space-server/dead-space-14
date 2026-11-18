// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Wizard.Runes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Wizard.Runes;

public sealed class RuneScreenFlashOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float FadeIn = 0.15f;
    private const float FadeOut = 0.35f;
    private const float ScreenShare = 1.4f;

    private readonly SpriteSystem _sprite;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public RuneScreenFlashOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entity.System<SpriteSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _player.LocalEntity is { } local &&
               _entity.TryGetComponent(local, out RuneScreenFlashComponent? flash) &&
               flash.Sprite != null &&
               _timing.CurTime < flash.End;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entity.TryGetComponent(local, out RuneScreenFlashComponent? flash) ||
            flash.Sprite is not { } sprite)
            return;

        var now = _timing.CurTime;
        var shown = (float) (now - flash.Start).TotalSeconds;
        var left = (float) (flash.End - now).TotalSeconds;
        var alpha = Math.Clamp(MathF.Min(shown / FadeIn, left / FadeOut), 0f, 1f);
        if (alpha <= 0f)
            return;

        var texture = _sprite.Frame0(sprite);
        UIBox2 bounds = args.ViewportBounds;
        var side = MathF.Max(bounds.Width, bounds.Height) * ScreenShare;
        var aspect = texture.Width / (float) Math.Max(1, texture.Height);
        var size = new Vector2(side * aspect, side);
        var wobble = MathF.Sin(shown * 9f) * 0.04f * side;
        var center = bounds.Center + new Vector2(wobble, 0f);
        args.ScreenHandle.DrawTextureRect(texture, UIBox2.FromDimensions(center - size / 2f, size), Color.White.WithAlpha(alpha));
    }
}
