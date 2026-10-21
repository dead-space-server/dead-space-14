// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.Lavaland.ChromiteCaves;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.Lavaland.ChromiteCaves;

[UsedImplicitly]
public sealed class LavalandCavePitEui : BaseEui
{
    private readonly YesNoWindow _window;
    private bool _sent;

    public LavalandCavePitEui()
    {
        _window = new YesNoWindow(
            Loc.GetString("lavaland-caves-pit-title"),
            Loc.GetString("lavaland-caves-pit-message"));

        _window.YesButton.Text = Loc.GetString("lavaland-caves-pit-yes");
        _window.NoButton.Text = Loc.GetString("lavaland-caves-pit-no");
        _window.YesButton.OnPressed += _ => SendResponse(true);
        _window.NoButton.OnPressed += _ => SendResponse(false);
        _window.OnClose += () => SendResponse(false);
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
    }

    public override void Closed()
    {
        _sent = true;
        _window.Close();
    }

    private void SendResponse(bool accepted)
    {
        if (_sent)
            return;

        _sent = true;
        SendMessage(new LavalandCavePitResponseMessage(accepted));
        _window.Close();
    }
}
