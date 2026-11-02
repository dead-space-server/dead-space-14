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
    public float Bone;

    [DataField]
    public float Shard;

    [DataField]
    public float Revive;

    [DataField]
    public LocId Text = "reagent-effect-guidebook-limb-mend";

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString(Text, ("chance", Probability));
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

            if (effect.Bone > 0f && state.Bone > 0f)
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
    }
}
