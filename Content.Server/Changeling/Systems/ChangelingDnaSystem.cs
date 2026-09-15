using System.Linq;
using Content.Server.Store.Systems;
using Content.Shared.Changeling.Components;
using Content.Shared.Fluids;
using Content.Shared.Humanoid;
using Content.Shared.Store.Components;

namespace Content.Server.Changeling.Systems;

/// <summary>
/// Awards DNA currency after a successful unique devour.
/// </summary>
public sealed class ChangelingDnaSystem : EntitySystem
{
    private const float IpcDnaRewardMultiplier = 0.3f; // DS14

    [Dependency] private readonly SharedPuddleSystem _puddle = default!; // DS14
    [Dependency] private readonly StoreSystem _store = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChangelingDevouredEvent>(OnDevoured); // DS14: StoreSystem is server-only on this content baseline.
    }

    private void OnDevoured(ref ChangelingDevouredEvent args)
    {
        // DS14-start
        if (!TryComp<ChangelingDevourComponent>(args.Changeling, out var devour))
            return;

        if (devour.DevourSpill is { } devourSpill)
            _puddle.TrySpillAt(args.Devoured, devourSpill.Clone(), out _, false);
        // DS14-end

        if (!args.GrantedDna ||
            !TryComp<StoreComponent>(args.Changeling, out var store))
        {
            return;
        }

        // DS14-start
        var multiplier = TryComp<HumanoidAppearanceComponent>(args.Devoured, out var humanoid) && humanoid.Species == "IPC"
            ? IpcDnaRewardMultiplier
            : 1f;
        var reward = devour.DevourDnaReward.ToDictionary(pair => pair.Key.Id, pair => pair.Value * multiplier);
        // DS14-end
        _store.TryAddCurrency(reward, args.Changeling, store);
    }
}
