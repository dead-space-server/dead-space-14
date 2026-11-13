using Content.Server.Atmos.Components;
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

    protected override bool CanEnable(EntityUid uid, JetpackComponent component)
    {
        // DS14-start
        if (!component.RequiresGas)
            return base.CanEnable(uid, component);
        // DS14-end

        return base.CanEnable(uid, component) &&
               TryComp<GasTankComponent>(uid, out var gasTank) &&
               !(gasTank.Air.TotalMoles < component.MoleUsage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toDisable = new ValueList<(EntityUid Uid, JetpackComponent Component)>();
        var query = EntityQueryEnumerator<ActiveJetpackComponent, JetpackComponent>(); // DS14

        while (query.MoveNext(out var uid, out var active, out var comp)) // DS14
        {
            // DS14-start
            if (!comp.RequiresGas)
                continue;

            if(!TryComp<GasTankComponent>(uid, out var gasTankComp))
                continue;
            // DS14-end

            if (_timing.CurTime < active.TargetTime)
                continue;

            var gasTank = (uid, gasTankComp);
            active.TargetTime = _timing.CurTime + TimeSpan.FromSeconds(active.EffectCooldown);
            var usedAir = _gasTank.RemoveAir(gasTank, comp.MoleUsage);

            if (usedAir == null)
                continue;

            var usedEnoughAir =
                MathHelper.CloseTo(usedAir.TotalMoles, comp.MoleUsage, comp.MoleUsage/100);

            if (!usedEnoughAir)
            {
                toDisable.Add((uid, comp));
            }

            _gasTank.UpdateUserInterface(gasTank);
        }

        foreach (var (uid, comp) in toDisable)
        {
            SetEnabled(uid, comp, false);
        }
    }
}