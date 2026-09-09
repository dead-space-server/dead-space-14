using Content.Shared.DeadSpace.Fishing;
using Content.Shared.Input;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.DeadSpace.Fishing;

public sealed class LavaFishingSystem : SharedLavaFishingSystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly InputSystem _input = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new LavaFishingLineOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<LavaFishingLineOverlay>();
        base.Shutdown();
    }

    public LavaFishingReelState GetRenderState(Entity<LavaFishingRodComponent> ent)
    {
        var rod = ent.Comp;
        if (rod.Phase != LavaFishingPhase.Reeling || _timing.Paused || MetaData(ent).EntityPaused)
            return new LavaFishingReelState(rod.ZonePosition, rod.FishPosition, rod.Progress, rod.Elapsed);

        var remainder = (float) Math.Clamp(_timing.TickRemainder.TotalSeconds, 0, _timing.TickPeriod.TotalSeconds);
        var pulling = _input.CmdStates.GetState(ContentKeyFunctions.UseItemInHand) == BoundKeyState.Down;
        return AdvanceReeling(rod, remainder, pulling);
    }
}
