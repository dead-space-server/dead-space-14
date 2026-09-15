using Content.Server.Ghost;
using Content.Shared.Actions.Components;
using Content.Shared.Changeling.Components;

namespace Content.Server.Changeling.Systems;

/// <summary>
/// Blocks leaving the body while changeling regenerative stasis is active.
/// </summary>
public sealed class RegenerativeStasisGhostSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // DS14: the pre-v288 action system has no GhostAttemptEvent relay.
        SubscribeLocalEvent<GhostAttemptHandleEvent>(OnGhostAttempt);
    }

    private void OnGhostAttempt(GhostAttemptHandleEvent args)
    {
        if (args.Mind.CurrentEntity is not { } body ||
            !TryComp<ActionsComponent>(body, out var actions))
        {
            return;
        }

        foreach (var action in actions.Actions)
        {
            if (!TryComp<RegenerativeStasisActionComponent>(action, out var stasis) ||
                stasis.AllowGhosting ||
                !stasis.IsInStasis)
            {
                continue;
            }

            args.Handled = true;
            args.Result = false;
            return;
        }
    }
}
