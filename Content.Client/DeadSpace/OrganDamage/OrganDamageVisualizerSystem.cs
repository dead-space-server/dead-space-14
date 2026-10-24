// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.OrganDamage;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.DeadSpace.OrganDamage;

public sealed class OrganDamageVisualizerSystem : VisualizerSystem<OrganDamageVisualComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, OrganDamageVisualComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        Color color;

        if (AppearanceSystem.TryGetData<bool>(uid, OrganDamageVisuals.Dead, out var dead, args.Component) && dead)
        {
            color = Color.FromHex("#6f6f6f");
        }
        else if (AppearanceSystem.TryGetData<byte>(uid, OrganDamageVisuals.DamagePercent, out var percent, args.Component))
        {
            var fraction = Math.Clamp(percent / 100f, 0f, 1f);
            color = Color.InterpolateBetween(Color.White, Color.FromHex("#7c1f1f"), fraction);
        }
        else
        {
            return;
        }

        args.Sprite.Color = color;
    }
}
