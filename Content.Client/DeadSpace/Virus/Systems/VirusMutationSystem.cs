// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Virus.Components;
using Content.Shared.DeadSpace.Virus;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Virus.Systems;

public sealed partial class VirusMutationSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VirusMutationComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }
    private void OnAppearanceChange(EntityUid uid, VirusMutationComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (_appearance.TryGetData<bool>(uid, VirusMutationVisuals.infected, out var infected, args.Component))
        {
            if (infected)
                _sprite.LayerSetRsiState(uid, 0, component.InfectedState);
            else
                _sprite.LayerSetRsiState(uid, 0, component.State);
        }
    }
}
