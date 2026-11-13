using Content.Shared.CombatMode;
using Content.Shared.DeadSpace.Borgs;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.DeadSpace.Borgs;

public sealed partial class BorgCombatModeOverlaySystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedPointLightSystem _light = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BorgCombatModeOverlayComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<BorgCombatModeOverlayComponent, CombatModeChangedEvent>(OnCombatModeChanged);
    }

    private void OnInit(Entity<BorgCombatModeOverlayComponent> ent, ref ComponentInit args)
    {
        if (TryComp<CombatModeComponent>(ent, out var combat))
            SetCombatVisuals(ent, combat.IsInCombatMode);
    }

    private void OnCombatModeChanged(Entity<BorgCombatModeOverlayComponent> ent, ref CombatModeChangedEvent args)
    {
        SetCombatVisuals(ent, args.IsInCombatMode);
    }

    private void SetCombatVisuals(EntityUid uid, bool isInCombat)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, BorgCombatModeVisuals.Combat, isInCombat, appearance);

        if (TryComp<PointLightComponent>(uid, out var pointLight))
            _light.SetColor(uid, isInCombat ? Color.Red : Color.White, pointLight);
    }
}
