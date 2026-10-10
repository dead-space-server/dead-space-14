using Content.Shared.Actions;
using Content.Server.Actions;
using Content.Shared.Sectants;
using Robust.Shared.Prototypes;

namespace Content.Server.Sectants;

public sealed class SectantActionsGrantSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantActionsGrantComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, SectantActionsGrantComponent comp, MapInitEvent args)
    {
        foreach (var actionId in comp.Actions)
            _actions.AddAction(uid, actionId);
    }
}
