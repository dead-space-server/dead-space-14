using Content.Shared.DeviceLinking.Events;
using Content.Server.DeadSpace.Research.Components;
using Content.Server.Research.TechnologyDisk.Components;
using Content.Shared.Research; // DiskConsolePrintDiskMessage
using Robust.Shared.GameObjects;
using Content.Server.Power.EntitySystems;

namespace Content.Server.DeadSpace.Research.Systems;

public sealed partial class TechDiskPrinterOnSignalSystem : EntitySystem
{
    [Dependency] private PowerReceiverSystem _powerReceiver = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TechDiskPrinterOnSignalComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    private void OnSignalReceived(
        EntityUid uid,
        TechDiskPrinterOnSignalComponent component,
        ref SignalReceivedEvent args)
    {
        if (args.Port != component.PrintPort)
            return;

        if (!_powerReceiver.IsPowered(uid))
            return;

        // Создаем сообщение без актора
        var message = new DiskConsolePrintDiskMessage();
        RaiseLocalEvent(uid, message);
    }
}