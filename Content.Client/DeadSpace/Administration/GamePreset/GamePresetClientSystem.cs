// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Client.Administration.Managers;
using Content.Client.Administration.UI.GamePreset;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.Administration.GamePreset;
using Robust.Client.Console;
using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.DeadSpace.Administration.GamePreset;

public sealed class GamePresetClientSystem : EntitySystem
{
    [Dependency] private readonly IClientConsoleHost _consoleHost = default!;
    [Dependency] private readonly IClientAdminManager _adminManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    public event Action<GamePresetsResponseMessage>? PresetsUpdated;

    public List<string> AvailableModes { get; private set; } = new();
    public Dictionary<string, string> ModeNames { get; private set; } = new();
    public Dictionary<string, string> PresetNames { get; private set; } = new();

    private static bool _commandRegistered;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GamePresetsResponseMessage>(OnPresetsResponse);

        if (!_commandRegistered)
        {
            _consoleHost.RegisterCommand(
                "gamepresetui",
                "Открыть окно управления пресетами режимов",
                "gamepresetui",
                ExecuteOpenGamePresetWindow,
                false);
            _commandRegistered = true;
        }
    }

    private void ExecuteOpenGamePresetWindow(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_adminManager.HasFlag(AdminFlags.Server))
            return;

        var window = new GamePresetWindow();
        _uiManager.WindowRoot.AddChild(window);
        window.OpenCentered();
    }

    private void OnPresetsResponse(GamePresetsResponseMessage msg)
    {
        AvailableModes = msg.ModeNames.Keys.ToList();
        ModeNames = msg.ModeNames;
        PresetNames = msg.PresetNames;
        PresetsUpdated?.Invoke(msg);
    }

    public void RequestPresets()
    {
        RaiseNetworkEvent(new RequestGamePresetsMessage());
    }

    public void SetSystemEnabled(bool enabled)
    {
        RaiseNetworkEvent(new SetSystemEnabledMessage(enabled));
    }

    public void CreateCustomPreset(string name, List<string> modes, string presetType, bool secret)
    {
        RaiseNetworkEvent(new CreateCustomPresetMessage(name, modes, presetType, secret));
    }

    public void UpdateCustomPreset(string id, string name, List<string> modes, string presetType, bool secret)
    {
        RaiseNetworkEvent(new UpdateCustomPresetMessage(id, name, modes, presetType, secret));
    }

    public void DeleteCustomPreset(string id)
    {
        RaiseNetworkEvent(new DeleteCustomPresetMessage(id));
    }

    public void SetActivePresets(List<string> presetIds)
    {
        RaiseNetworkEvent(new SetActivePresetsMessage(presetIds));
    }

    public void AddPresetToQueue(string presetId)
    {
        RaiseNetworkEvent(new AddPresetToQueueMessage(presetId));
    }

    public void RemovePresetFromQueue(string presetId)
    {
        RaiseNetworkEvent(new RemovePresetFromQueueMessage(presetId));
    }

    public void MovePresetInQueue(int fromIndex, int toIndex)
    {
        RaiseNetworkEvent(new MovePresetInQueueMessage(fromIndex, toIndex));
    }

    public void UpdatePresetSettings(
        int maxRdmRow,
        int voteDurationSeconds,
        bool disableOoc,
        bool preventRepeat,
        bool checkPlayerLimit,
        List<string> whitelistModeIds,
        bool enableLowPlayerPreset,
        int lowPlayerThreshold,
        string? lowPlayerPresetId)
    {
        RaiseNetworkEvent(new UpdatePresetSettingsMessage(
            maxRdmRow,
            voteDurationSeconds,
            disableOoc,
            preventRepeat,
            checkPlayerLimit,
            whitelistModeIds,
            enableLowPlayerPreset,
            lowPlayerThreshold,
            lowPlayerPresetId));
    }

    public void InitiateVoteNow()
    {
        RaiseNetworkEvent(new InitiateVoteNowMessage());
    }

    public void SkipCurrentPreset()
    {
        RaiseNetworkEvent(new SkipCurrentPresetMessage());
    }
}