// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Psychiatry;

public enum PsychiatryAntagImmunity : byte
{
    /// <summary>
    /// Укол, газ и случайные ситуации антагониста не берут.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Случайные ситуации не берут. Укол и газ берут.
    /// </summary>
    Partial = 1,

    /// <summary>
    /// Антагонист заболевает так же, как остальные.
    /// </summary>
    None = 2,
}
