// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Abilities.StunRadius;
using Content.Shared.DeadSpace.Abilities.StunRadius.Components;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Abilities.StunRadius;

public sealed class StunRadiusSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunRadiusComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, StunRadiusComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, StunRadiusVisuals.StunRadius, out var stun, args.Component))
        {
            if (stun)
                args.Sprite.LayerSetState(0, component.StunState);
            else
                args.Sprite.LayerSetState(0, component.State);
        }
    }
}
