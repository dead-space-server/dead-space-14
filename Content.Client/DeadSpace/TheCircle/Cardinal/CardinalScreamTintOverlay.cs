// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.TheCircle.Cardinal;

/// <summary>
/// The view of a player the cardinal's scream reached: recoloured in the scream's colour, flaring at once and fading
/// back over a couple of seconds.
/// </summary>
public sealed class CardinalScreamTintOverlay : Overlay
{
    private const float Hold = 1.0f;
    private const float Fade = 1.5f;
    private const float Peak = 0.9f;
    private static readonly ProtoId<ShaderPrototype> ShaderId = "CardinalScreamTint";

    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;
    private TimeSpan _started = TimeSpan.MinValue;
    private Color _color;
    private float _strength;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public CardinalScreamTintOverlay(IGameTiming timing, IPrototypeManager prototypes)
    {
        _timing = timing;
        _shader = prototypes.Index(ShaderId).InstanceUnique();
        ZIndex = 6;
    }

    public void Start(Color color)
    {
        _color = color;
        _started = _timing.RealTime;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_started == TimeSpan.MinValue)
            return false;

        var elapsed = (float) (_timing.RealTime - _started).TotalSeconds;
        if (elapsed >= Hold + Fade)
            return false;

        var rise = Math.Clamp(elapsed / 0.08f, 0f, 1f);
        var fade = elapsed <= Hold ? 1f : 1f - (elapsed - Hold) / Fade;
        _strength = Peak * rise * fade * fade;
        return _strength > 0.005f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("tint", _color);
        _shader.SetParameter("strength", _strength);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
