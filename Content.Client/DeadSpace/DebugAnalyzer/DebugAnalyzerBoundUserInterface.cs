// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.DebugAnalyzer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.DeadSpace.DebugAnalyzer;

[UsedImplicitly]
public sealed class DebugAnalyzerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private DebugAnalyzerWindow? _window;

    public DebugAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DebugAnalyzerWindow>();
        _window.Title = Loc.GetString("debug-analyzer-window-title");
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is DebugAnalyzerScannedMessage cast)
            _window?.Populate(cast);
    }
}
