using Content.Server.Body.Systems;
using Content.Shared.Changeling;
using Content.Shared.Changeling.Components;

namespace Content.Server.Changeling.Systems;

/// <summary>
/// Compatibility bridge for Wizards' shared metabolism component on DS14's server-side metabolism architecture.
/// </summary>
public sealed class ChangelingMetabolismSystem : EntitySystem
{
    [Dependency] private readonly MetabolizerSystem _metabolizer = default!;

    public override void Initialize()
    {
        base.Initialize();

        // DS14: metabolism is server-only on the pre-v288 content baseline.
        SubscribeLocalEvent<AddMetabolismComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AddMetabolismComponent, AfterChangelingTransformEvent>(OnAfterTransform);
    }

    private void OnMapInit(Entity<AddMetabolismComponent> ent, ref MapInitEvent args)
    {
        AddMetabolism(ent);
    }

    private void OnAfterTransform(Entity<AddMetabolismComponent> ent, ref AfterChangelingTransformEvent args)
    {
        AddMetabolism(ent);
    }

    private void AddMetabolism(Entity<AddMetabolismComponent> ent)
    {
        if (ent.Comp.AddedMetabolizer is { } metabolizer)
            _metabolizer.AddMetabolizerToBody(ent, metabolizer);
    }
}
