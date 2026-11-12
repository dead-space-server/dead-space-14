using Content.Shared.Medical.HealthAnalyzer;
using Content.Shared.MedicalScanner;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Shared.DeadSpace.Medical; //DS14

namespace Content.Client.HealthAnalyzer.UI;

[UsedImplicitly]
public sealed partial class HealthAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private HealthAnalyzerWindow? _window;

<<<<<<< ours
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;

            // DS14-start
            _window.OnPrintButtonPressed += () =>
            {
                SendMessage(new HealthAnalyzerPrintUiMessage());
            };
            // DS14-end
        }
||||||| base
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        }
=======
    protected override void Open()
    {
        base.Open();
>>>>>>> theirs

        _window = this.CreateWindow<HealthAnalyzerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
    }

    /// <summary>
    /// This will update the UI to reflect the newest health changes of the scanned entity.
    /// This gets called in the <see cref="HealthAnalyzerSystem"/> by SetUIState().
    /// </summary>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not HealthAnalyzerUiState cast)
            return;

        _window.Populate(cast);
    }
}