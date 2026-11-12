using System.Linq;
using Content.Server.Administration.Commands;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Shared.Administration.Notes;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Server.Audio;
using Content.Server.Administration.Managers;
using Robust.Shared.Audio;

namespace Content.Server.Administration.Notes;

/// <inheritdoc/>
public sealed partial class ServerAdminNotesSystem : AdminNotesSystem
{
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private IAdminNotesManager _notes = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private EuiManager _euis = default!;
    [Dependency] private AudioSystem _audioSystem = default!;
    [Dependency] private IAdminManager _adminManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    protected override void OpenNotes(EntityUid user, EntityUid target)
    {
        if (!TryComp<ActorComponent>(user, out var userActor) ||
            !TryComp<ActorComponent>(target, out var targetActor))
            return;

        var userSession = userActor.PlayerSession;
        _console.RemoteExecuteCommand(userSession,
            $"{OpenAdminNotesCommand.CommandName} \"{targetActor.PlayerSession.UserId}\"");
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus != SessionStatus.InGame)
            return;

        var messages = await _notes.GetNewMessages(e.Session.UserId);
        var watchlists = await _notes.GetActiveWatchlists(e.Session.UserId);

        if (!_playerManager.TryGetPlayerData(e.Session.UserId, out var playerData))
        {
            Log.Error($"Could not get player data for ID {e.Session.UserId}");
        }

        var username = playerData?.UserName ?? e.Session.UserId.ToString();
        foreach (var watchlist in watchlists)
        {
            _chat.SendAdminAlert(Loc.GetString("admin-notes-watchlist", ("player", username), ("message", watchlist.Message)));
            _audioSystem.PlayGlobal(new SoundPathSpecifier("/Audio/_DeadSpace/Misc/watchlist.ogg"), Filter.Empty().AddPlayers(_adminManager.ActiveAdmins), false, AudioParams.Default.WithVolume(-2f));
        }

        var messagesToShow = messages.OrderBy(x => x.CreatedAt).Where(x => !x.Dismissed).ToArray();
        if (messagesToShow.Length == 0)
            return;

        var ui = new AdminMessageEui(messagesToShow);
        _euis.OpenEui(ui, e.Session);
    }
}
