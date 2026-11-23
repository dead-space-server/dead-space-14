// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Players;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.CustomNameOnSpawn;

public sealed partial class CustomNameOnSpawnSystem : EntitySystem
{
    [Dependency] private AntagSelectionSystem _antagSelection = default!;
    [Dependency] private MetaDataSystem _metaSystem = default!;
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IConfigurationManager _cfgManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Для обычных игроков (раундстарт, лейтджоин)
        SubscribeLocalEvent<CustomNameOnSpawnComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawned);

        // Для антагов
        SubscribeLocalEvent<AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    // Обычный спавн

    private void OnPlayerSpawned(EntityUid ent, CustomNameOnSpawnComponent component, PlayerSpawnCompleteEvent args)
    {
        ShowNameChangeMenu(ent, component, args.Player);
    }

    // Антаг спавн

    private void OnAntagSelected(ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session == null)
            return;

        var ent = args.EntityUid;

        if (!TryComp<CustomNameOnSpawnComponent>(ent, out var comp))
            return;

        ShowNameChangeMenu(ent, comp, args.Session, args.GameRule.Owner);
    }

    // Общая логика диалога

    private void ShowNameChangeMenu(EntityUid ent, CustomNameOnSpawnComponent component, ICommonSession player, EntityUid? antagRule = null)
    {
        var maxNameLength = _cfgManager.GetCVar(CCVars.MaxNameLength);

        var dialogTitle = component.NamePrefix != null
            ? Loc.GetString("custom-name-on-start-dialog-title-prefix", ("prefix", component.NamePrefix))
            : Loc.GetString("custom-name-on-start-dialog-title");

        _quickDialog.OpenDialog(player,
            dialogTitle,
            Loc.GetString("custom-name-on-start-dialog-newname-text"),
            (string newName) =>
            {
                newName = newName.Trim();

                if (newName.Length <= 2)
                {
                    _chatManager.DispatchServerMessage(
                        player,
                        Loc.GetString("custom-name-on-start-too-short"),
                        true);
                    ShowNameChangeMenu(ent, component, player, antagRule);
                    return;
                }

                var finalName = component.NamePrefix != null
                    ? $"{component.NamePrefix} {newName}"
                    : newName;

                if (finalName.Length > maxNameLength)
                {
                    _chatManager.DispatchServerMessage(
                        player,
                        Loc.GetString("custom-name-on-start-too-long"),
                        true);
                    ShowNameChangeMenu(ent, component, player, antagRule);
                    return;
                }

                _metaSystem.SetEntityName(ent, finalName);

                if (antagRule != null && player.GetMind() is { } mind)
                    _antagSelection.UpdateAntagIdentifierName(antagRule.Value, mind, finalName);
            });
    }
}
