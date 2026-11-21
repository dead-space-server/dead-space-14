// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Body.Limbs;
using Content.Shared.EntityEffects;

namespace Content.Server.DeadSpace.Body;

public sealed partial class ChiyuriMendSystem : EntityEffectSystem<LimbBodyComponent, ChiyuriMend>
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

    [Dependency] private LimbWoundSystem _limbs = default!;

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
            MendOrgan(entity);
            RegrowOne(entity, effect.Regrow);
        }
    }

    private void MendOne(Entity<LimbBodyComponent> entity)
    {
        if (Fix(entity, state => state.Embedded, Full(new LimbEdit { ClearEmbedded = true })))
            return;

        if (Fix(entity, state => state.Bone >= LimbWoundRules.StrainBone, Full(new LimbEdit { SetBone = true, Bone = 0f })))
            return;

        if (Fix(entity, state => state.Bleed > LimbWoundRules.TickBleed, Full(new LimbEdit { SetBleed = true, Bleed = 0f })))
            return;

        if (Fix(entity, state => state.Flesh > 0f, Full(new LimbEdit { SetFlesh = true, Flesh = 0f })))
            return;

        if (Fix(entity, state => state.SkinBare || state.Skin > 0f, Full(new LimbEdit { Skin = -1000f, ClearSkinBare = true })))
            return;

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

            var cap = LimbWoundRules.HealthFor(zone, entity.Comp);
            if (!state.Atrophied && state.Health >= cap)
                continue;

            _limbs.ApplyEdit(entity, zone, new LimbEdit
            {
                ClearAtrophy = true,
                SetHealth = true,
                Health = cap,
            });
            return;
        }
    }

    private static LimbEdit Full(LimbEdit edit)
    {
        edit.SetHealth = true;
        edit.Health = 10000f;
        return edit;
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

    private void MendOrgan(Entity<LimbBodyComponent> entity)
    {
        foreach (var organ in Enum.GetValues<LimbOrgan>())
        {
            var bit = (LimbOrganMask) (1 << (int) organ);
            if ((entity.Comp.Present & bit) == 0)
                continue;

            var index = (int) organ;
            if ((uint) index >= (uint) entity.Comp.Organs.Count)
                continue;

            var state = entity.Comp.Organs[index];
            if (state.Missing || state.Damage <= 0f && !state.Ruptured)
                continue;

            _limbs.ApplyOrganEdit(entity, organ, new LimbOrganEdit
            {
                SetDamage = true,
                Damage = 0f,
                SetRuptured = true,
                Ruptured = false,
            });
            return;
        }
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
