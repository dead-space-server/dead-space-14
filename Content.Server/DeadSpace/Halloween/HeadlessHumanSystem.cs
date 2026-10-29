using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;

namespace Content.Server.DeadSpace.Halloween;
// so a headless body must hide the head-related visual layers explicitly.
[RegisterComponent]
public sealed partial class HeadlessHumanComponent : Component;

public sealed class HeadlessHumanSystem : EntitySystem
{
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    private static readonly HumanoidVisualLayers[] HeadLayers =
    {
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.SnoutCover,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.Eyes,
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeadlessHumanComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<HeadlessHumanComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(ent, out var humanoid))
            return;

        var dirty = false;
        foreach (var layer in HeadLayers)
            _humanoid.SetLayerVisibility((ent.Owner, humanoid), layer, false, null, ref dirty);
    }
}
