using Content.Client.Alerts;
using Content.Client.UserInterface.Systems.Alerts.Controls;
using Content.Shared.Alert;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Ninja.Components;
using Content.Shared.Ninja.Systems;
using Robust.Client.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client.DeadSpace.Ninja.Systems;

public sealed class DashAbilitySystem : SharedDashAbilitySystem
{
    private const string CooldownAlert = "DashCooldown";

    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        CommandBinds.Builder
            .BindBefore(ContentKeyFunctions.AltActivateItemInWorld,
                new PointerInputCmdHandler(OnDashActivate, false, true),
                typeof(SharedInteractionSystem))
            .Register<DashAbilitySystem>();

        SubscribeLocalEvent<AlertsComponent, UpdateAlertSpriteEvent>(OnUpdateAlertSprite);
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<DashAbilitySystem>();
        base.Shutdown();
    }

    private bool OnDashActivate(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (args.State != BoundKeyState.Down ||
            args.Session?.AttachedEntity is not { } user ||
            !TryGetHeldItem(user, out var item) ||
            !TryComp<DashAbilityComponent>(item, out var comp) ||
            !comp.AltClickDash)
            return false;

        RaiseNetworkEvent(new DashRequestEvent(GetNetEntity(item), GetNetCoordinates(args.Coordinates)));
        return true;
    }

    private void OnUpdateAlertSprite(Entity<AlertsComponent> ent, ref UpdateAlertSpriteEvent args)
    {
        if (args.Alert.ID != CooldownAlert)
            return;

        if (!TryGetHeldItem(ent.Owner, out var item) ||
            !TryComp<SpriteComponent>(item, out var itemSprite))
            return;

        var rsi = _sprite.LayerGetEffectiveRsi((item, itemSprite), 0);
        var state = _sprite.LayerGetRsiState((item, itemSprite), 0);
        if (rsi == null || !state.IsValid)
            return;

        _sprite.LayerSetRsi((args.SpriteViewEnt.Owner, args.SpriteViewEnt.Comp), AlertVisualLayers.Base, rsi, state);
    }

    private bool TryGetHeldItem(EntityUid user, out EntityUid item)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<DashAbilityComponent>(held))
            {
                item = held;
                return true;
            }
        }

        item = default;
        return false;
    }
}
