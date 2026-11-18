using Content.Client.DeadSpace.RoundEnd.Credits; // DS14
using Content.Client.GameTicking.Managers;
using Content.Shared.DeadSpace.Arena;
using Content.Shared.GameTicking;
using Content.Shared.Input;
using JetBrains.Annotations;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Client.RoundEnd;

[UsedImplicitly]
public sealed class RoundEndSummaryUIController : UIController,
    IOnSystemLoaded<ClientGameTicker>
{
    [Dependency] private readonly IInputManager _input = default!;

    private RoundEndSummaryWindow? _window;
    private ArenaManifestEvent? _arenaManifest; // DS14

    private void ToggleScoreboardWindow(ICommonSession? session = null)
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
        {
            _window.Close();
        }
        else
        {
            OpenScoreboardWindow(); // DS14
        }
    }

    // DS14-start
    public void OpenScoreboardWindow()
    {
        if (_window == null || _window.IsOpen)
            return;

        _window.OpenCenteredRight();
        UIManager.GetUIController<RoundEndCreditsUIController>().TryHostWindow(_window);
        _window.ResumeManifestDollSnapshots();
        _window.MoveToFront();
    }
    // DS14-end

    public void OpenRoundEndSummaryWindow(RoundEndMessageEvent message)
    {
        CreateRoundEndSummaryWindow(message, true); // DS14
    }

    // DS14-start
    public void PrepareRoundEndSummaryWindow(RoundEndMessageEvent message)
    {
        CreateRoundEndSummaryWindow(message, false);
    }

    private void CreateRoundEndSummaryWindow(RoundEndMessageEvent message, bool open)
    // DS14-end
    {
        // Don't open duplicate windows (mainly for replays).
        if (_window?.RoundId == message.RoundId)
            return;

        _window?.Close(); // DS14

        _window = new RoundEndSummaryWindow(message.GamemodeTitle, message.RoundEndText,
            message.RoundDuration, message.RoundId, message.AllPlayersEndInfo, EntityManager, open); // DS14

        if (_arenaManifest != null) // DS14
            _window.SetArenaManifest(_arenaManifest); // DS14
    }

    public void OnSystemLoaded(ClientGameTicker system)
    {
        _input.SetInputCommand(ContentKeyFunctions.ToggleRoundEndSummaryWindow,
            InputCmdHandler.FromDelegate(ToggleScoreboardWindow));
    }

    // DS14-Start
    public void OnArenaManifest(ArenaManifestEvent ev)
    {
        _arenaManifest = ev;
        _window?.SetArenaManifest(ev);
    }
    // DS14-End
}
