// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Numerics;
using Content.Client.DeadSpace.Afterimages;
using Content.Shared.DeadSpace.Dodge;

namespace Content.Client.DeadSpace.Dodge;

public sealed partial class DodgeVisualSystem : EntitySystem
{
    [Dependency] private readonly DeadSpaceAfterimageSystem _afterimages = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<DodgeEvent>(OnDodge);
    }

    private void OnDodge(DodgeEvent args)
    {
        var uid = GetEntity(args.Entity);
        if (!TryComp<DodgeVisualComponent>(uid, out var visual))
            return;

        var coordinates = Transform(uid).Coordinates;
        var rotation = _transform.GetWorldRotation(uid);
        var forward = rotation.ToWorldVec();
        var sideways = new Vector2(-forward.Y, forward.X);

        for (var i = -1; i <= 1; i++)
        {
            var offset = sideways * (i * visual.SidewaysOffset) -
                         forward * (visual.BackwardOffset + MathF.Abs(i) * visual.OuterBackwardOffset);
            _afterimages.TrySpawnAfterimage(
                uid,
                coordinates.Offset(offset),
                rotation,
                visual.StartColor,
                visual.Lifetime,
                visual.FallbackEffect,
                visual.EndColor);
        }
    }
}
