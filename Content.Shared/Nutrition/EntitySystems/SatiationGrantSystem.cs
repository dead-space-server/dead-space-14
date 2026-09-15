using Content.Shared.Nutrition.Components;

namespace Content.Shared.Nutrition.EntitySystems;

/// <summary>
/// Adds and removes satiation types provided by a component.
/// </summary>
public sealed partial class SatiationGrantSystem : EntitySystem
{
    [Dependency] private readonly SatiationSystem _satiation = default!;

    public override void Initialize()
    {
        base.Initialize();

        // DS14: explicit subscriptions for the pre-v288 engine.
        SubscribeLocalEvent<SatiationGrantComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SatiationGrantComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnMapInit(Entity<SatiationGrantComponent> ent, ref MapInitEvent args)
    {
        foreach (var satiation in ent.Comp.Satiation)
            _satiation.AddSatiation(ent.Owner, satiation.Key, satiation.Value);
    }

    private void OnShutdown(Entity<SatiationGrantComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.RemoveOnShutdown)
            return;

        foreach (var satiation in ent.Comp.Satiation)
            _satiation.RemoveSatiationType(ent.Owner, satiation.Key);
    }
}
