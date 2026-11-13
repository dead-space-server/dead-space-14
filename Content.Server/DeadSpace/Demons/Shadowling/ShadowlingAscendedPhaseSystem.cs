// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Polymorph.Systems;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingAscendedPhaseSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    private const string ActionPhaseId = "ActionShadowlingAscendedPhase";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingAscendedPhaseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShadowlingAscendedPhaseComponent, ShadowlingAscendedPhaseActionEvent>(OnShadowlingPhase);
    }

    private void OnMapInit(EntityUid uid, ShadowlingAscendedPhaseComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ActionPhaseId);
    }

    private void OnShadowlingPhase(EntityUid uid, ShadowlingAscendedPhaseComponent component, ShadowlingAscendedPhaseActionEvent args)
    {
        if (args.Handled) return;

        if (TryComp<HandsComponent>(uid, out var hands))
        {
            foreach (var handName in hands.SortedHands)
            {
                _hands.TryDrop(uid, handName, null, false);
            }
        }

        var result = _polymorph.PolymorphEntity(uid, "ShadowlingAscendedPhasePolymorph");

        if (result != null)
        {
            _eye.SetDrawFov(result.Value, false);
            _eye.SetDrawLight(result.Value, false);
        }

        args.Handled = true;
    }
}