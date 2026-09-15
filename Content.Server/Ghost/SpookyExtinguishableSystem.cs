using Content.Server.Atmos.EntitySystems;
using Content.Server.Ghost.Components;
using Content.Shared.Atmos.Components;
using Robust.Server.Audio;
using Robust.Shared.Random;

namespace Content.Server.Ghost;

/// <summary>
/// Handles spooky extinguishing without replacing this branch's legacy ghost and powered-light systems.
/// </summary>
// DS14: semantic #44810 adapter for the current explicit-subscription engine baseline.
public sealed class SpookyExtinguishableSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpookyExtinguishableComponent, GhostBooEvent>(OnGhostBoo);
    }

    private void OnGhostBoo(Entity<SpookyExtinguishableComponent> ent, ref GhostBooEvent args)
    {
        if (args.Handled || !_random.Prob(ent.Comp.ExtinguishChance))
            return;

        if (!TryComp<FlammableComponent>(ent.Owner, out var flammable) ||
            !flammable.OnFire ||
            !flammable.CanExtinguish)
        {
            return;
        }

        // DS14: retain the legacy FlammableSystem API instead of importing its unrelated refactor.
        _flammable.Extinguish(ent.Owner, flammable);

        if (ent.Comp.ExtinguishSound != null)
            _audio.PlayPvs(ent.Comp.ExtinguishSound, ent.Owner);

        args.Handled = true;
    }
}
