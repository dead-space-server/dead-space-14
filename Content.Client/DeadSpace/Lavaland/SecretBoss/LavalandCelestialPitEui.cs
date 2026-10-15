// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.Lavaland.SecretBoss;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Lavaland.SecretBoss;

[UsedImplicitly]
public sealed class LavalandCelestialPitEui : BaseEui
{
    private readonly YesNoWindow _window;
    private bool _sent;

    public LavalandCelestialPitEui()
    {
        _window = new YesNoWindow(
            Loc.GetString("lavaland-secret-boss-jump-title"),
            Loc.GetString("lavaland-secret-boss-jump-message"));

        _window.YesButton.Text = Loc.GetString("lavaland-secret-boss-jump-yes");
        _window.NoButton.Text = Loc.GetString("lavaland-secret-boss-jump-no");
        _window.YesButton.OnPressed += _ => SendResponse(true);
        _window.NoButton.OnPressed += _ => SendResponse(false);
        _window.OnClose += () => SendResponse(false);
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
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
        SendMessage(new LavalandCelestialPitResponseMessage(accepted));
        _window.Close();
    }
}
