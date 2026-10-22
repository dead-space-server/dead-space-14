// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Shared.DeadSpace.Body.Limbs;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics.RSI;

namespace Content.Client.DeadSpace.Body.Limbs;

public sealed class LimbHealthView : Control
{
    public IReadOnlyList<LimbZoneState>? Zones;

    private RSI? _sheet;

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
                    : LimbWoundRules.Tone(state.Health, LimbWoundRules.HealthFor(icon.Zone));
            }

            var name = LimbDollLayout.HealthState(icon, tone);
            if (name.Length == 0 || !Sheet().TryGetState(name, out var rsi))
                continue;

            handle.DrawTextureRect(rsi.GetFrame(RsiDirection.South, 0), box, Color.White);
        }
    }

    private RSI Sheet()
    {
        return _sheet ??= IoCManager.Resolve<IResourceCache>().GetResource<RSIResource>(LimbDollLayout.Texture).RSI;
    }
}
