// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.PdaArcade;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.PdaArcade;

[UsedImplicitly]
public sealed class PdaInviteEui : BaseEui
{
    private readonly PdaInviteWindow _window = new();
    private bool _sent;

    public PdaInviteEui()
    {
        _window.AcceptButton.OnPressed += _ => Answer(true);
        _window.DenyButton.OnPressed += _ => Answer(false);
        _window.OnClose += () => Answer(false);
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _sent = true;
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is PdaInviteState invite)
            _window.Prompt.Text = invite.Text;
    }

    private void Answer(bool accept)
    {
        if (_sent)
            return;

        _sent = true;
        SendMessage(new PdaInviteChoiceMessage { Accept = accept });
    }
}
