// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.BSAConsole;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Map;

namespace Content.Client.DeadSpace.BSAConsole;

[UsedImplicitly]
public sealed class BSAConsoleBoundUserInterface : BoundUserInterface
{
    private BSAConsoleWindow? _window;

    public BSAConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BSAConsoleWindow>();
        _window.OnFirePressed += OnFire;
        _window.OnSwitchViewPressed += OnSwitchView;
        _window.OnSelectGridPressed += OnSelectGrid;
        _window.OnEjectDiskPressed += OnEjectDisk;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is BSAConsoleUiState bsaState)
            _window?.UpdateState(bsaState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    private void OnFire(MapCoordinates mapCoordinates)
    {
        SendMessage(new BSAConsoleFireMessage((float) mapCoordinates.X, (float) mapCoordinates.Y));
    }

    private void OnSwitchView(BSAConsoleViewMode viewMode)
    {
        SendMessage(new BSAConsoleSwitchViewMessage(viewMode));
    }

    private void OnSelectGrid(NetEntity gridUid)
    {
        SendMessage(new BSAConsoleSelectGridMessage(gridUid));
    }

    private void OnEjectDisk()
    {
        SendMessage(new BSAConsoleEjectDiskMessage());
    }
}
