// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.OrganPrinter;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.OrganPrinter;

[UsedImplicitly]
public sealed class OrganPrinterBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private OrganPrinterWindow? _window;

    public OrganPrinterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredRight<OrganPrinterWindow>();
        _window.OnPrintPressed += (product, quality, waste) =>
            SendMessage(new OrganPrinterPrintRequestMessage(product, quality, waste));
        _window.OnGameFailed += fastMode =>
            SendMessage(new OrganPrinterGameFailedMessage(fastMode));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is OrganPrinterUpdateState msg)
            _window?.UpdateState(msg);
    }
}
