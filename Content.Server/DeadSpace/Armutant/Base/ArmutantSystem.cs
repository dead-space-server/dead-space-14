using Content.Shared.DeadSpace.Armutant;
using Content.Server.Body.Systems;
using Content.Shared.Cuffs.Components;
using Content.Server.Cuffs;
using Content.Server.Destructible.Thresholds.Triggers;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible;
using System.Linq;
using Content.Server.Beam;

namespace Content.Server.DeadSpace.Armutant.Base;

public sealed class ArmutantSystem : SharedArmutantSystem
{
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly CuffableSystem _cuffable = default!;
    [Dependency] private readonly BeamSystem _beamSystem = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ArmutantComponent, BloodStreamRecoveryEvent>(RecoveryBloodStream);
        SubscribeLocalEvent<ArmutantComponent, UnCuffableArmEvent>(UnCuffableArm);
        SubscribeLocalEvent<ArmutantComponent, SetNewDestructibleThreshold>(SetDestructibleThreshold);
        SubscribeLocalEvent<ArmutantComponent, BeamActiveVoidHold>(OnBeamActive);
    }
    private void RecoveryBloodStream(Entity<ArmutantComponent> ent, ref BloodStreamRecoveryEvent args)
    {
        _blood.TryModifyBloodLevel(ent, 1000);
        _blood.TryModifyBleedAmount(ent, -1000);
    }
    private void UnCuffableArm(Entity<ArmutantComponent> ent, ref UnCuffableArmEvent args)
    {
        if (TryComp<CuffableComponent>(ent, out var cuffs) && cuffs.Container.ContainedEntities.Count > 0)
            _cuffable.Uncuff(ent, cuffs.LastAddedCuffs, cuffs.LastAddedCuffs);
    }
    private void SetDestructibleThreshold(Entity<ArmutantComponent> ent, ref SetNewDestructibleThreshold args)
    {
        var armutantThreshold = EnsureComp<DestructibleComponent>(ent);

        var bluntThreshold = armutantThreshold.Thresholds
            .OfType<DamageThreshold>()
            .FirstOrDefault(t => t.Trigger is DamageTypeTrigger damageTrigger &&
                                 damageTrigger.DamageType == "Blunt");

        var newThreshold = new DamageThreshold
        {
            Trigger = new DamageTypeTrigger
            {
                DamageType = ent.Comp.DamageTypeGib,
                Damage = ent.Comp.DamageAmountGib
            }
        };
        armutantThreshold.Thresholds.Add(newThreshold);
    }
    private void OnBeamActive(Entity<ArmutantComponent> ent, ref BeamActiveVoidHold args)
    {
        _beamSystem.TryCreateBeam(ent, args.Target, args.Effect);
    }
}
