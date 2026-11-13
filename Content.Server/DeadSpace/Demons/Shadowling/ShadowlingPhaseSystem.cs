// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Polymorph.Systems;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingPhaseSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private const string ActionPhaseId = "ActionShadowlingPhase";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingPhaseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShadowlingPhaseComponent, ShadowlingPhaseActionEvent>(OnShadowlingPhase);
    }

    private void OnMapInit(EntityUid uid, ShadowlingPhaseComponent component, MapInitEvent args)
    {
        if (HasComp<ShadowlingAnnihilationComponent>(uid))
            return;
        _actions.AddAction(uid, ActionPhaseId);
    }

    private void OnShadowlingPhase(EntityUid uid, ShadowlingPhaseComponent component, ShadowlingPhaseActionEvent args)
    {
        if (HasComp<ShadowlingAnnihilationComponent>(uid))
            return;
        if (args.Handled) return;

        if (TryComp<HandsComponent>(uid, out var hands))
        {
            foreach (var handName in hands.SortedHands)
            {
                _hands.TryDrop(uid, handName, null, false);
            }
        }

        var result = _polymorph.PolymorphEntity(uid, "ShadowlingPhasePolymorph");

        if (result != null)
        {
            _eye.SetDrawFov(result.Value, false);
            _eye.SetDrawLight(result.Value, false);
        }

        args.Handled = true;
    }
}