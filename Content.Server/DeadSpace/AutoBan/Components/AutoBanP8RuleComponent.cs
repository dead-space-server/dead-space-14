// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Server.DeadSpace.AutoBan.Components;

[RegisterComponent, Access(typeof(AutoBanP8RuleSystem))]
public sealed partial class AutoBanP8RuleComponent : Component
{
    [ViewVariables(VVAccess.ReadOnly)]
    public Dictionary<string, int> RuleViolations = new();

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int MaxWarnings = 2;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string Reason = "П8. Множество раз писал сообщения эротического характера в чат. Просим вас откликнулься на нашем дискорд сервере, канал: 'Обжалования'.";

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int Minutes = 1440; // 24 часа.
}
