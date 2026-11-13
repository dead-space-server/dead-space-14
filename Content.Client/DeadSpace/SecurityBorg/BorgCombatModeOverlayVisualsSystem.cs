using Content.Shared.DeadSpace.Borgs;
using Content.Shared.DeadSpace.SecurityBorg;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.SecurityBorg;

public sealed partial class BorgCombatModeOverlayVisualsSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgCombatModeOverlayComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(Entity<BorgCombatModeOverlayComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var combat = _appearance.TryGetData<bool>(ent.Owner, BorgCombatModeVisuals.Combat, out var isCombat) && isCombat;
        var prone = _appearance.TryGetData<bool>(ent.Owner, SecurityBorgProneVisuals.Prone, out var isProne) && isProne;

        string lightStatusState;
        if (combat && prone)
            lightStatusState = "sec_down_l_fight";
        else if (combat)
            lightStatusState = "sec_l_fight";
        else if (prone)
            lightStatusState = "sec_down_l";
        else
            lightStatusState = "sec_l";

        if (!_sprite.LayerMapTryGet((ent.Owner, args.Sprite), BorgVisualLayers.LightStatus, out var index, false))
            return;

        _sprite.LayerSetRsiState((ent.Owner, args.Sprite), index, lightStatusState);
    }
}
