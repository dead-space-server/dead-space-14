// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.EntityEffects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.DeadSpace.Surgery;

public sealed partial class LimbMend : EntityEffectBase<LimbMend>
{
    [DataField]
    public float Bleed;

    [DataField]
    public bool OrganBleed;

    [DataField]
    public float Bone;

    [DataField]
    public float Crack;

    [DataField]
    public float Fracture;

    [DataField]
    public float Rate = 0.5f;

    [DataField]
    public float Shard;

    [DataField]
    public float Revive;

    [DataField]
    public LocId Text = "reagent-effect-guidebook-limb-mend";

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString(Text, ("chance", Probability), ("crack", Crack), ("fracture", Fracture));
}

public sealed partial class LimbMendSystem : EntityEffectSystem<LimbBodyComponent, LimbMend>
{
    [Dependency] private readonly LimbWoundSystem _limbs = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;

    protected override void Effect(Entity<LimbBodyComponent> entity, ref EntityEffectEvent<LimbMend> args)
    {
        if (_net.IsClient)
            return;

        var effect = args.Effect;
        var scale = args.Scale;
        if (effect.Crack > 0f || effect.Fracture > 0f)
            KnitBone(entity, effect, scale);
        var shardDone = false;
        foreach (var zone in Enum.GetValues<LimbZone>())
        {
            if (!LimbWoundRules.IsBody(zone))
                continue;

            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) entity.Comp.Zones.Count)
                continue;

            var state = entity.Comp.Zones[index];
            if (state.Severed)
                continue;

            var edit = new LimbEdit();
            var touched = false;
            if (effect.Bleed > 0f && state.Bleed > 0f)
            {
                edit.Bleed = -effect.Bleed * scale;
                touched = true;
            }

            if (effect.Crack <= 0f && effect.Fracture <= 0f && effect.Bone > 0f && state.Bone > 0f)
            {
                edit.Bone = -effect.Bone * scale;
                touched = true;
            }

            if (effect.Shard > 0f && state.Embedded && !shardDone && _random.Prob(Math.Clamp(effect.Shard * scale, 0f, 1f)))
            {
                edit.ClearEmbedded = true;
                shardDone = true;
                touched = true;
            }

            if (effect.Revive > 0f && state.Atrophied)
            {
                edit.Health = LimbWoundRules.HealthFor(zone, entity.Comp) * effect.Revive * scale;
                edit.ClearAtrophy = true;
                touched = true;
            }

            if (touched)
                _limbs.ApplyEdit(entity, zone, edit);
        }

        if (effect.OrganBleed && effect.Bleed > 0f)
        {
            for (var i = 0; i < entity.Comp.Organs.Count && i < LimbOrganRules.Count; i++)
            {
                var organ = entity.Comp.Organs[i];
                if (!organ.Ruptured || organ.BleedStopped)
                    continue;

                _limbs.ApplyOrganEdit(entity, (LimbOrgan) i, new LimbOrganEdit { SetBleedStopped = true, BleedStopped = true });
            }
        }
    }

    private void KnitBone(Entity<LimbBodyComponent> entity, LimbMend effect, float scale)
    {
        entity.Comp.BoneMend += scale * effect.Rate;
        var fracture = -1;
        var crack = -1;
        foreach (var zone in Enum.GetValues<LimbZone>())
        {
            if (!LimbWoundRules.IsBody(zone))
                continue;

            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) entity.Comp.Zones.Count || entity.Comp.Zones[index].Severed)
                continue;

            var bone = entity.Comp.Zones[index].Bone;
            if (fracture < 0 && bone >= LimbWoundRules.BreakBone)
                fracture = index;
            else if (crack < 0 && bone >= LimbWoundRules.StrainBone)
                crack = index;
        }

        if (fracture >= 0)
        {
            if (entity.Comp.BoneMend < effect.Fracture)
                return;

            entity.Comp.BoneMend -= effect.Fracture;
            _limbs.ApplyEdit(entity, (LimbZone) fracture, new LimbEdit { SetBone = true, Bone = 0f });
            return;
        }

        if (crack < 0 || entity.Comp.BoneMend < effect.Crack)
            return;

        entity.Comp.BoneMend -= effect.Crack;
        _limbs.ApplyEdit(entity, (LimbZone) crack, new LimbEdit { SetBone = true, Bone = 0f });
    }
}
