using Content.Server.Doors.Systems;          // DoorSystem
using Content.Shared.Doors.Components;
using Content.Shared.Sectants;
using Robust.Shared.Timing;

namespace Content.Server.Sectants;

public sealed class SectantFemaleHackSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DoorSystem _door = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SectantFemaleHackActionEvent>(OnHack);
    }

    private void OnHack(SectantFemaleHackActionEvent args)
    {
        var xform = Transform(args.Performer);
        var coords = xform.Coordinates;
        var nearby = _lookup.GetEntitiesInRange(coords, 3f);

        foreach (var ent in nearby)
        {
            if (!TryComp<DoorComponent>(ent, out var door))
                continue;

            var hacked = EnsureComp<SectantHackedComponent>(ent);
            hacked.RevertAt = _timing.CurTime + TimeSpan.FromSeconds(20);
            _door.StartOpening(ent, door, args.Performer);
        }

        args.Handled = true;
    }
}
