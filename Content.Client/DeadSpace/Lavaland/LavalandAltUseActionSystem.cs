// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.DeadSpace.Lavaland.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Alt + right click with an item that has <see cref="LavalandAltUseActionComponent"/> performs that item's action
/// at the cursor, as if the action button had been used.
/// </summary>
public sealed class LavalandAltUseActionSystem : EntitySystem
{
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.UseSecondary,
                new PointerInputCmdHandler(OnSecondary, false, true),
                typeof(SharedInteractionSystem))
            .Register<LavalandAltUseActionSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<LavalandAltUseActionSystem>();
        base.Shutdown();
    }

    private bool OnSecondary(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State != BoundKeyState.Down ||
            !_input.IsKeyDown(Keyboard.Key.Alt) ||
            args.Session?.AttachedEntity is not { } user)
        {
            return false;
        }

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (!TryComp<LavalandAltUseActionComponent>(held, out var altUse))
                continue;

            if (altUse.Action == null)
            {
                NetEntity? target = args.EntityUid.IsValid() ? GetNetEntity(args.EntityUid) : null;
                RaiseNetworkEvent(new LavalandAltUseRequestEvent(GetNetEntity(held), GetNetCoordinates(args.Coordinates), target));
                return true;
            }

            foreach (var action in _actions.GetActions(user))
            {
                if (action.Comp.Container != held || MetaData(action).EntityPrototype?.ID != altUse.Action.Value.Id)
                    continue;

                RaisePredictiveEvent(new RequestPerformActionEvent(GetNetEntity(action),
                    GetNetCoordinates(args.Coordinates)));
                return true;
            }
        }

        return false;
    }
}
