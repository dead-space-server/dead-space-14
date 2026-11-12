using System.Numerics;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using static Content.Shared.Preferences.HumanoidCharacterProfile;

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
    public bool HairGradientEnabled { get; set; } = false;

    [DataField]
    public Color HairGradientColor { get; set; } = Color.Black;
    // DS14-end

    [DataField("facialHair")]
    public string FacialHairStyleId { get; set; } = HairStyles.DefaultFacialHairStyle;

    [DataField]
    public Color FacialHairColor { get; set; } = Color.Black;

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
        List<Marking> markings,
        // DS14-start
        bool hairGradientEnabled = false,
        Color? hairGradientColor = null)
        // DS14-end
    {
        HairStyleId = hairStyleId;
        HairColor = ClampColor(hairColor);
        // DS14-start
        HairGradientEnabled = hairGradientEnabled;
        HairGradientColor = hairGradientColor.HasValue ? ClampColor(hairGradientColor.Value) : ClampColor(hairColor);
        // DS14-end
        FacialHairStyleId = facialHairStyleId;
        FacialHairColor = ClampColor(facialHairColor);
        EyeColor = ClampColor(eyeColor);
        SkinColor = ClampColor(skinColor);
        Markings = markings;
    }

    public HumanoidCharacterAppearance(HumanoidCharacterAppearance other) :
        this(other.HairStyleId, other.HairColor, other.FacialHairStyleId, other.FacialHairColor, other.EyeColor, other.SkinColor, new(other.Markings), other.HairGradientEnabled, other.HairGradientColor) // DS14
    {

    }

    public HumanoidCharacterAppearance WithHairStyleName(string newName)
    {
        return new(newName, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColor); // DS14
    }

    public HumanoidCharacterAppearance WithHairColor(Color newColor)
    {
        return new(HairStyleId, newColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColor); // DS14
    }

    // DS14-start
    public HumanoidCharacterAppearance WithHairGradientEnabled(bool enabled)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, enabled, HairGradientColor);
    }

    public HumanoidCharacterAppearance WithHairGradientColor(Color newColor)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, Markings, HairGradientEnabled, newColor);
    }
    // DS14-end

    public HumanoidCharacterAppearance WithFacialHairStyleName(string newName)
    {
        return new(HairStyleId, HairColor, newName, FacialHairColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColor); // DS14
    }

    public HumanoidCharacterAppearance WithFacialHairColor(Color newColor)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, newColor, EyeColor, SkinColor, Markings, HairGradientEnabled, HairGradientColor); // DS14
    }

    public HumanoidCharacterAppearance WithEyeColor(Color newColor)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, newColor, SkinColor, Markings, HairGradientEnabled, HairGradientColor); // DS14
    }

    public HumanoidCharacterAppearance WithSkinColor(Color newColor)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, newColor, Markings, HairGradientEnabled, HairGradientColor); // DS14
    }

    public HumanoidCharacterAppearance WithMarkings(List<Marking> newMarkings)
    {
        return new(HairStyleId, HairColor, FacialHairStyleId, FacialHairColor, EyeColor, SkinColor, newMarkings, HairGradientEnabled, HairGradientColor); // DS14
    }

    public static HumanoidCharacterAppearance DefaultWithSpecies(string species)
    {
        var protoMan = IoCManager.Resolve<IPrototypeManager>();
        var speciesPrototype = protoMan.Index(species);
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

    private static readonly IReadOnlyList<Color> RealisticEyeColors =
    [
        Color.Brown,
        Color.Gray,
        Color.Azure,
        Color.SteelBlue,
        Color.Black
    ];

    /// <summary>
    /// Picks a random eye color.
    /// </summary>
    public static Color RandomEyes()
    {
        var random = IoCManager.Resolve<IRobustRandom>();
<<<<<<< ours
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
||||||| base
        var markingManager = IoCManager.Resolve<MarkingManager>();

        // TODO: Add random markings
=======
>>>>>>> theirs

        var eyes = random.Pick(RealisticEyeColors);
        return eyes;
    }

    /// <summary>
    /// Picks a random skin color using species.
    /// </summary>
    public static Color RandomSkin(ProtoId<SpeciesPrototype> species)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();

        var speciesProto = protoMan.Index(species);
        var skinType = speciesProto.SkinColoration;
        var strategy = protoMan.Index(skinType).Strategy;

        var skinColor = strategy.InputType switch
        {
            SkinColorationStrategyInput.Unary => strategy.FromUnary(random.NextFloat(0f, 100f)),
            SkinColorationStrategyInput.Color => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
            _ => strategy.ClosestSkinColor(new Color(random.NextFloat(1), random.NextFloat(1), random.NextFloat(1), 1)),
        };

<<<<<<< ours
        return new HumanoidCharacterAppearance(newHairStyle, newHairColor, newFacialHairStyle, newHairColor, newEyeColor, newSkinColor, new() // DS14
        ); // DS14

        float RandomizeColor(float channel)
        {
            return MathHelper.Clamp01(channel + random.Next(-25, 25) / 100f);
        }
||||||| base
        return new HumanoidCharacterAppearance(newEyeColor, newSkinColor, new());
=======
        return skinColor;
    }

    /// <summary>
    ///     Generates a randomized character appearance.
    /// </summary>
    /// <remarks>
    ///     When <see cref="RandomizeCfg"/> and an existing <see cref="HumanoidCharacterAppearance"> are passed in,
    ///     values will be selectively randomized with the option to maintain existing values.
    /// </remarks>
    /// <param name="charEditorRandomizeConfig">Which values to randomize.</param>
    /// <param name="baseAppearance">Appearance to base the new appearance on. Values that are not randomized will be taken from this appearance.</param>
    /// <param name="species">Species prototype ID.</param>
    /// <param name="sex">Sex.</param>
    public static HumanoidCharacterAppearance Random(SpeciesPrototype species, Sex sex, RandomizeCfg? charEditorRandomizeConfig = null, HumanoidCharacterAppearance? baseAppearance = null)
    {
        var random = IoCManager.Resolve<IRobustRandom>();
        var protoMan = IoCManager.Resolve<IPrototypeManager>();

        var skinType = protoMan.Index(species.SkinColoration);
        var palette = GetRandomClampedPalette(skinType, random);

        // squash Cfg as necessary
        palette = palette with
        {
            SkinColor = (charEditorRandomizeConfig & RandomizeCfg.Skin) != 0 || baseAppearance is null
                ? palette.SkinColor : baseAppearance.SkinColor,
            EyeColor = (charEditorRandomizeConfig & RandomizeCfg.Eyes) != 0 || baseAppearance is null
                ? palette.EyeColor : baseAppearance.EyeColor
        };

        var markings = ((charEditorRandomizeConfig & RandomizeCfg.Markings) != 0 || baseAppearance is null)
            ? RandomizeMarkings(species, sex, palette, protoMan, random)
            : baseAppearance.Markings;
        // TODO if someone really cares they can probably regenerate the old markings with new colors but im too tired to figure that out

        HumanoidCharacterAppearance appearance = new(
            palette.EyeColor,
            palette.SkinColor,
            markings);

        // Safety step. Most systems which called Random() also called this, and not doing so caused issues with markings.
        // In the future it could *maybe* be removed, but it's probably worth the extra CPU cycles to validate this info.
        return EnsureValid(appearance, species, sex);
>>>>>>> theirs
    }

    public static Color ClampColor(Color color)
    {
        return new(color.RByte, color.GByte, color.BByte);
    }

    // DS14-sponsors-start: Added sponsorMarkings parameter
    public static HumanoidCharacterAppearance EnsureValid(HumanoidCharacterAppearance appearance, string species, Sex sex, string[] sponsorMarkings)
    {
<<<<<<< ours
        var hairStyleId = appearance.HairStyleId;
        var facialHairStyleId = appearance.FacialHairStyleId;

        var hairColor = ClampColor(appearance.HairColor);
        var facialHairColor = ClampColor(appearance.FacialHairColor);
        var eyeColor = ClampColor(appearance.EyeColor);

||||||| base
        var eyeColor = ClampColor(appearance.EyeColor);

=======
>>>>>>> theirs
        var proto = IoCManager.Resolve<IPrototypeManager>();
        var markingManager = IoCManager.Resolve<MarkingManager>();
        var hairStyleIdForSponsorCheck = hairStyleId; // DS14

<<<<<<< ours
        // DS14-start
        if (!markingManager.MarkingsByCategory(MarkingCategories.Hair).ContainsKey(hairStyleId))
||||||| base
        var skinColor = appearance.SkinColor;
        var validatedMarkings = appearance.Markings.ShallowClone();

        if (proto.TryIndex(species, out var speciesProto))
=======
        var skinColor = appearance.SkinColor;
        var eyeColor = ClampColor(appearance.EyeColor); // not using ClampEyeColorToStrategy so characters can have fun eye colours
        var validatedMarkings = appearance.Markings.ShallowClone();

        if (proto.TryIndex(species, out var speciesProto))
>>>>>>> theirs
        {
<<<<<<< ours
            if (appearance.HairGradientEnabled && hairStyleId.EndsWith("Gradient"))
||||||| base
            var strategy = proto.Index(speciesProto.SkinColoration).Strategy;
            var organs = markingManager.GetOrgans(species);
            skinColor = strategy.EnsureVerified(skinColor);

            foreach (var (organ, markings) in appearance.Markings)
=======
            var coloration = proto.Index(speciesProto.SkinColoration);
            var organs = markingManager.GetOrgans(species);
            skinColor = coloration.Strategy.EnsureVerified(skinColor);

            foreach (var (organ, _) in appearance.Markings)
>>>>>>> theirs
            {
                var baseId = hairStyleId[..^"Gradient".Length];
                if (!markingManager.MarkingsByCategory(MarkingCategories.Hair).ContainsKey(baseId))
                    hairStyleId = HairStyles.DefaultHairStyle;
            }
            else
            {
                hairStyleId = HairStyles.DefaultHairStyle;
            }
        }

<<<<<<< ours
        if (appearance.HairGradientEnabled && hairStyleId.EndsWith("Gradient"))
        {
            var baseId = hairStyleId[..^"Gradient".Length];
            if (markingManager.MarkingsByCategory(MarkingCategories.Hair).ContainsKey(baseId))
                hairStyleIdForSponsorCheck = baseId;
        }
||||||| base
                var actualMarkings = appearance.Markings.GetValueOrDefault(organ)?.ShallowClone() ?? [];
=======
                var actualMarkings = appearance.Markings.GetValueOrDefault(organ)?.ShallowClone() ?? new();
>>>>>>> theirs

        if (proto.TryIndex(hairStyleIdForSponsorCheck, out MarkingPrototype? hairProto) &&
            hairProto.SponsorOnly &&
            !sponsorMarkings.Contains(hairStyleIdForSponsorCheck))
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
            markingSet.GetForwardEnumerator().ToList(),
            // DS14-start
            appearance.HairGradientEnabled,
            appearance.HairGradientColor
            // DS14-end
        );
    }
    // DS14-sponsors-end

    public bool MemberwiseEquals(ICharacterAppearance maybeOther)
    {
        if (maybeOther is not HumanoidCharacterAppearance other) return false;
        if (HairStyleId != other.HairStyleId) return false;
        if (!HairColor.Equals(other.HairColor)) return false;
        // DS14-start
        if (HairGradientEnabled != other.HairGradientEnabled) return false;
        if (!HairGradientColor.Equals(other.HairGradientColor)) return false;
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
               HairGradientEnabled == other.HairGradientEnabled &&
               HairGradientColor.Equals(other.HairGradientColor) &&
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
        hash.Add(HairGradientEnabled);
        hash.Add(HairGradientColor);
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
