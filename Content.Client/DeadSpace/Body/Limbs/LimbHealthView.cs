// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbHealthView : Control
{
    public IReadOnlyList<LimbZoneState>? Zones;
    public LimbBodyComponent? Pools;
    public float? Ether;

    private const string HatTexture = "/Textures/_DeadSpace/Wizard/rune_hud.rsi";
    private static readonly Color EtherCalm = Color.FromHex("#8d7bff");
    private static readonly Color EtherHot = Color.FromHex("#ffa24a");
    private static readonly Color EtherDanger = Color.FromHex("#ff3b4a");

    private RSI? _sheet;
    private RSI? _hat;
    private float _time;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _time += args.DeltaSeconds;
    }

    public LimbHealthView()
    {
        MouseFilter = MouseFilterMode.Ignore;
        MinSize = new Vector2(LimbDollLayout.Width, LimbDollLayout.Height) * LimbDollView.AlertScale;
        MaxSize = MinSize;
        HorizontalExpand = false;
        VerticalExpand = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        foreach (var icon in LimbDollLayout.Icons)
        {
            var tone = 0;
            var index = LimbWoundRules.Index(icon.Zone);
            if (Zones != null && (uint) index < (uint) Zones.Count)
            {
                var state = Zones[index];
                tone = state.Severed || state.Atrophied || state.Health <= 0f
                    ? -1
                    : LimbWoundRules.Tone(state.Health, LimbWoundRules.HealthFor(icon.Zone, Pools));
            }

            var name = LimbDollLayout.HealthState(icon, tone);
            if (name.Length == 0 || !Sheet().TryGetState(name, out var rsi))
                continue;

            handle.DrawTextureRect(rsi.GetFrame(RsiDirection.South, 0), box, Color.White);
        }

        if (Ether is { } ether)
            DrawWizard(handle, box, Math.Clamp(ether, 0f, 1f));
    }

    private void DrawWizard(DrawingHandleScreen handle, UIBox2 box, float ether)
    {
        var color = ether < 0.5f
            ? EtherCalm
            : ether < 0.75f
                ? Color.InterpolateBetween(EtherCalm, EtherHot, (ether - 0.5f) / 0.25f)
                : Color.InterpolateBetween(EtherHot, EtherDanger, (ether - 0.75f) / 0.25f);
        var flicker = ether > 0.75f ? 0.6f + 0.4f * MathF.Sin(_time * 12f) : 1f;

        var pixel = box.Width / LimbDollLayout.Width;
        var bar = UIBox2.FromDimensions(new Vector2(box.Right - pixel * 4f, box.Top + pixel * 6f), new Vector2(pixel * 2f, box.Height - pixel * 9f));
        var edge = pixel * 0.5f;
        handle.DrawRect(new UIBox2(bar.Left - edge, bar.Top - edge, bar.Right + edge, bar.Bottom + edge), Color.Black.WithAlpha(0.75f));
        var fill = new UIBox2(bar.Left, bar.Bottom - bar.Height * ether, bar.Right, bar.Bottom);
        handle.DrawRect(fill, color.WithAlpha(flicker));

        _hat ??= IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(HatTexture).RSI;
        if (!_hat.TryGetState("hat", out var hat))
            return;

        var frames = hat.GetFrames(RsiDirection.South);
        var delays = hat.GetDelays();
        var total = 0f;
        foreach (var delay in delays)
            total += delay;

        var at = total > 0f ? _time % total : 0f;
        var index = 0;
        while (index < delays.Length - 1 && at > delays[index])
        {
            at -= delays[index];
            index++;
        }

        handle.DrawTextureRect(frames[Math.Min(index, frames.Length - 1)], box, Color.InterpolateBetween(Color.White, color, ether * 0.6f));
    }

    private RSI Sheet()
    {
        return _sheet ??= IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(LimbDollLayout.Texture).RSI;
    }
}
