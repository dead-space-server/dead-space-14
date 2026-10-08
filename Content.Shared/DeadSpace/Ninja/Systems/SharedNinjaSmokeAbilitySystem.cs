// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Ninja.Components;

namespace Content.Shared.DeadSpace.Ninja.Systems;

public abstract class SharedNinjaSmokeAbilitySystem : EntitySystem
{
    public virtual bool TrySpawnNinjaSmoke(Entity<NinjaSmokeAbilityComponent> ent, bool autoMode)
    {
        return false;
    }
}