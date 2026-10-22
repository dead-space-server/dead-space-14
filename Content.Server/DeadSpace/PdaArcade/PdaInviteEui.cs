// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.EUI;
using Content.Shared.DeadSpace.PdaArcade;
using Content.Shared.Eui;

namespace Content.Server.DeadSpace.PdaArcade;

public sealed class PdaInviteEui : BaseEui
{
    private readonly PdaArcadeSystem _arcade;
    private readonly int _invite;
    private readonly string _text;
    private bool _answered;

    public PdaInviteEui(PdaArcadeSystem arcade, int invite, string text)
    {
        _arcade = arcade;
        _invite = invite;
        _text = text;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new PdaInviteState { Text = _text };
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (_answered || msg is not PdaInviteChoiceMessage choice)
            return;

        _answered = true;
        _arcade.AnswerInvite(_invite, choice.Accept);
        Close();
    }
}
