using Content.Client.Overlays;
using Content.Shared.Screech;
using Robust.Client.Graphics;

namespace Content.Client.Effects;

/// <summary>
/// This system ensures that <see cref="ScreechShockWaveOverlay"/> does not use costly queries.
/// </summary>
public sealed partial class ScreechShockWaveSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;

    private readonly HashSet<EntityUid> _registered = [];
    private ScreechShockWaveOverlay _overlay = default!; // DS14 - the current engine does not auto-register overlays.

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ScreechShockWaveOverlay(); // DS14
        _overlayMan.AddOverlay(_overlay); // DS14
        SubscribeLocalEvent<ScreechShockWaveComponent, ComponentStartup>(OnScreechShockWaveStartup);
        SubscribeLocalEvent<ScreechShockWaveComponent, ComponentRemove>(OnScreechShockWaveRemoved);
    }

    public override void Shutdown()
    {
        _overlayMan.RemoveOverlay(_overlay); // DS14
        _registered.Clear();
        base.Shutdown();
    }

    private void OnScreechShockWaveStartup(Entity<ScreechShockWaveComponent> ent, ref ComponentStartup args)
    {
        // we must only pass here once
        if (!_registered.Add(ent.Owner))
            return;

        _overlay.Register(ent);
    }

    private void OnScreechShockWaveRemoved(Entity<ScreechShockWaveComponent> ent, ref ComponentRemove args)
    {
        _registered.Remove(ent.Owner);
    }
}
