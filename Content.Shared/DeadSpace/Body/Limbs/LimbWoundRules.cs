// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Atmos;

namespace Content.Shared.DeadSpace.Body.Limbs;

public enum LimbLife : byte
{
    Alive = 0,
    Crit = 1,
    Dead = 2,
}

public static class LimbWoundRules
{
    public const int BodyZoneCount = 11;

    public const float StrainFlesh = 20f;
    public const float BreakFlesh = 50f;
    public const float StrainBone = 25f;
    public const float BreakBone = 40f;
    public const float ExtremityHealth = 40f;
    public const float LimbHealth = 55f;
    public const float HeadHealth = 105f;
    public const float TorsoHealth = 110f;
    public const float GroinHealth = 80f;
    public const float ToneBlue = 0.75f;
    public const float ToneGreen = 0.5f;
    public const float ToneYellow = 0.25f;
    public const float RawSkin = 70f;
    public const float BurnFirst = 16f;
    public const float BurnSecond = 40f;
    public const float BurnFourth = 100f;

    public const float HandFootSever = 40f;
    public const float LimbSever = 80f;
    public const float HeadSever = 200f;
    public const float ScarSeverDiscount = 8f;
    public const float MinSever = 10f;

    public const float SmallZoneChance = 0.34f;
    public const float BareDropChance = 0.65f;
    public const float GripDropChance = 0.2f;
    public const float EmbedChance = 0.08f;
    public const float AimedEmbedChance = 0.18f;
    public const float AimedShatterChance = 0.12f;
    public const float BehindArmorFlesh = 0.35f;
    public const float BehindArmorBone = 0.6f;
    public const float CloseSeverRange = 1f;
    public const float BlastSeverFar = 5f;
    public const float BlastSeverNearChance = 0.9f;
    public const int PelletSeverCount = 4;
    public const float PelletWindow = 0.4f;
    public const float ClothingBurn = 3f;
    public const float BandageBleedFactor = 0.2f;
    public const float BleedPerFlesh = 0.02f;
    public const float TickBleed = 0.01f;
    public const float BleedLocal = 0.35f;
    public const float BleedShare = 0.1f;
    public const float BleedAtrophy = 0.5f;
    public const float StumpBleed = 12f;
    public const float CritAir = 0.4f;
    public const float ShrapnelPierce = 4f;
    public const float BlastHeat = 2f;
    public const float BlastHeatHealth = 0.25f;
    public const float BlastPierceHealth = 0.5f;
    public const float BlastBluntHealth = 0.35f;
    public const float ShrapnelBleed = 0.2f;
    public const float LivingCelsius = 37f;
    public const float CorpseDegreePerMinute = 1f;
    public const float UrgeSoon = 8f;
    public const float UrgeMissing = 4f;
    public const float UrgeVital = 2.5f;
    public const float UrgeCut = 8f;
    public const float UrgeCutStart = 0.75f;
    public const float UrgeBleed = 0.15f;
    public const float UrgeTissue = 0.02f;
    public const float OintmentDose = 15f;

    public const float PistolRicochet = 0.55f;
    public const float HeavyRicochet = 0.1f;
    public const float PelletRicochet = 0.7f;
    public const float HeavyPoint = 25f;

    public const float AimFireRate = 0.85f;
    public const float AimSpread = 0.35f;

    public const float HandAttackRate = 0.75f;
    public const float LimpSpeed = 0.75f;
    public const float MissingLegSpeed = 0.5f;
    public const float CrawlSpeed = 0.75f;
    public const float CrawlAcceleration = 6f;

    public const float FireSpreadAmount = 0.5f;
    public const float ScorchNeighbor = 0.35f;
    public const float FireEdgeDecay = 0.5f;
    public const float FireOriginDecay = 0.15f;
    public const float FireSpreadNeed = 1f;

    public static bool IsBody(LimbZone zone)
    {
        return (int) zone < BodyZoneCount;
    }

    public static bool IsSmall(LimbZone zone)
    {
        return zone is LimbZone.Eyes or LimbZone.Mouth;
    }

    public static int Index(LimbZone zone)
    {
        return (int) zone;
    }

    public static bool AimIsPrecise(bool directionalMovement, bool sprinting)
    {
        return !(directionalMovement && sprinting);
    }

    public static LimbZone ZoneFromCursor(float x, float y)
    {
        return LimbAim.Zone(x, y, LimbAim.Doll, 0f);
    }

    public static LimbMark MarkFromAmounts(float slash, float pierce, float blunt, float heat, float caustic)
    {
        var best = blunt;
        var mark = LimbMark.Blunt;

        if (slash > best)
        {
            best = slash;
            mark = LimbMark.Edge;
        }

        if (pierce > best)
        {
            best = pierce;
            mark = LimbMark.Point;
        }

        if (heat > best)
        {
            best = heat;
            mark = LimbMark.Heat;
        }

        if (caustic > best)
            mark = LimbMark.Liquid;

        return mark;
    }

    public static float BlastSeverChance(float distance)
    {
        if (distance >= BlastSeverFar)
            return 0f;

        if (distance <= CloseSeverRange)
            return BlastSeverNearChance;

        var span = BlastSeverFar - CloseSeverRange;
        return BlastSeverNearChance * (1f - (distance - CloseSeverRange) / span);
    }

    public static bool TryBlastSever(float distance, bool armored, float roll, LimbZone zone)
    {
        if (armored || !CanSever(zone) || zone == LimbZone.Head)
            return false;

        return roll < BlastSeverChance(distance);
    }

    public static bool PelletCounts(float distance, bool armored, bool pellet, bool canSever)
    {
        return pellet && canSever && !armored && distance <= CloseSeverRange;
    }

    public static bool TryPelletVolley(int hits, float distance, bool armored, bool pellet, bool canSever, LimbZone hitZone, float zoneRoll, out LimbZone zone)
    {
        zone = hitZone;
        if (!PelletCounts(distance, armored, pellet, canSever) || hits < PelletSeverCount)
            return false;

        if (hitZone is LimbZone.Torso or LimbZone.Groin or LimbZone.Eyes or LimbZone.Mouth)
            zone = Extremity(zoneRoll);

        return IsBody(zone) && CanSever(zone);
    }

    public static int BurnDegree(float skin)
    {
        if (skin >= LimbTuning.BurnFourth)
            return 4;
        if (skin >= LimbTuning.BurnThird)
            return 3;
        if (skin >= LimbTuning.BurnSecond)
            return 2;
        if (skin >= LimbTuning.BurnFirst)
            return 1;
        return 0;
    }

    public static bool Useless(in LimbZoneState state)
    {
        return state.Severed || state.Atrophied || state.Health <= 0f;
    }

    public static bool Spend(ref LimbZoneState state, float loss)
    {
        if (state.Severed || state.Atrophied || loss <= 0f)
            return false;

        state.Health = MathF.Max(0f, state.Health - loss);
        if (state.Health > 0f)
            return false;

        state.Atrophied = true;
        return true;
    }

    public static LimbZone ZoneFromHit(float x, float y)
    {
        return LimbAim.Zone(x, y, LimbAim.Shot, LimbAim.Hip);
    }

    public static bool AimCovers(LimbZone called, LimbZone natural)
    {
        return LimbAim.Covers(called, natural);
    }

    public static LimbZone Pick(bool aiming, LimbZone called, LimbZone natural)
    {
        if (aiming && AimCovers(called, natural))
            return called;

        return natural;
    }

    public static float TargetSize(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Eyes => 0.08f,
            LimbZone.Mouth => 0.12f,
            LimbZone.Head => 0.3f,
            LimbZone.Groin => 0.7f,
            LimbZone.HandLeft or LimbZone.HandRight or LimbZone.FootLeft or LimbZone.FootRight => 0.22f,
            LimbZone.ArmLeft or LimbZone.ArmRight or LimbZone.LegLeft or LimbZone.LegRight => 0.5f,
            _ => 1f,
        };
    }

    public static float SlipChance(float size, float distance, bool ready, float extra = 0f)
    {
        if (size >= 0.99f)
            return Math.Clamp((ready ? 0.08f : 0f) + extra, 0f, 0.95f);

        var gap = 1f - size;
        var close = ready ? 0.75f * gap : 0.04f * gap;
        var far = Math.Clamp(0.5f + gap * 0.55f, 0f, 0.95f);
        var range = 2.2f + size * 8f;
        var dist = Math.Clamp((distance - 0.35f) / range, 0f, 1f);
        return Math.Clamp(close + dist * (far - close) + extra, 0f, 0.95f);
    }

    public static LimbZone SlipTo(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Eyes or LimbZone.Mouth => LimbZone.Head,
            LimbZone.Head => LimbZone.Torso,
            LimbZone.Groin => LimbZone.Torso,
            LimbZone.HandLeft => LimbZone.ArmLeft,
            LimbZone.HandRight => LimbZone.ArmRight,
            LimbZone.FootLeft => LimbZone.LegLeft,
            LimbZone.FootRight => LimbZone.LegRight,
            LimbZone.Torso => LimbZone.Torso,
            _ => LimbZone.Torso,
        };
    }

    public static float HealthFor(LimbZone zone)
    {
        return zone switch
        {
            LimbZone.Head => LimbTuning.HeadHealth,
            LimbZone.Torso => LimbTuning.TorsoHealth,
            LimbZone.Groin => LimbTuning.GroinHealth,
            LimbZone.HandLeft or LimbZone.HandRight or LimbZone.FootLeft or LimbZone.FootRight => LimbTuning.ExtremityHealth,
            _ => LimbTuning.LimbHealth,
        };
    }

    public static LimbLife Life(float head, float torso)
    {
        if (head <= 0f)
            return LimbLife.Dead;

        if (torso <= 0f)
            return LimbLife.Crit;

        return LimbLife.Alive;
    }

    public static float Urgency(LimbZone zone, in LimbZoneState state)
    {
        if (state.Severed || state.Atrophied || state.Health <= 0f)
            return 0f;

        var pool = HealthFor(zone);
        var missing = pool <= 0f ? 1f : 1f - state.Health / pool;
        var vital = zone is LimbZone.Head or LimbZone.Torso ? UrgeVital : 1f;
        var local = state.Bleed * BleedLocal;
        var soon = local > TickBleed ? Math.Clamp(UrgeSoon * local / state.Health, 0f, UrgeSoon) : 0f;
        var cap = SeverThreshold(zone, state.Scar);
        var cutting = cap > 0f ? state.Sever / cap : 0f;
        var cut = cutting >= UrgeCutStart ? UrgeCut * cutting : 0f;
        var wound = state.Bleed * UrgeBleed + (state.Flesh + state.Bone + state.Skin) * UrgeTissue;
        return soon + missing * vital * UrgeMissing + cut + wound * vital;
    }

    public static int TreatIndex(ReadOnlySpan<LimbZoneState> zones, int selected)
    {
        if ((uint) selected < (uint) zones.Length && Urgency((LimbZone) selected, zones[selected]) > 0f)
            return selected;

        return PickTreat(zones);
    }

    public static void SpreadOne(Span<float> values, float delta)
    {
        if (delta >= 0f || values.Length == 0)
            return;

        var open = 0;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > 0f)
                open++;
        }

        if (open == 0)
            return;

        var share = delta / open;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] <= 0f)
                continue;

            values[i] = MathF.Max(0f, values[i] + share);
        }
    }

    public const float BulletFullRange = 4f;
    public const float BulletFarRange = 14f;
    public const float BulletFarKeep = 0.82f;

    public static float BulletDistance(float distance)
    {
        if (distance <= BulletFullRange)
            return 1f;

        if (distance >= BulletFarRange)
            return BulletFarKeep;

        var span = BulletFarRange - BulletFullRange;
        var t = (distance - BulletFullRange) / span;
        return 1f - t * (1f - BulletFarKeep);
    }

    public static int PickTreat(ReadOnlySpan<LimbZoneState> zones)
    {
        var best = -1;
        var score = 0f;
        var count = Math.Min(zones.Length, BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            var value = Urgency((LimbZone) i, zones[i]);
            if (value <= score)
                continue;

            score = value;
            best = i;
        }

        return best;
    }

    public static void Mend(ref LimbZoneState state, float amount, float cap)
    {
        if (amount <= 0f || state.Severed || state.Atrophied)
            return;

        state.Health = MathF.Min(cap, state.Health + amount);
    }

    public static bool DrainBleed(Span<LimbZoneState> zones)
    {
        var count = Math.Min(zones.Length, BodyZoneCount);
        var any = false;
        var overflow = 0f;
        for (var i = 0; i < count; i++)
        {
            ref var state = ref zones[i];
            if (state.Bleed <= TickBleed)
                continue;

            any = true;
            if (state.Severed || state.Atrophied || state.Health <= 0f)
            {
                if (!state.Severed)
                    state.Atrophied = true;
                overflow += state.Bleed * BleedLocal;
                continue;
            }

            state.Health = MathF.Max(0f, state.Health - state.Bleed * BleedLocal);
            if (state.Health <= 0f)
                state.Atrophied = true;
        }

        if (overflow <= TickBleed || (uint) Index(LimbZone.Torso) >= (uint) zones.Length)
            return any;

        ref var chest = ref zones[Index(LimbZone.Torso)];
        var half = HealthFor(LimbZone.Torso) * 0.5f;
        if (!chest.Severed && chest.Health > half)
        {
            chest.Health = MathF.Max(0f, chest.Health - overflow);
            if (chest.Health <= 0f)
                chest.Atrophied = true;
            return true;
        }

        var living = 0;
        for (var i = 0; i < count; i++)
        {
            if (!zones[i].Severed && zones[i].Health > 0f)
                living++;
        }

        if (living == 0)
            return any;

        var share = overflow / living;
        for (var i = 0; i < count; i++)
        {
            if (zones[i].Severed || zones[i].Health <= 0f)
                continue;

            zones[i].Health = MathF.Max(0f, zones[i].Health - share);
            if (zones[i].Health <= 0f)
                zones[i].Atrophied = true;
        }

        return true;
    }

    public static void MarkSevered(ref LimbZoneState state)
    {
        state.Severed = true;
        state.Scar = 0;
        state.Bleed = state.Bandaged ? 0f : MathF.Max(state.Bleed, StumpBleed);
    }

    public static float CorpseKelvin(float minutes)
    {
        var celsius = MathF.Max(0f, LivingCelsius - minutes * CorpseDegreePerMinute);
        return Atmospherics.T0C + celsius;
    }

    public readonly record struct LimbWork(LimbZone Zone, float Seconds);

    public static readonly (LimbZone Parent, LimbZone Child)[] Carries =
    {
        (LimbZone.ArmLeft, LimbZone.HandLeft),
        (LimbZone.ArmRight, LimbZone.HandRight),
        (LimbZone.LegLeft, LimbZone.FootLeft),
        (LimbZone.LegRight, LimbZone.FootRight),
    };

    public static bool TryCarried(LimbZone zone, out LimbZone child)
    {
        foreach (var row in Carries)
        {
            if (row.Parent != zone)
                continue;

            child = row.Child;
            return true;
        }

        child = default;
        return false;
    }

    public static readonly LimbWork[] Harvest =
    {
        new(LimbZone.Head, 14f),
        new(LimbZone.Torso, 10f),
        new(LimbZone.ArmLeft, 7f),
        new(LimbZone.ArmRight, 7f),
        new(LimbZone.LegLeft, 7f),
        new(LimbZone.LegRight, 7f),
        new(LimbZone.HandLeft, 4f),
        new(LimbZone.HandRight, 4f),
        new(LimbZone.FootLeft, 4f),
        new(LimbZone.FootRight, 4f),
        new(LimbZone.Eyes, 5f),
        new(LimbZone.Mouth, 5f),
    };

    public static bool ApplyBlast(ref LimbZoneState state, float heat, float pierce, float blunt, float slash)
    {
        if (state.Severed || heat <= 0f && pierce <= 0f && blunt <= 0f && slash <= 0f)
            return false;

        if (heat > 0f)
        {
            state.Heat += heat;
            state.Skin += heat * BlastHeat;
            state.Health = MathF.Max(0f, state.Health - heat * BlastHeatHealth);
        }

        if (pierce >= ShrapnelPierce)
        {
            state.Pierce += pierce;
            state.Embedded = true;
            state.Flesh += pierce;
            state.Bleed += pierce * ShrapnelBleed;
            state.Health = MathF.Max(0f, state.Health - pierce * BlastPierceHealth);
        }
        else if (pierce > 0f)
        {
            state.Pierce += pierce;
            state.Flesh += pierce * 0.5f;
            state.Health = MathF.Max(0f, state.Health - pierce * BlastPierceHealth);
        }

        if (blunt > 0f)
        {
            state.Bone += blunt;
            state.Health = MathF.Max(0f, state.Health - blunt * BlastBluntHealth);
        }

        if (slash > 0f)
        {
            state.Cut += slash;
            state.Flesh += slash;
            state.Bleed += slash * BleedPerFlesh;
            state.Health = MathF.Max(0f, state.Health - slash * BlastPierceHealth);
        }

        if (state.Health <= 0f)
            state.Atrophied = true;

        if (StripsSkin(false, 0f, state.Skin))
            state.SkinBare = true;

        return true;
    }

    public static float HarvestSeconds(LimbZone zone, float cut)
    {
        var seconds = 6f;
        foreach (var row in Harvest)
        {
            if (row.Zone != zone)
                continue;

            seconds = row.Seconds;
            break;
        }

        var tool = cut <= 0f ? 1f : cut;
        return MathF.Max(1.5f, seconds / tool);
    }

    public static bool KeepsBodyDamage(LimbZone zone)
    {
        return zone is LimbZone.Head or LimbZone.Torso or LimbZone.Groin or LimbZone.Eyes or LimbZone.Mouth;
    }

    public static bool CanGrasp(in LimbZoneState armLeft, in LimbZoneState handLeft, in LimbZoneState armRight, in LimbZoneState handRight)
    {
        return SideGrasps(armLeft, handLeft) || SideGrasps(armRight, handRight);
    }

    public static float BurnPain(float skin)
    {
        var degree = BurnDegree(skin);
        if (degree >= 4)
            return 0.2f;
        if (degree >= 3)
            return 0.12f;
        if (degree >= 2)
            return 0.04f;
        return 0f;
    }

    public static int Vision(int eyesGone, float eyeBurn)
    {
        if (eyesGone >= 2)
            return 9;

        var fromGone = eyesGone >= 1 ? 4 : 0;
        var fromBurn = BurnDegree(eyeBurn) switch
        {
            0 => 0,
            1 => 1,
            2 => 3,
            3 => 6,
            _ => 8,
        };
        return Math.Min(9, fromGone + fromBurn);
    }

    public static string Garble(string message, float trauma)
    {
        if (trauma < 8f || message.Length == 0)
            return message;

        var heavy = trauma >= 30f;
        var step = heavy ? 2 : 4;
        var built = new System.Text.StringBuilder(message.Length + 8);
        for (var i = 0; i < message.Length; i++)
        {
            var letter = message[i];
            if (char.IsLetter(letter) && i % step == 0)
                built.Append(heavy ? '-' : letter);

            built.Append(letter);
        }

        return built.ToString();
    }

    private static bool SideGrasps(in LimbZoneState arm, in LimbZoneState hand)
    {
        return !arm.Severed && !hand.Severed
               && !arm.Embedded && !hand.Embedded
               && BurnDegree(arm.Skin) < 3 && BurnDegree(hand.Skin) < 3;
    }

    public static float RibBreath(float bone, float flesh)
    {
        if (bone >= BreakBone && flesh >= StrainFlesh)
            return 1.4f;

        if (bone >= BreakBone)
            return 0.9f;

        if (bone >= StrainBone)
            return 0.35f;

        return 0f;
    }

    public static bool Awake(ReadOnlySpan<LimbZoneState> zones, ReadOnlySpan<LimbOrganState> organs, bool cryo)
    {
        var bleed = 0f;
        var pain = 0f;
        var cryoWork = false;
        var count = Math.Min(zones.Length, BodyZoneCount);
        for (var i = 0; i < count; i++)
        {
            ref readonly var zone = ref zones[i];
            if (zone.Fire > 0f)
                return true;

            bleed += zone.Bleed;
            pain += BurnPain(zone.Skin);
            if (cryo && (zone.Flesh >= StrainFlesh || zone.Bone >= StrainBone || zone.Skin > 0f))
                cryoWork = true;
        }

        if (bleed > TickBleed || pain > 0f)
            return true;

        var torso = Index(LimbZone.Torso);
        if (torso < zones.Length && RibBreath(zones[torso].Bone, zones[torso].Flesh) > 0f)
            return true;

        if (organs.Length > 0)
        {
            var failure = LimbOrganRules.Tick(organs);
            if (failure.Bloodloss > 0f || failure.Asphyxiation > 0f || failure.Poison > 0f)
                return true;

            if (cryo)
            {
                for (var i = 0; i < organs.Length; i++)
                {
                    if (!organs[i].Ruptured && organs[i].Damage > 0f)
                        return true;
                }
            }
        }

        return cryoWork;
    }

    public static LimbZone Extremity(float roll)
    {
        var index = (int) (Math.Clamp(roll, 0f, 0.999f) * 8f);
        return index switch
        {
            0 => LimbZone.HandLeft,
            1 => LimbZone.HandRight,
            2 => LimbZone.ArmLeft,
            3 => LimbZone.ArmRight,
            4 => LimbZone.LegLeft,
            5 => LimbZone.LegRight,
            6 => LimbZone.FootLeft,
            _ => LimbZone.FootRight,
        };
    }

    public static bool StripsSkin(bool pellet, float distance, float skin, bool blast = false)
    {
        if (skin >= LimbTuning.BurnThird || blast)
            return true;

        return pellet && distance <= CloseSeverRange;
    }

    public static bool BlastStrips(float damage, float wound)
    {
        return damage * wound >= RawSkin;
    }

    public static bool IsHeavyPoint(float pierce)
    {
        return pierce >= HeavyPoint;
    }

    public static bool HeavyLimb(LimbZone zone)
    {
        return zone is LimbZone.ArmLeft or LimbZone.ArmRight or LimbZone.LegLeft or LimbZone.LegRight;
    }

    public static float SeverGain(LimbZone zone, float already, int scar, float amount, float cut, float roll, float chance)
    {
        var gain = amount * (cut <= 0f ? 1f : cut);
        var threshold = SeverThreshold(zone, scar);
        if (!HeavyLimb(zone) || already + gain < threshold || roll < chance)
            return gain;

        return MathF.Max(0f, threshold - already - 0.01f);
    }

    public static float SeverThreshold(LimbZone zone, int scar)
    {
        var threshold = zone switch
        {
            LimbZone.HandLeft or LimbZone.HandRight or LimbZone.FootLeft or LimbZone.FootRight => LimbTuning.SeverHand,
            LimbZone.Head => LimbTuning.SeverHead,
            LimbZone.Torso or LimbZone.Groin => float.PositiveInfinity,
            _ => LimbTuning.SeverLimb,
        };

        return MathF.Max(MinSever, threshold - scar * ScarSeverDiscount);
    }

    public static bool CanSever(LimbZone zone)
    {
        return !float.IsPositiveInfinity(SeverThreshold(zone, 0));
    }

    public static int Tone(float health, float pool)
    {
        if (pool <= 0f || health <= 0f)
            return -1;

        var left = health / pool;
        if (left > ToneBlue)
            return 0;

        if (left > ToneGreen)
            return 1;

        if (left > ToneYellow)
            return 2;

        return 4;
    }

    public static LimbStrikeResult Apply(ref LimbZoneState state, LimbStrike strike)
    {
        if (strike.ShieldBlocked && strike.Projectile && TryRicochet(strike))
            return LimbStrikeResult.Glanced();

        if (strike.ShieldBlocked)
            return LimbStrikeResult.Stopped();

        if (strike.Projectile && TryRicochet(strike))
            return LimbStrikeResult.Glanced();

        if (Blocked(strike))
            return LimbStrikeResult.Stopped();

        if (IsSmall(strike.Zone))
        {
            if (strike.Roll > SmallZoneChance)
                return LimbStrikeResult.Miss();

            if (strike.Zone == LimbZone.Eyes && strike.Mark is LimbMark.Edge or LimbMark.Point)
                return LimbStrikeResult.Eyes(1);

            if (strike.Zone == LimbZone.Eyes && strike.Mark == LimbMark.Blunt)
                return new LimbStrikeResult(true, false, false, false, false, 0, false, 0f);

            if (strike.Zone == LimbZone.Mouth && strike.Mark == LimbMark.Liquid)
                return LimbStrikeResult.Swallowed();

            if (strike.Zone == LimbZone.Mouth && strike.Mark is LimbMark.Blunt or LimbMark.Edge or LimbMark.Point)
                return new LimbStrikeResult(true, false, false, false, false, 0, false, 0f);

            if (strike.Mark is LimbMark.Heat or LimbMark.Liquid)
                return new LimbStrikeResult(true, false, false, false, false, 0, false, 0f);

            return LimbStrikeResult.Miss();
        }

        if (!IsBody(strike.Zone) || state.Severed)
            return LimbStrikeResult.Miss();

        var amount = strike.Amount * strike.WoundMultiplier;
        if (amount <= 0f)
            return LimbStrikeResult.Miss();

        if (strike.Mark == LimbMark.Blunt)
            state.Splinted = false;

        switch (strike.Mark)
        {
            case LimbMark.Edge:
                state.Cut += amount;
                state.Flesh += amount;
                if (strike.Sharp)
                    state.Sever += SeverGain(strike.Zone, state.Sever, state.Scar, amount, strike.Cut, strike.Roll, strike.SeverChance);
                break;
            case LimbMark.Point:
                state.Pierce += amount;
                if (strike.BehindArmor && strike.Amount >= HeavyPoint)
                {
                    state.Flesh += amount * BehindArmorFlesh;
                    state.Bone += strike.Amount * BehindArmorBone;
                    break;
                }

                state.Flesh += amount;
                if (strike.Projectile && strike.Roll < (strike.Aimed ? AimedEmbedChance : EmbedChance))
                    state.Embedded = true;
                if (strike.Projectile && strike.Aimed && strike.SecondaryRoll < AimedShatterChance)
                    state.Bone += amount;
                break;
            case LimbMark.Blunt:
                state.Bone += amount;
                break;
            case LimbMark.Heat:
                state.Heat += amount;
                state.Flesh += amount * 0.25f;
                state.Skin += amount;
                break;
            case LimbMark.Liquid:
                state.Caustic += amount;
                state.Skin += amount;
                if (strike.LiquidSever > 0f)
                    state.Sever += amount * strike.LiquidSever;
                break;
        }

        if (StripsSkin(strike.Pellet, strike.Distance, state.Skin, strike.Blast))
            state.SkinBare = true;

        if (strike.RoundBone > 0f)
            state.Bone += strike.RoundBone * strike.WoundMultiplier;

        var bleedAdded = 0f;
        if (strike.RoundBleed > 0f)
        {
            var internalBleed = strike.RoundBleed * strike.WoundMultiplier;
            if (state.Bandaged)
                internalBleed *= BandageBleedFactor;

            state.Bleed += internalBleed;
            bleedAdded += internalBleed;
        }
        if (state.Flesh >= StrainFlesh && strike.Mark is LimbMark.Edge or LimbMark.Point)
        {
            var open = amount * BleedPerFlesh;
            if (state.Bandaged)
                open *= BandageBleedFactor;

            if (state.Embedded)
                open *= 2f;

            state.Bleed += open;
            bleedAdded += open;
        }

        var drop = strike.Mark == LimbMark.Blunt
                   && strike.Zone is LimbZone.HandLeft or LimbZone.HandRight
                   && strike.Roll < (strike.Grip ? GripDropChance : BareDropChance);

        var severed = false;
        if (strike.Sharp && strike.Mark == LimbMark.Edge && state.Sever >= SeverThreshold(strike.Zone, state.Scar))
        {
            MarkSevered(ref state);
            severed = true;
        }

        return new LimbStrikeResult(true, false, false, severed, drop, 0, false, bleedAdded);
    }

    public static void Bandage(ref LimbZoneState state)
    {
        state.Bandaged = true;
        state.Bleed = state.Severed ? 0f : state.Bleed * BandageBleedFactor;
    }

    public static void Splint(ref LimbZoneState state)
    {
        if (state.Bone >= StrainBone)
            state.Splinted = true;
    }

    public static void Ointment(ref LimbZoneState state, float amount)
    {
        state.Skin = MathF.Max(0f, state.Skin - amount);
    }

    public static void Cryo(ref LimbZoneState state)
    {
        var scarred = false;
        if (state.Flesh >= BreakFlesh)
        {
            state.Flesh = StrainFlesh;
            scarred = true;
        }
        else if (state.Flesh >= StrainFlesh)
        {
            state.Flesh = 0f;
        }

        if (state.Bone >= BreakBone)
        {
            state.Bone = StrainBone;
            scarred = true;
        }
        else if (state.Bone >= StrainBone)
        {
            state.Bone = 0f;
        }

        if (state.Skin >= RawSkin)
            state.Skin = RawSkin * 0.5f;
        else
            state.Skin = MathF.Max(0f, state.Skin - 20f);

        if (scarred)
            state.Scar++;
    }

    public static bool Limp(in LimbZoneState state, bool adrenaline)
    {
        if (state.Severed)
            return false;

        if (state.Embedded)
            return true;

        return !adrenaline && !state.Splinted && state.Bone >= BreakBone;
    }

    public static bool SlowHands(in LimbZoneState arm, in LimbZoneState hand, bool adrenaline)
    {
        if (arm.Embedded || hand.Embedded)
            return true;

        if (adrenaline)
            return false;

        return arm.Bone >= BreakBone || hand.Bone >= BreakBone;
    }

    public static float MoveSpeed(bool limp, int legsMissing)
    {
        var speed = 1f;
        if (limp)
            speed *= LimbTuning.LimpSpeed;

        for (var i = 0; i < legsMissing; i++)
            speed *= LimbTuning.MissingLegSpeed;

        return speed;
    }

    public static void Ignite(Span<LimbZoneState> zones, LimbZone origin, float amount)
    {
        if (!IsBody(origin))
            origin = LimbZone.Torso;

        zones[Index(origin)].Fire += amount;
    }

    public static void IgniteFloor(Span<LimbZoneState> zones, float amount)
    {
        AddFire(zones, LimbZone.FootLeft, amount);
        AddFire(zones, LimbZone.FootRight, amount);
        AddFire(zones, LimbZone.Groin, amount * 0.5f);
        if (Hottest(zones) >= 0)
            return;

        AddFire(zones, LimbZone.LegLeft, amount);
        AddFire(zones, LimbZone.LegRight, amount);
        if (Hottest(zones) >= 0)
            return;

        Ignite(zones, LimbZone.Torso, amount);
    }

    private static void AddFire(Span<LimbZoneState> zones, LimbZone zone, float amount)
    {
        var index = Index(zone);
        if ((uint) index >= (uint) zones.Length || zones[index].Severed || amount <= 0f)
            return;

        zones[index].Fire += amount;
    }

    public static void Scorch(Span<LimbZoneState> zones, float heat)
    {
        if (heat <= 0f)
            return;

        var count = Math.Min(zones.Length, BodyZoneCount);
        var hottest = 0f;
        var burning = 0;
        for (var i = 0; i < count; i++)
        {
            if (zones[i].Severed || zones[i].Fire <= 0f)
                continue;

            burning++;
            if (zones[i].Fire > hottest)
                hottest = zones[i].Fire;
        }

        if (burning == 0 || hottest <= 0f)
            return;

        var share = heat / burning;
        for (var i = 0; i < count; i++)
        {
            if (zones[i].Severed || zones[i].Fire <= 0f)
                continue;

            var gain = zones[i].Fire >= hottest - 0.001f ? heat : heat * ScorchNeighbor;
            zones[i].Skin += gain;
            zones[i].HitHeat += share;
            if (zones[i].Skin >= LimbTuning.BurnThird)
                zones[i].SkinBare = true;
        }
    }

    public static void SpreadFire(Span<LimbZoneState> zones, bool[] blocked)
    {
        var origin = Hottest(zones);
        for (var i = 0; i < BodyZoneCount; i++)
        {
            if (zones[i].Fire <= 0f)
                continue;

            var decay = i == origin ? FireOriginDecay : FireEdgeDecay;
            zones[i].Fire = MathF.Max(0f, zones[i].Fire - decay);
        }

        origin = Hottest(zones);
        if (origin < 0 || zones[origin].Fire < FireSpreadNeed)
            return;

        foreach (var next in Neighbors((LimbZone) origin))
        {
            var index = Index(next);
            if (blocked[index] || zones[index].Fire >= zones[origin].Fire)
                continue;

            zones[origin].Fire -= LimbTuning.FireSpread;
            zones[index].Fire += LimbTuning.FireSpread;
            return;
        }
    }

    public static int Hottest(Span<LimbZoneState> zones)
    {
        var best = -1;
        var heat = 0f;
        for (var i = 0; i < BodyZoneCount; i++)
        {
            if (zones[i].Fire > heat)
            {
                heat = zones[i].Fire;
                best = i;
            }
        }

        return best;
    }

    public static IEnumerable<LimbZone> Neighbors(LimbZone zone)
    {
        switch (zone)
        {
            case LimbZone.FootLeft:
                yield return LimbZone.LegLeft;
                yield break;
            case LimbZone.FootRight:
                yield return LimbZone.LegRight;
                yield break;
            case LimbZone.LegLeft:
            case LimbZone.LegRight:
            case LimbZone.Head:
                yield return LimbZone.Torso;
                yield break;
            case LimbZone.ArmLeft:
                yield return LimbZone.HandLeft;
                yield break;
            case LimbZone.ArmRight:
                yield return LimbZone.HandRight;
                yield break;
            case LimbZone.Groin:
                yield return LimbZone.Torso;
                yield break;
            case LimbZone.Torso:
                yield return LimbZone.Head;
                yield return LimbZone.ArmLeft;
                yield return LimbZone.ArmRight;
                yield return LimbZone.LegLeft;
                yield return LimbZone.LegRight;
                yield return LimbZone.Groin;
                yield break;
        }
    }

    private static bool TryRicochet(LimbStrike strike)
    {
        if (!strike.Ricochet || strike.Mark != LimbMark.Point)
            return false;

        var chance = strike.Pellet ? PelletRicochet
            : IsHeavyPoint(strike.Pierce) ? HeavyRicochet
            : PistolRicochet;

        return strike.Roll < chance;
    }

    private static bool Blocked(LimbStrike strike)
    {
        if (!strike.FaceCovered)
            return false;

        if (strike.Zone is LimbZone.Eyes or LimbZone.Mouth)
            return true;

        return strike.Mark == LimbMark.Liquid && strike.Zone == LimbZone.Head;
    }
}

public readonly record struct LimbStrike(
    LimbZone Zone,
    LimbMark Mark,
    float Amount,
    float Roll,
    float Pierce,
    bool Projectile,
    bool FaceCovered,
    bool Grip,
    bool Ricochet,
    bool ShieldBlocked,
    float WoundMultiplier,
    bool Aimed = false,
    bool BehindArmor = false,
    float SecondaryRoll = 1f,
    float Distance = 0f,
    float SeverRoll = 1f,
    float Cut = 1f,
    float LiquidSever = 0f,
    bool Blast = false,
    bool ThroughArmor = false,
    LimbZone Called = LimbZone.Torso,
    bool Sharp = false,
    bool Pellet = false,
    bool PelletSever = false,
    float RoundBone = 0f,
    float RoundOrgan = 1f,
    float RoundBleed = 0f,
    float SeverChance = 1f,
    float Harm = 0f);

public readonly record struct LimbStrikeResult(
    bool Applied,
    bool Ricochet,
    bool Blocked,
    bool Severed,
    bool DropItem,
    int EyeDamage,
    bool Ingest,
    float BleedAdded)
{
    public static LimbStrikeResult Glanced()
    {
        return new LimbStrikeResult(false, true, false, false, false, 0, false, 0f);
    }

    public static LimbStrikeResult Stopped()
    {
        return new LimbStrikeResult(false, false, true, false, false, 0, false, 0f);
    }

    public static LimbStrikeResult Miss()
    {
        return new LimbStrikeResult(false, false, false, false, false, 0, false, 0f);
    }

    public static LimbStrikeResult Eyes(int damage)
    {
        return new LimbStrikeResult(true, false, false, false, false, damage, false, 0f);
    }

    public static LimbStrikeResult Swallowed()
    {
        return new LimbStrikeResult(true, false, false, false, false, 0, true, 0f);
    }
}
