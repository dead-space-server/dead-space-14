
using Content.Server.DeviceLinking.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Pinpointer;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Interaction.Events;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.DeadSpace.Items.Devices;

public sealed class EmergencySignallerSystem : EntitySystem
{
    [Dependency] private readonly NavMapSystem _navMap = default!;
    [Dependency] private readonly RadioSystem _radioSystem = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmergencySignallerComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(EntityUid uid, EmergencySignallerComponent component, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!_powerCell.TryGetBatteryFromSlot(uid, out var batteryUid, out var battery, null) && !TryComp(uid, out battery))
            return;

        if (batteryUid == null)
            return;

        if (!_battery.TryUseCharge(batteryUid.Value, component.Charge, battery))
            return;

        if (!TryComp(uid, out EncryptionKeyHolderComponent? keys))
            return;

        var ownerXform = Transform(uid);
        var pos = ownerXform.MapPosition;
        var x = (int)pos.X;
        var y = (int)pos.Y;
        var cordText = $" ({x}, {y})";

        var posText = FormattedMessage.RemoveMarkupOrThrow(_navMap.GetNearestBeaconString(uid));
        var message = Loc.GetString(component.Message, ("user", args.User), ("position", posText + cordText));

        RadioChannelPrototype radioChannelProto;
        foreach (string channelId in keys.Channels)
        {
            radioChannelProto = _prototypeManager.Index<RadioChannelPrototype>(channelId);
            _radioSystem.SendRadioMessage(uid, message, radioChannelProto, uid);
        }

        args.Handled = true;
    }
}
