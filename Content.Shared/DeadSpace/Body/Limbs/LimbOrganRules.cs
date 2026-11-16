// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

namespace Content.Shared.DeadSpace.Body.Limbs;

public static class LimbOrganRules
{
    public const int Count = 8;
    public const float RuptureHpShare = 0.9f;
    public const float Rupture = 40f;
    public const float RuptureChance = 0.2f;
    public const float HeartBleed = 0.5f;
    public const float LungAir = 1f;
    public const float BrainAir = 0.2f;
    public const float LiverPoison = 0.25f;
    public const float KidneyPoison = 0.15f;
    public const float StomachPoison = 0.1f;
    public const float LungTornAir = 0.9f;
    public const int EyeRuptureDamage = 9;
    public const float LungHitAir = 6f;
    public const float LungHitShare = 0.3f;
    public const float ConcussionHit = 12f;
    public const float ConcussionBrain = 15f;

    public static (float Damage, bool Ruptured) Carry(float damage, bool ruptured, bool tore, bool rotten, float decay)
    {
        var next = MathF.Max(0f, damage);
        next = MathF.Max(next, MathF.Max(0f, decay) * Rupture);
        var burst = ruptured || tore || rotten;
        if (burst)
            next = MathF.Max(next, Rupture);

        return (next, burst);
    }

    public static float OrganSize(LimbOrgan organ)
    {
        return organ switch
        {
            LimbOrgan.Heart => 0.1f,
            LimbOrgan.Lungs => 0.2f,
            LimbOrgan.Kidneys => 0.16f,
            LimbOrgan.Stomach => 0.18f,
            LimbOrgan.Brain => 0.35f,
            LimbOrgan.Eyes => 0.08f,
            _ => 0.14f,
        };
    }

    public static List<LimbOrganState> CreateAll()
    {
        var list = new List<LimbOrganState>(Count);
        for (var i = 0; i < Count; i++)
            list.Add(new LimbOrganState());

        return list;
    }

    public static bool TryTarget(LimbZone zone, LimbMark mark, out LimbOrgan organ, bool projectile = false)
    {
        var plan = Plan(zone, mark, projectile);
        if (plan.Count == 0)
        {
            organ = default;
            return false;
        }

        organ = plan.First.Organ;
        return true;
    }

    public static OrganPlan Plan(LimbZone zone, LimbMark mark, bool projectile = false)
    {
        OrganPlan plan = default;
        foreach (var row in LimbCatalog.Rows)
        {
            if (row.Hook != LimbHooks.Organ || !LimbCatalog.Fits(row, zone, mark, projectile))
                continue;

            if (plan.Count == 0)
                plan = OrganPlan.One(row.Organ, row.Factor);
            else
                plan = OrganPlan.Two(plan.First.Organ, plan.First.Factor, row.Organ, row.Factor);
        }

        return plan;
    }

    public static bool ShouldRupture(float lost)
    {
        return lost >= RuptureHpShare;
    }

    public static OrganHit Apply(Span<LimbOrganState> organs, LimbZone zone, LimbMark mark, float amount, float woundMultiplier, LimbOrganMask present = LimbOrganMask.Standard, bool projectile = false)
    {
        if (amount <= 0f)
            return OrganHit.None;

        var ruptured = false;
        var eyes = 0;
        var concussion = false;
        var primary = default(LimbOrgan);
        var any = false;
        var scaled = amount * woundMultiplier;
        foreach (var row in LimbCatalog.Rows)
        {
            if (row.Hook != LimbHooks.Organ || !LimbCatalog.Fits(row, zone, mark, projectile))
                continue;

            if ((present & (LimbOrganMask) (1 << (int) row.Organ)) == 0)
                continue;

            if (!any)
                primary = row.Organ;

            any = true;
            concussion |= Hurt(organs, new OrganShare(row.Organ, row.Factor), mark, scaled);
        }

        if (!any)
            return OrganHit.None;

        return new OrganHit(true, primary, ruptured, eyes, concussion);
    }

    public static int RuptureHome(Span<LimbOrganState> organs, LimbOrganMask present, LimbZone zone, float chance, float roll, float pick)
    {
        if (chance <= 0f || roll >= chance)
            return 0;

        var count = Math.Min(organs.Length, Count);
        var eligible = 0;
        for (var i = 0; i < count; i++)
        {
            if (MayRupture(organs, present, zone, i))
                eligible++;
        }

        if (eligible == 0)
            return 0;

        var wanted = Math.Clamp((int) (pick * eligible), 0, eligible - 1);
        for (var i = 0; i < count; i++)
        {
            if (!MayRupture(organs, present, zone, i))
                continue;

            if (wanted > 0)
            {
                wanted--;
                continue;
            }

            organs[i].Ruptured = true;
            return (LimbOrgan) i == LimbOrgan.Eyes ? EyeRuptureDamage : 0;
        }

        return 0;
    }

    private static bool MayRupture(ReadOnlySpan<LimbOrganState> organs, LimbOrganMask present, LimbZone zone, int index)
    {
        return LimbOrganHomes.Home((LimbOrgan) index) == zone
               && (present & (LimbOrganMask) (1 << index)) != 0
               && !organs[index].Missing
               && !organs[index].Ruptured;
    }

    private static bool Hurt(Span<LimbOrganState> organs, OrganShare share, LimbMark mark, float scaled)
    {
        var index = (int) share.Organ;
        if (index < 0 || index >= organs.Length)
            return false;

        ref var state = ref organs[index];
        if (state.Ruptured)
            return false;

        var before = state.Damage;
        state.Damage += scaled * share.Factor;
        var concussion = share.Organ == LimbOrgan.Brain
                         && mark is LimbMark.Blunt or LimbMark.Point
                         && (scaled >= ConcussionHit || (before < ConcussionBrain && state.Damage >= ConcussionBrain));
        return concussion;
    }

    public static void Cryo(Span<LimbOrganState> organs)
    {
        for (var i = 0; i < organs.Length; i++)
        {
            if (organs[i].Ruptured)
                continue;

            organs[i].Damage = MathF.Max(0f, organs[i].Damage - 15f);
        }
    }

    public static OrganFailure Tick(ReadOnlySpan<LimbOrganState> organs)
    {
        var blood = 0f;
        var air = 0f;
        var poison = 0f;
        if (Bleeding(organs, LimbOrgan.Heart))
            blood += HeartBleed;
        if (Ruptured(organs, LimbOrgan.Brain))
            air += BrainAir;

        var lungs = (int) LimbOrgan.Lungs;
        if (lungs < organs.Length)
            air += Breath(organs[lungs].Damage, organs[lungs].Ruptured);

        if (Ruptured(organs, LimbOrgan.Liver))
            poison += LiverPoison;
        if (Ruptured(organs, LimbOrgan.Kidneys))
            poison += KidneyPoison;
        if (Ruptured(organs, LimbOrgan.Stomach))
            poison += StomachPoison;

        return new OrganFailure(blood, air, poison);
    }

    public static float Breath(float damage, bool ruptured)
    {
        if (ruptured)
            return LungTornAir;

        if (damage >= 15f)
            return 0.7f;

        return 0f;
    }

    private static bool Ruptured(ReadOnlySpan<LimbOrganState> organs, LimbOrgan organ)
    {
        var index = (int) organ;
        return index < organs.Length && organs[index].Ruptured;
    }

    private static bool Bleeding(ReadOnlySpan<LimbOrganState> organs, LimbOrgan organ)
    {
        var index = (int) organ;
        return index < organs.Length && organs[index].Ruptured && !organs[index].BleedStopped;
    }
}

public readonly record struct OrganShare(LimbOrgan Organ, float Factor);

public struct OrganPlan
{
    public int Count;
    public OrganShare First;
    public OrganShare Second;

    public static OrganPlan One(LimbOrgan organ, float factor)
    {
        return new OrganPlan { Count = 1, First = new OrganShare(organ, factor) };
    }

    public static OrganPlan Two(LimbOrgan first, float firstFactor, LimbOrgan second, float secondFactor)
    {
        return new OrganPlan
        {
            Count = 2,
            First = new OrganShare(first, firstFactor),
            Second = new OrganShare(second, secondFactor),
        };
    }
}

public static class LimbOrganHomes
{
    public static LimbZone Home(LimbOrgan organ)
    {
        return organ switch
        {
            LimbOrgan.Brain or LimbOrgan.Eyes => LimbZone.Head,
            LimbOrgan.Kidneys => LimbZone.Groin,
            _ => LimbZone.Torso,
        };
    }
}

public readonly record struct OrganHit(bool Hit, LimbOrgan Organ, bool Ruptured, int EyeDamage, bool Concussion = false)
{
    public static OrganHit None => new(false, default, false, 0);
}

public readonly record struct OrganFailure(float Bloodloss, float Asphyxiation, float Poison);
