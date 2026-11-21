// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Silicons.AiBorgControl;
using Content.Shared.Administration.Logs;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.NameIdentifier;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server.DeadSpace.Borgs;

/// <summary>
/// Предлагает игроку ввести имя для своего андроида/киборга при подключении к серверу
/// (раундстарт, поздний вход, гост-роли, установка мозга в шасси).
/// </summary>
public sealed partial class BorgNamePromptSystem : EntitySystem
{
    [Dependency] private QuickDialogSystem _quickDialog = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private IConfigurationManager _cfgManager = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Спавн при старте раунда или позднем входе из лобби
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        // Прикрепление игрока к сущности (спавн, гост-роли, установка мозга/MMI в шасси)
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (TryComp<BorgChassisComponent>(args.Mob, out var chassis))
        {
            TryPromptName(args.Mob, chassis, args.Player);
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (TryComp<BorgChassisComponent>(args.Entity, out var chassis))
        {
            TryPromptName(args.Entity, chassis, args.Player);
        }
    }

    /// <summary>
    /// Проверяет условия и при необходимости показывает диалог ввода имени киборга.
    /// Гарантирует, что диалог покажется не более одного раза для каждого игрока на данном шасси.
    /// </summary>
    public bool TryPromptName(EntityUid uid, BorgChassisComponent chassis, ICommonSession session)
    {
        if (TerminatingOrDeleted(uid))
            return false;

        if (session.Status != SessionStatus.InGame)
            return false;

        // Исключаем удаленное управление ИИ
        if (HasComp<AiControlledBorgComponent>(uid))
            return false;

        var promptComp = EnsureComp<BorgNamePromptComponent>(uid);
        if (!promptComp.PromptedUsers.Add(session.UserId))
            return false;

        ShowNameDialog(uid, chassis, session);
        return true;
    }

    /// <summary>
    /// Открывает окно QuickDialog для ввода имени киборга.
    /// </summary>
    public void ShowNameDialog(EntityUid uid, BorgChassisComponent chassis, ICommonSession session)
    {
        if (TerminatingOrDeleted(uid) || session.Status != SessionStatus.InGame)
            return;

        var title = Loc.GetString("borg-name-prompt-dialog-title");
        var prompt = TryComp<NameIdentifierComponent>(uid, out var nameIdentifier) && !string.IsNullOrWhiteSpace(nameIdentifier.FullIdentifier)
            ? Loc.GetString("borg-name-prompt-dialog-prompt-with-id", ("id", nameIdentifier.FullIdentifier))
            : Loc.GetString("borg-name-prompt-dialog-prompt");

        _quickDialog.OpenDialog<string>(
            session,
            title,
            prompt,
            newName => HandleNameEntered(uid, chassis, session, newName),
            () =>
            {
                // Игрок отменил ввод - имя киборга остается прежним
            });
    }

    private void HandleNameEntered(EntityUid uid, BorgChassisComponent chassis, ICommonSession session, string rawName)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var maxNameLength = _cfgManager.GetCVar(CCVars.MaxNameLength);
        var identifier = TryComp<NameIdentifierComponent>(uid, out var nameIdentifier)
            ? nameIdentifier.FullIdentifier
            : null;

        var cleanName = CleanAndValidateName(rawName, identifier, maxNameLength, out var errorKey);
        if (cleanName == null)
        {
            if (errorKey != null)
            {
                _chatManager.DispatchServerMessage(session, Loc.GetString(errorKey), true);
            }
            ShowNameDialog(uid, chassis, session);
            return;
        }

        var meta = MetaData(uid);
        _metaData.SetEntityName(uid, cleanName, meta);

        _adminLog.Add(
            LogType.Action,
            LogImpact.High,
            $"Player {session.Name} ({session.UserId}) set borg \"{ToPrettyString(uid)}\" name to: {cleanName}");

        _chatManager.DispatchServerMessage(
            session,
            Loc.GetString("borg-name-prompt-success", ("name", cleanName)));
    }

    /// <summary>
    /// Валидирует и очищает имя киборга, предотвращая дублирование идентификатора.
    /// Чистая логика для удобного юнит-тестирования.
    /// </summary>
    public static string? CleanAndValidateName(
        string rawName,
        string? fullIdentifier,
        int maxNameLength,
        out string? errorKey)
    {
        errorKey = null;

        if (string.IsNullOrWhiteSpace(rawName))
        {
            errorKey = "borg-name-prompt-too-short";
            return null;
        }

        var name = rawName.Trim();

        // Если у киборга есть числовой/буквенный идентификатор (например, "Си-1234"),
        // и игрок случайно ввел его в конце имени (например, "Валли (Си-1234)" или "Валли Си-1234"),
        // аккуратно срезаем дубликат, чтобы движок не отобразил его дважды.
        if (!string.IsNullOrWhiteSpace(fullIdentifier))
        {
            var id = fullIdentifier.Trim();
            var bracketed = $"({id})";

            if (name.EndsWith(bracketed, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^bracketed.Length].Trim();
            }
            else if (name.EndsWith(id, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^id.Length].Trim();
            }
        }

        if (name.Length < 2)
        {
            errorKey = "borg-name-prompt-too-short";
            return null;
        }

        if (name.Length > maxNameLength)
        {
            errorKey = "borg-name-prompt-too-long";
            return null;
        }

        return name;
    }
}
