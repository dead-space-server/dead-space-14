// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Eui;
using Content.Shared.DeadSpace.UI;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.DeadSpace.Salvage;

/// <summary>«Merge two fultons linked to different beacons into one?»</summary>
[UsedImplicitly]
public sealed class FultonMergeEui : BaseEui
{
    private readonly YesNoWindow _window = new(string.Empty, string.Empty);
    private bool _answered;

    public FultonMergeEui()
    {
        _window.YesButton.OnPressed += _ => Reply(YesNoUiButton.Yes);
        _window.NoButton.OnPressed += _ => Reply(YesNoUiButton.No);
        _window.OnClose += () => Reply(YesNoUiButton.No);
    }

    private void Reply(YesNoUiButton choice)
    {
        if (_answered)
            return;

        _answered = true;
        SendMessage(new YesNoChoiceMessage(choice));
        _window.Close();
    }

    public override void Opened() => _window.OpenCentered();

    public override void HandleState(EuiStateBase state)
    {
        if (state is not YesNoEuiState prompt)
            return;

        _window.Title = prompt.Title;
        _window.MessageLabel.SetMessage(prompt.Text);
    }

    public override void Closed()
    {
        _answered = true;
        _window.Close();
    }
}
