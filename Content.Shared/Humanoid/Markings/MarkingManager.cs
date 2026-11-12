using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

<<<<<<< ours
namespace Content.Shared.Humanoid.Markings
||||||| base
namespace Content.Shared.Humanoid.Markings;

/// <summary>
/// Manager responsible for sharing the logic of markings between in-simulation bodies and out-of-simulation profile editing
/// </summary>
public sealed class MarkingManager
=======
namespace Content.Shared.Humanoid.Markings;

/// <summary>
/// Manager responsible for sharing the logic of markings between in-simulation bodies and out-of-simulation profile editing
/// </summary>
public sealed partial class MarkingManager
>>>>>>> theirs
{
    [Dependency] private IComponentFactory _component = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private FrozenDictionary<HumanoidVisualLayers, FrozenDictionary<string, MarkingPrototype>> _categorizedMarkings = default!;
    private FrozenDictionary<string, MarkingPrototype> _markings = default!;

    public void Initialize()
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        private readonly List<MarkingPrototype> _index = new();
        public FrozenDictionary<MarkingCategories, FrozenDictionary<string, MarkingPrototype>> CategorizedMarkings = default!;
        public FrozenDictionary<string, MarkingPrototype> Markings = default!;

        public void Initialize()
        {
            _prototypeManager.PrototypesReloaded += OnPrototypeReload;
            CachePrototypes();
        }

        private void CachePrototypes()
        {
            _index.Clear();
            var markingDict = new Dictionary<MarkingCategories, Dictionary<string, MarkingPrototype>>();

            foreach (var category in Enum.GetValues<MarkingCategories>())
            {
                markingDict.Add(category, new());
            }

            foreach (var prototype in _prototypeManager.EnumeratePrototypes<MarkingPrototype>())
            {
                _index.Add(prototype);
                markingDict[prototype.MarkingCategory].Add(prototype.ID, prototype);
            }

            Markings = _prototypeManager.EnumeratePrototypes<MarkingPrototype>().ToFrozenDictionary(x => x.ID);
            CategorizedMarkings = markingDict.ToFrozenDictionary(
                x => x.Key,
                x => x.Value.ToFrozenDictionary());
        }

        public FrozenDictionary<string, MarkingPrototype> MarkingsByCategory(MarkingCategories category)
        {
            // all marking categories are guaranteed to have a dict entry
            return CategorizedMarkings[category];
        }

        /// <summary>
        ///     Markings by category and species.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="species"></param>
        /// <remarks>
        ///     This is done per category, as enumerating over every single marking by species isn't useful.
        ///     Please make a pull request if you find a use case for that behavior.
        /// </remarks>
        /// <returns></returns>
        public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSpecies(MarkingCategories category,
            string species)
        {
            var speciesProto = _prototypeManager.Index<SpeciesPrototype>(species);
            var markingPoints = _prototypeManager.Index(speciesProto.MarkingPoints);
            var res = new Dictionary<string, MarkingPrototype>();

            foreach (var (key, marking) in MarkingsByCategory(category))
            {
                if ((markingPoints.OnlyWhitelisted || markingPoints.Points[category].OnlyWhitelisted) && marking.SpeciesRestrictions == null)
                {
                    continue;
                }

<<<<<<< ours
                if (marking.SpeciesRestrictions != null && !marking.SpeciesRestrictions.Contains(species))
||||||| base
                if (marking.Sprites.Count != markings[i].MarkingColors.Count)
=======
                var existingColors = markings[i].MarkingColors;
                var markingColorCount = marking.GetColorCount();

                // If there are less colors than sprites, fill missing spots with the last existing color,
                // or white, if there are no existing colors
                // This will occur if an existing marking is edited to have additional layers
                if (existingColors.Count < markingColorCount)
                {
                    var numMissingColors = markingColorCount - existingColors.Count;
                    var lastColor = existingColors.Count > 0
                        ? existingColors[^1] // Last color in the list
                        : Color.White; // White, if none

                    var missingColors = Enumerable.Repeat(lastColor, numMissingColors);
                    markings[i] = new Marking(marking.ID, existingColors.Concat(missingColors));
                }
                // If there are more colors than sprites, drop the extras
                else if (marking.Sprites.Count < existingColors.Count)
                {
                    markings[i] = new Marking(marking.ID, existingColors.Take(marking.Sprites.Count));
                }

                // Ensure the colors are clamped properly
                if (marking.Strategy == null)
                    continue;

                var clampedColors = new List<Color>();
                foreach (var color in markings[i].MarkingColors)
>>>>>>> theirs
                {
                    clampedColors.Add(marking.Strategy.EnsureVerified(color));
                }

                markings[i] =  new Marking(marking.ID, clampedColors);
            }

            return res;
        }

<<<<<<< ours
        /// <summary>
        ///     Markings by category and sex.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="sex"></param>
        /// <remarks>
        ///     This is done per category, as enumerating over every single marking by species isn't useful.
        ///     Please make a pull request if you find a use case for that behavior.
        /// </remarks>
        /// <returns></returns>
        public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSex(MarkingCategories category,
            Sex sex)
||||||| base
    /// <summary>
    /// Ensures that the <see cref="markingSets"/> only belong to the <see cref="layers"/>
    /// </summary>
    public void EnsureValidLayers(Dictionary<HumanoidVisualLayers, List<Marking>> markingSets, HashSet<HumanoidVisualLayers> layers)
    {
        foreach (var markings in markingSets.Values)
=======
    /// <summary>
    /// Ensures that the <see cref="markingSets"/> only belong to the <see cref="layers"/>
    /// </summary>
    public void EnsureValidLayers(Dictionary<HumanoidVisualLayers, List<Marking>> markingSets, HashSet<HumanoidVisualLayers> layers)
    {
        foreach (var (markingSet, markings) in markingSets)
>>>>>>> theirs
        {
            var res = new Dictionary<string, MarkingPrototype>();

            foreach (var (key, marking) in MarkingsByCategory(category))
            {
                if (marking.SexRestriction != null && marking.SexRestriction != sex)
                {
                    continue;
                }

                res.Add(key, marking);
            }
<<<<<<< ours
||||||| base
        }
    }
=======

            if (markings.Count == 0)
                markingSets.Remove(markingSet);
        }
    }
>>>>>>> theirs

            return res;
        }

        /// <summary>
        ///     Markings by category, species and sex.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="species"></param>
        /// <param name="sex"></param>
        /// <remarks>
        ///     This is done per category, as enumerating over every single marking by species isn't useful.
        ///     Please make a pull request if you find a use case for that behavior.
        /// </remarks>
        /// <returns></returns>
        public IReadOnlyDictionary<string, MarkingPrototype> MarkingsByCategoryAndSpeciesAndSex(MarkingCategories category,
            string species, Sex sex)
        {
            var speciesProto = _prototypeManager.Index<SpeciesPrototype>(species);
            var onlyWhitelisted = _prototypeManager.Index(speciesProto.MarkingPoints).OnlyWhitelisted;
            var res = new Dictionary<string, MarkingPrototype>();

            foreach (var (key, marking) in MarkingsByCategory(category))
            {
                if (onlyWhitelisted && marking.SpeciesRestrictions == null)
                {
                    continue;
                }

                if (marking.SpeciesRestrictions != null && !marking.SpeciesRestrictions.Contains(species))
                {
                    continue;
                }

                if (marking.SexRestriction != null && marking.SexRestriction != sex)
                {
                    continue;
                }

                counts[marking.BodyPart] = count + 1;
            }

            return res;
        }

        public bool TryGetMarking(Marking marking, [NotNullWhen(true)] out MarkingPrototype? markingResult)
        {
            return Markings.TryGetValue(marking.MarkingId, out markingResult);
        }

        /// <summary>
        ///     Check if a marking is valid according to the category, species, and current data this marking has.
        /// </summary>
        /// <param name="marking"></param>
        /// <param name="category"></param>
        /// <param name="species"></param>
        /// <param name="sex"></param>
        /// <returns></returns>
        public bool IsValidMarking(Marking marking, MarkingCategories category, string species, Sex sex)
        {
            if (!TryGetMarking(marking, out var proto))
            {
                return false;
            }

<<<<<<< ours
            if (proto.MarkingCategory != category ||
                proto.SpeciesRestrictions != null && !proto.SpeciesRestrictions.Contains(species) ||
                proto.SexRestriction != null && proto.SexRestriction != sex)
            {
                return false;
            }
||||||| base
        if (!appearancePrototype.TryGetComponent<InitialBodyComponent>(out var initialBody, _component))
            return new();

        return initialBody.Organs;
    }

    /// <summary>
    /// Looks up the expected set of <see cref="OrganMarkingData" /> for the species to have
    /// </summary>
    /// <param name="species">The species to look up the usual organs of.</param>
    /// <returns>A dictionary of organ categories to their usual organ marking data within a species.</returns>
    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData> GetMarkingData(ProtoId<SpeciesPrototype> species)
    {
        var ret = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData>();
=======
        if (!appearancePrototype.TryComp<InitialBodyComponent>(out var initialBody, _component))
            return new();

        return initialBody.Organs;
    }

    /// <summary>
    /// Looks up the expected set of <see cref="OrganMarkingData" /> for the species to have
    /// </summary>
    /// <param name="species">The species to look up the usual organs of.</param>
    /// <returns>A dictionary of organ categories to their usual organ marking data within a species.</returns>
    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData> GetMarkingData(ProtoId<SpeciesPrototype> species)
    {
        var ret = new Dictionary<ProtoId<OrganCategoryPrototype>, OrganMarkingData>();
>>>>>>> theirs

            if (marking.MarkingColors.Count != proto.Sprites.Count)
            {
                return false;
            }

            return true;
        }

        private void OnPrototypeReload(PrototypesReloadedEventArgs args)
        {
            if (args.WasModified<MarkingPrototype>())
                CachePrototypes();
        }

<<<<<<< ours
        public bool CanBeApplied(string species, Sex sex, Marking marking, IPrototypeManager? prototypeManager = null)
        {
            IoCManager.Resolve(ref prototypeManager);
||||||| base
        return ret;
    }

    /// <summary>
    /// Gets the <see cref="OrganMarkingData" /> for the entity prototype corresponding to an organ
    /// </summary>
    /// <param name="organ">The ID of the organ entity prototype to look up</param>
    /// <param name="organData">The marking data for the organ if it exists</param>
    /// <returns>Whether the provided entity prototype ID corresponded to organ marking data that could be returned</returns>
    public bool TryGetMarkingData(EntProtoId organ, [NotNullWhen(true)] out OrganMarkingData? organData)
    {
        organData = null;

        if (!_prototype.TryIndex(organ, out var organProto))
            return false;

        if (!organProto.TryGetComponent<VisualOrganMarkingsComponent>(out var comp, _component))
            return false;

        organData = comp.MarkingData;

        return true;
    }
=======
        return ret;
    }

    /// <summary>
    /// Gets the <see cref="OrganMarkingData" /> for the entity prototype corresponding to an organ
    /// </summary>
    /// <param name="organ">The ID of the organ entity prototype to look up</param>
    /// <param name="organData">The marking data for the organ if it exists</param>
    /// <returns>Whether the provided entity prototype ID corresponded to organ marking data that could be returned</returns>
    public bool TryGetMarkingData(EntProtoId organ, [NotNullWhen(true)] out OrganMarkingData? organData)
    {
        organData = null;

        if (!_prototype.TryIndex(organ, out var organProto))
            return false;

        if (!organProto.TryComp<VisualOrganMarkingsComponent>(out var comp, _component))
            return false;

        organData = comp.MarkingData;

        return true;
    }
>>>>>>> theirs

            var speciesProto = prototypeManager.Index<SpeciesPrototype>(species);
            var onlyWhitelisted = prototypeManager.Index(speciesProto.MarkingPoints).OnlyWhitelisted;

            if (!TryGetMarking(marking, out var prototype))
            {
                return false;
            }

            if (onlyWhitelisted && prototype.SpeciesRestrictions == null)
            {
                return false;
            }

            if (prototype.SpeciesRestrictions != null
                && !prototype.SpeciesRestrictions.Contains(species))
            {
                return false;
            }

            if (prototype.SexRestriction != null && prototype.SexRestriction != sex)
            {
                return false;
            }

            return true;
        }

        public bool CanBeApplied(string species, Sex sex, MarkingPrototype prototype, IPrototypeManager? prototypeManager = null)
        {
            IoCManager.Resolve(ref prototypeManager);

            var speciesProto = prototypeManager.Index<SpeciesPrototype>(species);
            var onlyWhitelisted = prototypeManager.Index(speciesProto.MarkingPoints).OnlyWhitelisted;

            if (onlyWhitelisted && prototype.SpeciesRestrictions == null)
            {
                return false;
            }

            if (prototype.SpeciesRestrictions != null &&
                !prototype.SpeciesRestrictions.Contains(species))
            {
                return false;
            }

            if (prototype.SexRestriction != null && prototype.SexRestriction != sex)
            {
                return false;
            }

            return true;
        }

        public bool MustMatchSkin(string species, HumanoidVisualLayers layer, out float alpha, IPrototypeManager? prototypeManager = null)
        {
            IoCManager.Resolve(ref prototypeManager);
            var speciesProto = prototypeManager.Index<SpeciesPrototype>(species);
            if (
                !prototypeManager.Resolve(speciesProto.SpriteSet, out var baseSprites) ||
                !baseSprites.Sprites.TryGetValue(layer, out var spriteName) ||
                !prototypeManager.Resolve(spriteName, out HumanoidSpeciesSpriteLayer? sprite) ||
                sprite == null ||
                !sprite.MarkingsMatchSkin
            )
            {
                alpha = 1f;
                return false;
            }

            alpha = sprite.LayerAlpha;
            return true;
        }
    }
}
