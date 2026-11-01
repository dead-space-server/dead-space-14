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
    private readonly Random _random = new();

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

        _entityManager.System<ServerGlobalSoundSystem>().AdminSoundQueueChanged += OnSoundQueueChanged;
        RefreshEntries();
        StateDirty();
    }

    public override void Closed()
    {
        _entityManager.System<ServerGlobalSoundSystem>().AdminSoundQueueChanged -= OnSoundQueueChanged;
    }

    private void OnSoundQueueChanged()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var soundSystem = _entityManager.System<ServerGlobalSoundSystem>();
        var queue = soundSystem.AdminQueue
            .Select(entry => new AdminGlobalSoundQueueEntry(entry.Path))
            .ToList();

        return new AdminGlobalSoundEuiState(
            _currentPath,
            _entries,
            soundSystem.ActiveAdminSoundPath,
            soundSystem.ActiveAdminSoundPaused,
            _error,
            queue);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (IsShutDown)
            return;

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
            case AdminGlobalSoundEuiMessage.QueueAdd add:
                QueueAdd(add);
                break;
            case AdminGlobalSoundEuiMessage.QueueRemove remove:
                if (_entityManager.System<ServerGlobalSoundSystem>().TryRemoveAdminQueueEntry(remove.Index))
                    _error = null;
                break;
            case AdminGlobalSoundEuiMessage.QueueClear:
                _entityManager.System<ServerGlobalSoundSystem>().ClearAdminQueue();
                _error = null;
                break;
            case AdminGlobalSoundEuiMessage.QueueSkip:
                _entityManager.System<ServerGlobalSoundSystem>().ControlAdminGlobalSound(AdminGlobalSoundControl.Stop);
                _error = null;
                break;
            case AdminGlobalSoundEuiMessage.QueueStop:
            {
                var soundSystem = _entityManager.System<ServerGlobalSoundSystem>();
                soundSystem.ClearAdminQueue();
                soundSystem.ControlAdminGlobalSound(AdminGlobalSoundControl.Stop);
                _error = null;
                break;
            }
            case AdminGlobalSoundEuiMessage.QueueFolder folder:
                QueueFolder(folder);
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

    private void QueueAdd(AdminGlobalSoundEuiMessage.QueueAdd request)
    {
        var soundSystem = _entityManager.System<ServerGlobalSoundSystem>();

        if (!soundSystem.TryEnqueueAdminSound(request.Path, request.Volume, request.CKey))
        {
            _error = Loc.GetString("admin-global-sound-invalid-file");
            return;
        }

        if (!soundSystem.HasActiveAdminSound)
            soundSystem.PlayNextQueuedSound();

        _error = null;
    }

    private void QueueFolder(AdminGlobalSoundEuiMessage.QueueFolder request)
    {
        if (!request.Path.StartsWith(AudioRoot, StringComparison.OrdinalIgnoreCase))
        {
            _error = Loc.GetString("admin-global-sound-invalid-directory");
            return;
        }

        var files = GetAudioFilesInDirectory(request.Path);
        if (files.Count == 0)
        {
            _error = Loc.GetString("admin-global-sound-empty-directory");
            return;
        }

        if (request.Shuffle)
            ShuffleList(files);

        var soundSystem = _entityManager.System<ServerGlobalSoundSystem>();
        foreach (var file in files)
        {
            soundSystem.TryEnqueueAdminSound(file, request.Volume, request.CKey);
        }

        if (!soundSystem.HasActiveAdminSound)
            soundSystem.PlayNextQueuedSound();

        _error = null;
    }

    private List<string> GetAudioFilesInDirectory(string path)
    {
        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directory = new ResPath(path);

        foreach (var entry in _resourceManager.ContentGetDirectoryEntries(directory))
        {
            if (entry.EndsWith('/') || !entry.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                continue;

            results.Add((directory / entry).ToString());
        }

        foreach (var audio in _prototypeManager.EnumeratePrototypes<AudioMetadataPrototype>())
        {
            var audioPath = new ResPath(audio.ID);
            if (!audioPath.TryRelativeTo(directory, out var relative) || relative is not { } relativePath)
                continue;

            var segments = relativePath.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 1 && segments[0].EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                results.Add(audioPath.ToString());
        }

        return results.ToList();
    }

    private void ShuffleList(List<string> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
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
