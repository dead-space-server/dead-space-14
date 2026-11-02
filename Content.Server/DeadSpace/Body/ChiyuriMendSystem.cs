// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.EntityEffects;

namespace Content.Server.DeadSpace.Body;

public sealed class ChiyuriMendSystem : EntityEffectSystem<LimbBodyComponent, ChiyuriMend>
{
    private static readonly LimbZone[] RegrowOrder =
    {
        LimbZone.ArmLeft,
        LimbZone.ArmRight,
        LimbZone.LegLeft,
        LimbZone.LegRight,
        LimbZone.HandLeft,
        LimbZone.HandRight,
        LimbZone.FootLeft,
        LimbZone.FootRight,
    };

    [Dependency] private readonly LimbWoundSystem _limbs = default!;

    protected override void Effect(Entity<LimbBodyComponent> entity, ref EntityEffectEvent<ChiyuriMend> args)
    {
        var effect = args.Effect;
        if (effect.Dose <= 0f)
            return;

        entity.Comp.Chiyuri += args.Scale * effect.Rate;
        while (entity.Comp.Chiyuri >= effect.Dose)
        {
            entity.Comp.Chiyuri -= effect.Dose;
            MendOne(entity);
            RegrowOne(entity, effect.Regrow);
        }
    }

    private void MendOne(Entity<LimbBodyComponent> entity)
    {
        if (Fix(entity, state => state.Embedded, new LimbEdit { ClearEmbedded = true }))
            return;

        if (Fix(entity, state => state.Bone >= LimbWoundRules.StrainBone, new LimbEdit { SetBone = true, Bone = 0f }))
            return;

        if (Fix(entity, state => state.Bleed > LimbWoundRules.TickBleed, new LimbEdit { SetBleed = true, Bleed = 0f }))
            return;

        if (Fix(entity, state => state.Flesh > 0f, new LimbEdit { SetFlesh = true, Flesh = 0f }))
            return;

        if (Fix(entity, state => state.SkinBare || state.Skin > 0f, new LimbEdit { Skin = -1000f, ClearSkinBare = true }))
            return;

        foreach (var zone in Enum.GetValues<LimbZone>())
        {
            if (!LimbWoundRules.IsBody(zone))
                continue;

            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) entity.Comp.Zones.Count)
                continue;

            var state = entity.Comp.Zones[index];
            if (state.Severed || !state.Atrophied)
                continue;

            _limbs.ApplyEdit(entity, zone, new LimbEdit
            {
                ClearAtrophy = true,
                SetHealth = true,
                Health = LimbWoundRules.HealthFor(zone, entity.Comp) * 0.1f,
            });
            return;
        }
    }

    private bool Fix(Entity<LimbBodyComponent> entity, Func<LimbZoneState, bool> match, LimbEdit edit)
    {
        foreach (var zone in Enum.GetValues<LimbZone>())
        {
            if (!LimbWoundRules.IsBody(zone))
                continue;

            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) entity.Comp.Zones.Count)
                continue;

            var state = entity.Comp.Zones[index];
            if (state.Severed || !match(state))
                continue;

            _limbs.ApplyEdit(entity, zone, edit);
            return true;
        }

        return false;
    }

    private void RegrowOne(Entity<LimbBodyComponent> entity, float fraction)
    {
        foreach (var zone in RegrowOrder)
        {
            var index = LimbWoundRules.Index(zone);
            if ((uint) index >= (uint) entity.Comp.Zones.Count || !entity.Comp.Zones[index].Severed)
                continue;

            if (_limbs.TryRegrowZone(entity, zone, fraction))
                return;
        }
    }
}
