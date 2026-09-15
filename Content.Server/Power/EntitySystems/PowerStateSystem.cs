using Content.Server.Power.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;

namespace Content.Server.Power.EntitySystems;

public sealed class PowerStateSystem : SharedPowerStateSystem
{
    // DS14-start: explicit subscriptions for the pre-v288 engine.
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PowerStateComponent, ComponentStartup>(OnComponentStartup);
    }
    // DS14-end

    /// <summary> Init IsWorking and power values on startup. </summary>
    private void OnComponentStartup(Entity<PowerStateComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.EnsureApc)
            EnsureComp<ApcPowerReceiverComponent>(ent);

        SetPowerLoad(ent, ent.Comp.IsWorking);
    }

    /// <inheritdoc/>
    protected override void SetPowerLoad(Entity<PowerStateComponent> ent, bool isWorking)
    {
        base.SetPowerLoad(ent, isWorking);

        if (TryComp<PowerConsumerComponent>(ent, out var powerConsumer))
            powerConsumer.DrawRate = isWorking ? ent.Comp.WorkingPowerDraw : ent.Comp.IdlePowerDraw;
    }
}
