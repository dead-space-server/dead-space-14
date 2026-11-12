using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Collections;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

public sealed partial class JetpackSystem : SharedJetpackSystem
{
    [Dependency] private GasTankSystem _gasTank = default!;
    [Dependency] private IGameTiming _timing = default!;

    protected override bool CanEnable(Entity<JetpackComponent> ent)
    {
        // DS14-start
        if (!ent.Comp.RequiresGas)
            return base.CanEnable(ent);
        // DS14-end

        return base.CanEnable(ent) &&
               TryComp<GasTankComponent>(ent.Owner, out var gasTank) &&
               !(gasTank.Air.TotalMoles < ent.Comp.MoleUsage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toDisable = new ValueList<Entity<JetpackComponent>>();
        var query = EntityQueryEnumerator<ActiveJetpackComponent, JetpackComponent>(); // DS14

        while (query.MoveNext(out var uid, out var active, out var comp)) // DS14
        {
            // DS14-start
            if (!comp.RequiresGas)
                continue;

            if (!TryComp<GasTankComponent>(uid, out var gasTankComp))
                continue;
            // DS14-end

            if (_timing.CurTime < active.NextGasUsage)
                continue;

            var gasTank = (uid, gasTankComp);
            active.NextGasUsage = _timing.CurTime + TimeSpan.FromSeconds(comp.GasUsageInterval);
            var usedAir = _gasTank.RemoveAir(gasTank, comp.MoleUsage);

            var usedEnoughAir =
                MathHelper.CloseTo(usedAir.TotalMoles, comp.MoleUsage, comp.MoleUsage / 100);

            if (!usedEnoughAir)
                toDisable.Add((uid, comp));

            _gasTank.UpdateUserInterface(gasTank);
        }

        foreach (var ent in toDisable)
        {
            SetEnabled(ent.AsNullable(), false);
        }
    }
}