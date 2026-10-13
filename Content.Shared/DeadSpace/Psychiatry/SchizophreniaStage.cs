// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Psychiatry;

public enum SchizophreniaStage : byte
{
    None = 0,
    // Нет симптомов. Через автопрогресс становится латентной.
    Incipient = 1,
    Latent = 2,
    Simple = 3,
    Acute = 4,
}
