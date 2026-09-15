// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.PipeShuttle;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.PipeShuttle.UI;

[UsedImplicitly]
public sealed partial class PipeShuttleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private PipeShuttleWindow? _window;

    protected override void Open()
    {
        base.Open();

        if (_window != null)
            return;

        _window = new PipeShuttleWindow();
        _window.OnDestSelected += OnDestSelected;
        _window.OnClose += OnClose;
        _window.OpenCentered();
    }

    public override void Update()
    {
        base.Update();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PipeShuttleUiState shuttleState)
            _window?.UpdateState(shuttleState);
    }

    private void OnClose()
    {
        if (_window != null)
        {
            _window.OnDestSelected -= OnDestSelected;
            _window.OnClose -= OnClose;
            _window = null;
        }
        Close();
    }

    private void OnDestSelected(string destId)
    {
        SendPredictedMessage(new PipeShuttleCallMessage { DestId = destId });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_window != null)
        {
            _window.OnDestSelected -= OnDestSelected;
            _window.OnClose -= OnClose;
            _window.Dispose();
            _window = null;
        }
    }
}
