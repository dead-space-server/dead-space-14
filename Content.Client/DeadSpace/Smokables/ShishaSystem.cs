using Content.Client.Items.Systems;
using Content.Shared.DeadSpace.Smokables;
using Content.Shared.Hands;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Serialization.Manager;

namespace Content.Client.DeadSpace.Smokables;

public sealed class ShishaSystem : SharedShishaSystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly ItemSystem _items = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new ShishaHoseOverlay(EntityManager));
        SubscribeLocalEvent<ShishaComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<ShishaComponent, GetInhandVisualsEvent>(OnInhandVisuals, after: [typeof(ItemSystem)]);
    }

    private void OnAppearanceChanged(Entity<ShishaComponent> ent, ref AppearanceChangeEvent args)
    {
        _items.VisualsChanged(ent);
    }

    private void OnInhandVisuals(EntityUid uid, ShishaComponent component, GetInhandVisualsEvent args)
    {
        if (!_appearance.TryGetData<string>(uid, ShishaVisuals.State, out var state))
            return;

        // The carried base uses the same art at half scale, always without the hanging hose.
        if (!state.EndsWith("-no-hose"))
            state += "-no-hose";
        for (var i = 0; i < args.Layers.Count; i++)
        {
            var (key, data) = args.Layers[i];
            var layer = _serialization.CreateCopy(data, notNullableOverride: true);
            layer.State = state;
            args.Layers[i] = (key, layer);
        }
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<ShishaHoseOverlay>();
        base.Shutdown();
    }
}
