using Content.Shared.Mobs;

namespace Content.Server.DeadSpace.Halloween;

public sealed class CandyDropSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CandyDropComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<CandyDropComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Dropped)
            return;

        ent.Comp.Dropped = true;

        var coords = Transform(ent).Coordinates;

        for (var i = 0; i < ent.Comp.Amount; i++)
        {
            Spawn(ent.Comp.Candy, coords);
        }
    }
}