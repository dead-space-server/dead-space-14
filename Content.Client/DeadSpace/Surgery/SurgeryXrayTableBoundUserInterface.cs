// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Surgery;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.Surgery;

[UsedImplicitly]
public sealed class SurgeryXrayTableBoundUserInterface : BoundUserInterface
{
    private SurgeryXrayTableWindow? _window;

    public SurgeryXrayTableBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new SurgeryXrayTableWindow();
        _window.OnClose += Close;
        _window.PrintRequested += (zone, image) => SendMessage(new SurgeryXrayTablePrintMessage { Zone = zone, Image = image ?? Array.Empty<byte>() });
        _window.StartRequested += operation => SendMessage(new SurgeryXrayTableStartMessage { Operation = operation });
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not SurgeryXrayTableState cast)
            return;

        _window.ShowState(cast);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _window?.Dispose();
    }
}
