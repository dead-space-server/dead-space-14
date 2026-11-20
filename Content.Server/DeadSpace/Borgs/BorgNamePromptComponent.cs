// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Network;

namespace Content.Server.DeadSpace.Borgs;

/// <summary>
/// Отслеживает показ диалога выбора имени киборга.
/// </summary>
[RegisterComponent]
public sealed partial class BorgNamePromptComponent : Component
{
    /// <summary>
    /// Список игроков (UserId), которым уже предлагалось ввести имя для этого киборга.
    /// Предотвращает повторный показ диалога при переподключениях и дублирующих событиях.
    /// </summary>
    [DataField]
    public HashSet<NetUserId> PromptedUsers = new();
}
