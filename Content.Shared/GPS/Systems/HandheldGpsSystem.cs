using Content.Shared.GPS.Components;
using Content.Shared.Examine;
using Robust.Shared.Map;
using Content.Shared.DeadSpace.Lavaland; // DS14
using Robust.Shared.Random; // DS14

namespace Content.Shared.GPS.Systems;

public sealed class HandheldGpsSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly LavalandDayCycleSystem _dayCycle = default!; // DS14
    [Dependency] private readonly IRobustRandom _random = default!; // DS14

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandheldGPSComponent, ExaminedEvent>(OnExamine);
    }

    /// <summary>
    /// Handles showing the coordinates when a GPS is examined.
    /// </summary>
    private void OnExamine(Entity<HandheldGPSComponent> ent, ref ExaminedEvent args)
    {
        var posText = "Error";

        var pos = _transform.GetMapCoordinates(ent);

        if (pos.MapId != MapId.Nullspace)
        {
            var x = (int) pos.Position.X;
            var y = (int) pos.Position.Y;
            posText = $"({x}, {y})";
            // DS14-start: lavaland deep night jams the GPS on examine too, as in the item status
            if (_dayCycle.IsTrackerJammed(Transform(ent).MapUid))
                posText = $"({_random.Next(-999, 1000)}, ???)";
            // DS14-end
        }

        args.PushMarkup(Loc.GetString("handheld-gps-coordinates-title", ("coordinates", posText)));
    }
}
