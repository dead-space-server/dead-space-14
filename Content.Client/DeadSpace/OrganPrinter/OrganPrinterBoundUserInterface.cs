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

    private OrganPrinterUpdateState? _lastState;

    public OrganPrinterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<OrganPrinterWindow>();
        _window.OnPrintPressed += (product, group, quality, waste) =>
            SendMessage(new OrganPrinterPrintRequestMessage(product, group, quality, waste));
        _window.OnGameFailed += (product, group, fastMode) =>
            SendMessage(new OrganPrinterGameFailedMessage(product, group, fastMode));

        if (_lastState is { } pending)
            _window.UpdateState(pending);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not OrganPrinterUpdateState msg)
            return;

        _lastState = msg;
        _window?.UpdateState(msg);
    }
}
