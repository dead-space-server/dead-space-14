// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Lavaland.Components;
using Content.Server.NPC.HTN;

namespace Content.Server.DeadSpace.Lavaland;

/// <summary>Mapped lavaland bosses: spawned by a marker when the map loads, living outside any arena.</summary>
public sealed class LavalandBossSpawnerSystem : EntitySystem
{
    [Dependency] private readonly HTNSystem _htn = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandBossSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<LavalandBossSpawnerComponent> ent, ref MapInitEvent args)
    {
        var boss = Spawn(ent.Comp.Boss, Transform(ent).Coordinates);
        EnsureComp<LavalandFreeBossComponent>(boss);

        if (TryComp<LavalandLegionMegafaunaComponent>(boss, out var legion))
        {
            legion.Free = true;
            legion.Dormant = ent.Comp.Dormant;
        }

        if (ent.Comp.Dormant && TryComp<HTNComponent>(boss, out var htn))
            _htn.SetHTNEnabled((boss, htn), false);

        QueueDel(ent);
    }
}
