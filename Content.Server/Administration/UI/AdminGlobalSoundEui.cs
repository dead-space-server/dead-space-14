// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT
using System.Linq;
using Content.Server.Audio;
using Content.Server.EUI;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.Administration.Events;
using Content.Shared.Eui;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Administration.UI;

public sealed class AdminGlobalSoundEui : BaseEui
{
    private const string AudioRoot = "/Audio/";

    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private string _currentPath = AudioRoot;
    private List<AdminGlobalSoundEntry> _entries = [];
    private string? _error;

    public AdminGlobalSoundEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void Opened()
    {
        if (!HasAccess())
        {
            Close();
            return;
        }

        RefreshEntries();
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var soundSystem = _entityManager.System<ServerGlobalSoundSystem>();
        return new AdminGlobalSoundEuiState(
            _currentPath,
            _entries,
            soundSystem.ActiveAdminSoundPath,
            soundSystem.ActiveAdminSoundPaused,
            _error);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!HasAccess())
        {
            Close();
            return;
        }

        switch (msg)
        {
            case AdminGlobalSoundEuiMessage.Browse browse:
                Browse(browse.Path);
                break;
            case AdminGlobalSoundEuiMessage.Play play:
                Play(play);
                break;
            case AdminGlobalSoundEuiMessage.Control control:
                _entityManager.System<ServerGlobalSoundSystem>().ControlAdminGlobalSound(control.Action);
                _error = null;
                break;
        }

        StateDirty();
    }

    private bool HasAccess()
    {
        return Player != null && _adminManager.HasAdminFlag(Player, AdminFlags.Fun);
    }

    private void Browse(string path)
    {
        if (!_entries.Any(entry => entry.IsDirectory && entry.Path == path))
        {
            _error = Loc.GetString("admin-global-sound-invalid-directory");
            return;
        }

        _currentPath = path;
        _error = null;
        RefreshEntries();
    }

    private void Play(AdminGlobalSoundEuiMessage.Play request)
    {
        if (!_entries.Any(entry => !entry.IsDirectory && entry.Path == request.Path))
        {
            _error = Loc.GetString("admin-global-sound-invalid-file");
            return;
        }

        var volume = Math.Clamp(request.Volume, -24, 12);
        var parameters = AudioParams.Default.WithVolume(volume - 8);
        ICommonSession[]? recipients = null;
        Filter filter;

        if (string.IsNullOrWhiteSpace(request.CKey))
        {
            filter = Filter.Empty().AddAllPlayers(_playerManager);
        }
        else if (_playerManager.TryGetSessionByUsername(request.CKey.Trim(), out var target))
        {
            recipients = [target];
            filter = Filter.Empty().AddPlayer(target);
        }
        else
        {
            _error = Loc.GetString("admin-global-sound-player-not-found", ("ckey", request.CKey));
            return;
        }

        var sound = _entityManager.System<SharedAudioSystem>().ResolveSound(new SoundPathSpecifier(request.Path));
        var soundSystem = _entityManager.System<ServerGlobalSoundSystem>();
        soundSystem.ControlAdminGlobalSound(AdminGlobalSoundControl.FadeOut);
        soundSystem.PlayAdminGlobal(
            filter,
            sound,
            parameters,
            replay: true,
            recipients,
            request.Path);
        _error = null;
    }

    private void RefreshEntries()
    {
        var entries = new Dictionary<string, AdminGlobalSoundEntry>(StringComparer.OrdinalIgnoreCase);
        var directory = new ResPath(_currentPath);

        foreach (var entry in _resourceManager.ContentGetDirectoryEntries(directory))
        {
            var isDirectory = entry.EndsWith('/');
            var name = entry.TrimEnd('/');
            if (!isDirectory && !name.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                continue;

            var fullPath = (directory / entry).ToString();
            entries[fullPath] = new AdminGlobalSoundEntry(name, fullPath, isDirectory);
        }

        foreach (var audio in _prototypeManager.EnumeratePrototypes<AudioMetadataPrototype>())
        {
            var audioPath = new ResPath(audio.ID);
            if (!audioPath.TryRelativeTo(directory, out var relative) || relative is not { } relativePath)
                continue;

            var segments = relativePath.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
                continue;

            if (segments.Length == 1)
            {
                if (segments[0].EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                    entries[audioPath.ToString()] = new AdminGlobalSoundEntry(segments[0], audioPath.ToString(), false);
                continue;
            }

            var subdirectory = (directory / (segments[0] + "/")).ToString();
            entries[subdirectory] = new AdminGlobalSoundEntry(segments[0], subdirectory, true);
        }

        if (_currentPath != AudioRoot)
        {
            var parent = GetParentPath(_currentPath);
            entries[".."] = new AdminGlobalSoundEntry("..", parent, true);
        }

        _entries = entries.Values
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetParentPath(string path)
    {
        var trimmed = path.TrimEnd('/');
        var separator = trimmed.LastIndexOf('/');
        return separator < AudioRoot.Length - 1
            ? AudioRoot
            : trimmed[..(separator + 1)];
    }
}
