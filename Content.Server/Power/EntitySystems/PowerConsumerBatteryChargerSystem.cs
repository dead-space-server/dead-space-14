using Content.Server.Power.Components;
using Content.Shared.Power.Components;

namespace Content.Server.Power.EntitySystems;

public sealed partial class PowerConsumerBatteryChargerSystem : EntitySystem
{
    [Dependency] private readonly BatterySystem _battery = default!; // DS14 - pre-source-generator IoC style.

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<PowerConsumerComponent, PowerConsumerBatteryChargerComponent, BatteryComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var powerConsumer, out var batteryCharger, out var battery, out var transformComp)) // DS14 - old battery API needs the component.
        {
            if (!transformComp.Anchored)
                continue;

            var powerConsumed = powerConsumer.ReceivedPower * frameTime;
            _battery.ChangeCharge((uid, battery), powerConsumed * batteryCharger.Efficiency); // DS14 - old battery API.
        }
    }
}
