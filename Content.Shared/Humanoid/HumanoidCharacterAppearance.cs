using System.Linq;
using System.Numerics;
using Content.Shared.DeadSpace.Humanoid.Markings; // DS14
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class HumanoidCharacterAppearance : ICharacterAppearance, IEquatable<HumanoidCharacterAppearance>
{
    [DataField("hair")]
    public string HairStyleId { get; set; } = HairStyles.DefaultHairStyle;

    [DataField]
    public Color HairColor { get; set; } = Color.Black;

    // DS14-start
    [DataField]
    public MarkingDye? HairDye { get; set; }

    [DataField]
    public MarkingDye? BodyDye { get; set; }
    // DS14-end

    [DataField("facialHair")]
    public string FacialHairStyleId { get; set; } = HairStyles.DefaultFacialHairStyle;

    [DataField]
    public Color FacialHairColor { get; set; } = Color.Black;

    // DS14-start
    [DataField]
    public MarkingDye? FacialHairDye { get; set; }
    // DS14-end

    [DataField]
    public Color EyeColor { get; set; } = Color.Black;

    [DataField]
    public Color SkinColor { get; set; } = Color.FromHsv(new Vector4(0.07f, 0.2f, 1f, 1f));

    [DataField]
    public List<Marking> Markings { get; set; } = new();

    public HumanoidCharacterAppearance(string hairStyleId,
        Color hairColor,
        string facialHairStyleId,
        Color facialHairColor,
        Color eyeColor,
        Color skinColor,
        List<Marking> markings)
    {
        HairStyleId = hairStyleId;
        HairColor = ClampColor(hairColor);
        FacialHairStyleId = facialHairStyleId;
        FacialHairColor = ClampColor(facialHairColor);
        EyeColor = ClampColor(eyeColor);
        SkinColor = ClampColor(skinColor);
        Markings = markings;
    }

    public HumanoidCharacterAppearance(HumanoidCharacterAppearance other) :
        this(other.HairStyleId, other.HairColor, other.FacialHairStyleId, other.FacialHairColor, other.EyeColor, other.SkinColor, new(other.Markings))
    {
        // DS14-start
        HairDye = other.HairDye;
        FacialHairDye = other.FacialHairDye;
        BodyDye = other.BodyDye;
        // DS14-end
    }

    // DS14-start
    private HumanoidCharacterAppearance KeepDyes(HumanoidCharacterAppearance appearance)
    {
        appearance.HairDye = HairDye;
        appearance.FacialHairDye = FacialHairDye;
        appearance.BodyDye = BodyDye;
        return appearance;
    }
    // DS14-end

    public HumanoidCharacterAppearance WithHairStyleName(string newName)
    {
        return KeepDyes(new(newName, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings)); // DS14
    }

    public HumanoidCharacterAppearance WithHairColor(Color newColor)
    {
        return KeepDyes(new(HairStyleId, newColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings)); // DS14
    }

    // DS14-start
    public HumanoidCharacterAppearance WithHairDye(MarkingDye? dye)
    {
        return new(this) { HairDye = dye };
    }

    public HumanoidCharacterAppearance WithFacialHairDye(MarkingDye? dye)
    {
        return new(this) { FacialHairDye = dye };
    }

    public HumanoidCharacterAppearance WithBodyDye(MarkingDye? dye)
    {
        return new(this) { BodyDye = dye };
    }
    // DS14-end

    public HumanoidCharacterAppearance WithFacialHairStyleName(string newName)
    {
        return KeepDyes(new(HairStyleId, HairColor, newName, FacialHairColor, EyeColor, SkinColor, Markings)); // DS14
    }

    public HumanoidCharacterAppearance WithFacialHairColor(Color newColor)
    {
        return KeepDyes(new(HairStyleId, HairColor, FacialHairStyleId, newColor, EyeColor, SkinColor, Markings)); // DS14
    }

    public HumanoidCharacterAppearance WithEyeColor(Color newColor)
    {
        return KeepDyes(new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, newColor, SkinColor, Markings)); // DS14
    }

    public HumanoidCharacterAppearance WithSkinColor(Color newColor)
    {
        return KeepDyes(new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, newColor, Markings)); // DS14
    }

    public HumanoidCharacterAppearance WithMarkings(List<Marking> newMarkings)
    {
        return KeepDyes(new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, newMarkings)); // DS14
    }

    public static HumanoidCharacterAppearance DefaultWithSpecies(string species)
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var speciesPrototype = protoMan.Index<SpeciesPrototype>(species);
        var skinColoration = protoMan.Index(speciesPrototype.SkinColoration).Strategy;
        var skinColor = skinColoration.InputType switch
        {
            SkinColorationStrategyInput.Unary => skinColoration.FromUnary(speciesPrototype.DefaultHumanSkinTone),
            SkinColorationStrategyInput.Color => skinColoration.ClosestSkinColor(speciesPrototype.DefaultSkinTone),
            _ => skinColoration.ClosestSkinColor(speciesPrototype.DefaultSkinTone),
        };

        return new(
            HairStyles.DefaultHairStyle,
            Color.Black,
            HairStyles.DefaultFacialHairStyle,
            Color.Black,
            Color.Black,
            skinColor,
            new()
        );
    }

    private static IReadOnlyList<Color> _realisticEyeColors = new List<Color>
    {
        Color.Brown,
        Color.Gray,
        Color.Azure,
        Color.SteelBlue,
        Color.Black
    };

    public static HumanoidCharacterAppearance Random(string species, Sex sex)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var markingManager = IoCManager.Resolve<MarkingManager>();
        var hairStyles = markingManager.MarkingsByCategoryAndSpecies(MarkingCategories.Hair, species).Keys.ToList();
        var facialHairStyles = markingManager.MarkingsByCategoryAndSpecies(MarkingCategories.FacialHair, species).Keys.ToList();

        var newHairStyle = hairStyles.Count > 0
            ? random.Pick(hairStyles)
            : HairStyles.DefaultHairStyle.Id;

        var newFacialHairStyle = facialHairStyles.Count == 0 || sex == Sex.Female
            ? HairStyles.DefaultFacialHairStyle.Id
            : random.Pick(facialHairStyles);

        var newHairColor = random.Pick(HairStyles.RealisticHairColors);
        newHairColor = newHairColor
            .WithRed(RandomizeColor(newHairColor.R))
            .WithGreen(RandomizeColor(newHairColor.G))
            .WithBlue(RandomizeColor(newHairColor.B));

        // TODO: Add random markings

        var newEyeColor = random.Pick(_realisticEyeColors);

        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var skinType = protoMan.Index<SpeciesPrototype>(species).SkinColoration;
        var strategy = protoMan.Index(skinType).Strategy;

        var newSkinColor = strategy.InputType switch
        {
            SkinColorationStrategyInput.Unary => strategy.FromUnary(random.NextFloat(0f, 100f)),
            SkinColorationStrategyInput.Color => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
            _ => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
        };

        return new HumanoidCharacterAppearance(newHairStyle, newHairColor, newFacialHairStyle, newHairColor, newEyeColor, newSkinColor, new() // DS14
        ); // DS14

        float RandomizeColor(float channel)
        {
            return MathHelper.Clamp01(channel + random.Next(-25, 25) / 100f);
        }
    }

    public static Color ClampColor(Color color)
    {
        return new(color.RByte, color.GByte, color.BByte);
    }

    // DS14-sponsors-start: Added sponsorMarkings parameter
    public static HumanoidCharacterAppearance EnsureValid(HumanoidCharacterAppearance appearance, string species, Sex sex, string[] sponsorMarkings)
    {
        var hairStyleId = appearance.HairStyleId;
        var facialHairStyleId = appearance.FacialHairStyleId;

        var hairColor = ClampColor(appearance.HairColor);
        var facialHairColor = ClampColor(appearance.FacialHairColor);
        var eyeColor = ClampColor(appearance.EyeColor);

        var proto = IoCManager.Resolve<IPrototypeManager>();
        var markingManager = IoCManager.Resolve<MarkingManager>();

        // DS14-start
        if (!markingManager.MarkingsByCategory(MarkingCategories.Hair).ContainsKey(hairStyleId))
        {
            hairStyleId = HairStyles.DefaultHairStyle;
        }

        if (proto.TryIndex(hairStyleId, out MarkingPrototype? hairProto) &&
            hairProto.SponsorOnly &&
            !sponsorMarkings.Contains(hairStyleId))
        {
            hairStyleId = HairStyles.DefaultHairStyle;
        }
        // DS14-end

        if (!markingManager.MarkingsByCategory(MarkingCategories.FacialHair).ContainsKey(facialHairStyleId))
        {
            facialHairStyleId = HairStyles.DefaultFacialHairStyle;
        }

        // DS14-sponsors-start
        if (proto.TryIndex(facialHairStyleId, out MarkingPrototype? facialHairProto) &&
            facialHairProto.SponsorOnly &&
            !sponsorMarkings.Contains(facialHairStyleId))
        {
            facialHairStyleId = HairStyles.DefaultFacialHairStyle;
        }
        // DS14-sponsors-end

        var markingSet = new MarkingSet();
        var skinColor = appearance.SkinColor;
        if (proto.TryIndex(species, out SpeciesPrototype? speciesProto))
        {
            markingSet = new MarkingSet(appearance.Markings, speciesProto.MarkingPoints, markingManager, proto);
            markingSet.EnsureValid(markingManager);
            markingSet.FilterSponsor(sponsorMarkings, markingManager); // DS14-sponsors

            var strategy = proto.Index(speciesProto.SkinColoration).Strategy;
            skinColor = strategy.EnsureVerified(skinColor);

            markingSet.EnsureSpecies(species, skinColor, markingManager);
            markingSet.FilterSponsor(sponsorMarkings, markingManager); // DS14-sponsors
            markingSet.EnsureSexes(sex, markingManager);
        }

        return new HumanoidCharacterAppearance(
            hairStyleId,
            hairColor,
            facialHairStyleId,
            facialHairColor,
            eyeColor,
            skinColor,
            markingSet.GetForwardEnumerator().ToList())
        {
            // DS14-start
            HairDye = appearance.HairDye?.Cleaned(),
            BodyDye = appearance.BodyDye?.Cleaned(),
            FacialHairDye = appearance.FacialHairDye?.Cleaned(),
            // DS14-end
        };
    }
    // DS14-sponsors-end

    public bool MemberwiseEquals(ICharacterAppearance maybeOther)
    {
        if (maybeOther is not HumanoidCharacterAppearance other) return false;
        if (HairStyleId != other.HairStyleId) return false;
        if (!HairColor.Equals(other.HairColor)) return false;
        // DS14-start
        if (!Nullable.Equals(HairDye, other.HairDye)) return false;
        if (!Nullable.Equals(FacialHairDye, other.FacialHairDye)) return false;
        if (!Nullable.Equals(BodyDye, other.BodyDye)) return false;
        // DS14-end
        if (FacialHairStyleId != other.FacialHairStyleId) return false;
        if (!FacialHairColor.Equals(other.FacialHairColor)) return false;
        if (!EyeColor.Equals(other.EyeColor)) return false;
        if (!SkinColor.Equals(other.SkinColor)) return false;
        if (!Markings.SequenceEqual(other.Markings)) return false;
        return true;
    }

    public bool Equals(HumanoidCharacterAppearance? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return HairStyleId == other.HairStyleId &&
               HairColor.Equals(other.HairColor) &&
               // DS14-start
               Nullable.Equals(HairDye, other.HairDye) &&
               Nullable.Equals(FacialHairDye, other.FacialHairDye) &&
               Nullable.Equals(BodyDye, other.BodyDye) &&
               // DS14-end
               FacialHairStyleId == other.FacialHairStyleId &&
               FacialHairColor.Equals(other.FacialHairColor) &&
               EyeColor.Equals(other.EyeColor) &&
               SkinColor.Equals(other.SkinColor) &&
               Markings.SequenceEqual(other.Markings);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is HumanoidCharacterAppearance other && Equals(other);
    }

    public override int GetHashCode()
    {
        // DS14-Start
        var hash = new HashCode();
        hash.Add(HairStyleId);
        hash.Add(HairColor);
        hash.Add(HairDye);
        hash.Add(FacialHairDye);
        hash.Add(BodyDye);
        hash.Add(FacialHairStyleId);
        hash.Add(FacialHairColor);
        hash.Add(EyeColor);
        hash.Add(SkinColor);
        hash.Add(Markings);
        return hash.ToHashCode();
        // DS14-End
    }

    public HumanoidCharacterAppearance Clone()
    {
        return new(this);
    }
}
