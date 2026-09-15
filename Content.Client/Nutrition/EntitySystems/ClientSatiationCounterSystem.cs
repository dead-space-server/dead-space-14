using Content.Shared.Alert.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Client.Nutrition.EntitySystems;

/// <summary>
/// Supplies satiation values to generic counter alerts.
/// </summary>
public sealed class ClientSatiationCounterSystem : EntitySystem
{
    [Dependency] private readonly SatiationSystem _satiation = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    private EntityQuery<SatiationCounterAlertComponent> _counterQuery; // DS14 - initialized explicitly on the current engine.

    public override void Initialize()
    {
        base.Initialize();
        _counterQuery = GetEntityQuery<SatiationCounterAlertComponent>(); // DS14
        SubscribeLocalEvent<SatiationComponent, GetGenericAlertCounterAmountEvent>(OnGenericCounter);
    }

    private void OnGenericCounter(Entity<SatiationComponent> entity, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled ||
            !_counterQuery.TryComp(args.SpriteView, out var alert) ||
            !_prototypes.Resolve(alert.SatiationType, out var satiationType) ||
            _satiation.GetValueOrNull(entity, satiationType) is not { } amount)
        {
            return;
        }

        args.Amount = (int) amount;
    }
}
