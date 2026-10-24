// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using Content.Shared.DeadSpace.Microscope;
using Content.Shared.DeadSpace.OrganDamage;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.Microscope.UI;

[UsedImplicitly]
public sealed class MicroscopeBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MicroscopeWindow? _window;

    [ViewVariables]
    private WoundInfoWindow? _woundWindow;

    public MicroscopeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = new MicroscopeWindow
        {
            Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName,
        };

        _window.EjectOrganPressed += OnEjectOrganPressed;
        _window.WoundClicked += OpenWoundWindow;
        _window.OnClose += CloseWoundWindow;
        _window.OpenCentered();
    }

    private void OnEjectOrganPressed()
    {
        SendMessage(new MicroscopeActionMessage(MicroscopeUiAction.EjectOrgan));
    }

    private void OpenWoundWindow(OrganWound wound)
    {
        CloseWoundWindow();

        _woundWindow = new WoundInfoWindow(wound);
        _woundWindow.OpenCentered();
    }

    private void CloseWoundWindow()
    {
        if (_woundWindow == null)
            return;

        _woundWindow.Close();
        _woundWindow.Dispose();
        _woundWindow = null;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is MicroscopeBoundUserInterfaceState microscopeState)
            _window?.UpdateState(microscopeState);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_window != null)
        {
            _window.EjectOrganPressed -= OnEjectOrganPressed;
            _window.WoundClicked -= OpenWoundWindow;
            _window.OnClose -= CloseWoundWindow;
        }

        CloseWoundWindow();
        _window?.Dispose();
        _window = null;
    }
}
