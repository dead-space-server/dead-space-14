// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.CentCommFax;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.CentCommFax;

[UsedImplicitly]
public sealed class CentCommFaxEui : BaseEui
{
    private readonly CentCommFaxWindow _window;

    public CentCommFaxEui()
    {
        _window = new CentCommFaxWindow(IoCManager.Resolve<IPrototypeManager>(), IoCManager.Resolve<IComponentFactory>());
        _window.OnClose += () => SendMessage(new CloseEuiMessage());
        _window.OnReply += SendMessage;
        _window.OnTemplateRequested += (template, target) =>
            SendMessage(new CentCommFaxEuiMsg.RequestTemplate { Template = template, Target = target });
        _window.OnFollow += fax => SendMessage(new CentCommFaxEuiMsg.Follow { Target = fax });
    }

    public override void Opened()
    {
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is CentCommFaxEuiState cast)
            _window.UpdateState(cast);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is CentCommFaxEuiMsg.TemplateLoaded template)
            _window.ApplyTemplate(template);
    }
}
