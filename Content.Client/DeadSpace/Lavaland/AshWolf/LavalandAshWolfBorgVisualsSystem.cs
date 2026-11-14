// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Silicons.Borgs;
using Content.Shared.DeadSpace.Lavaland.AshWolf;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Lavaland.AshWolf;

/// <summary>
/// The borg system sets the light layer (the eyes) to one state on every appearance change; this puts the rolled-up
/// eyes there while the Ash Wolf borg is rolled up for an attack.
/// </summary>
public sealed class LavalandAshWolfBorgVisualsSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandAshWolfBorgComponent, AppearanceChangeEvent>(OnAppearanceChanged, after: [typeof(BorgSystem)]);
    }

    private void OnAppearanceChanged(Entity<LavalandAshWolfBorgComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite ||
            !_sprite.LayerMapTryGet((ent.Owner, sprite), BorgVisualLayers.Light, out var layer, false))
        {
            return;
        }

        _appearance.TryGetData<LavalandAshWolfVisualState>(ent.Owner, LavalandAshWolfVisuals.State, out var state, args.Component);
        _sprite.LayerSetRsiState((ent.Owner, sprite), layer,
            state == LavalandAshWolfVisualState.Roll ? ent.Comp.RollEyesState : ent.Comp.EyesState);
    }
}
