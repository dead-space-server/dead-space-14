using System.Linq;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.GhostTypes;

public sealed partial class GhostSpriteStateSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    /// <summary>
    /// It goes through an entity damage and assigns them a sprite according to the highest damage type/s
    /// </summary>
    public void SetGhostSprite(Entity<GhostSpriteStateComponent?> ent, EntityUid mind)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (!TryComp<AppearanceComponent>(ent, out var appearance) || !HasComp<MindComponent>(mind))
            return;

        var damageTypes = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>();
        ProtoId<SpecialCauseOfDeathPrototype>? specialCase = null;

        if (!TryComp<LastBodyDamageComponent>(mind, out var storedDamage))
          return;

        if (storedDamage.DamagePerGroup != null && storedDamage.Damage != null)
        {
            foreach (var (damageGroupId, _) in storedDamage.DamagePerGroup)
            {
                var group = _proto.Index<DamageGroupPrototype>(damageGroupId);
                foreach (var type in group.DamageTypes)
                {
                    if (!storedDamage.Damage.DamageDict.TryGetValue(type, out var damageValue) || damageValue == 0)
                        continue;

                    damageTypes.TryAdd(type, damageValue);
                }
            }
        }
        specialCase = storedDamage.SpecialCauseOfDeath;

        Dirty(mind, storedDamage);

        var damageTypesSorted = damageTypes.OrderByDescending(x => x.Value).ToDictionary();
        if (damageTypesSorted.Count == 0)
            return;

        var highestType = damageTypesSorted.First().Key; // We only need 1 of the values

        var rand = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));

        ProtoId<DamageTypePrototype>? spriteState = null;

        if (specialCase != null)  // Possible special cases like death by an explosion
        {
            var prototype = _proto.Index(specialCase);
            spriteState = specialCase + rand.Next(prototype.NumOfStates);
        }
        else if (ent.Comp.DamageMap.TryGetValue(highestType, out var spriteAmount))
        {
                spriteState = highestType + rand.Next(spriteAmount);
        }

        if (spriteState != null)
            _appearance.SetData(ent, GhostVisuals.Damage, ent.Comp.Prefix + spriteState, appearance);
    }
}
