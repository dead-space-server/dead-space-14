// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos.Components;
using Content.Shared.Input;
using Content.Shared.DeadSpace.Lavaland;
using Content.Shared.Interaction;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.DeadSpace.Lavaland;

/// <summary>
/// Alt + left click on a pipe asks the server to start sliding along it; while sliding, Alt + left click anywhere hops off.
/// </summary>
public sealed class LavalandPipeSlideInputSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            // Alt + left click is its own key function (AltActivateItemInWorld), not Use with Alt held.
            .BindBefore(ContentKeyFunctions.AltActivateItemInWorld,
                new PointerInputCmdHandler(OnUse, false, true),
                typeof(SharedInteractionSystem))
            .Register<LavalandPipeSlideInputSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<LavalandPipeSlideInputSystem>();
        base.Shutdown();
    }

    private bool OnUse(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State != BoundKeyState.Down ||
            args.Session?.AttachedEntity is not { } user)
        {
            return false;
        }

        if (HasComp<LavalandPipeSliderComponent>(user))
        {
            RaiseNetworkEvent(new LavalandPipeSlideRequestEvent(null));
            return true;
        }

        if (!args.EntityUid.IsValid() || !HasComp<PipeAppearanceComponent>(args.EntityUid))
            return false;

        RaiseNetworkEvent(new LavalandPipeSlideRequestEvent(GetNetEntity(args.EntityUid)));
        return true;
    }
}
