// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Actions;
using Content.Shared.DeadSpace.Demons.Shadowling;

namespace Content.Server.DeadSpace.Demons.Shadowling;

public sealed partial class ShadowlingAscendedPhaseReturnSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    private const string ActionReturnId = "ActionShadowlingAscendedPhaseReturn";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadowlingAscendedPhaseReturnComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShadowlingAscendedPhaseReturnComponent, ShadowlingAscendedPhaseReturnEvent>(OnReturn);
    }

    private void OnMapInit(EntityUid uid, ShadowlingAscendedPhaseReturnComponent component, MapInitEvent args)
    {
        _actions.AddAction(uid, ActionReturnId);
    }

    private void OnReturn(EntityUid uid, ShadowlingAscendedPhaseReturnComponent component, ShadowlingAscendedPhaseReturnEvent args)
    {
        if (args.Handled) return;

        if (TryComp<PolymorphedEntityComponent>(uid, out var polymorphComp))
            _polymorph.Revert((uid, polymorphComp));

        args.Handled = true;
    }
}