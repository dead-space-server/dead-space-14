using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.Kitchen
{
    public sealed class RecipeManager
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public List<FoodRecipePrototype> Recipes { get; private set; } = new();

        public void Initialize()
        {
            //DS14-start
            _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;
            ReloadRecipes();
            //DS14-end
        }

        //DS14-start
        private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
        {
            if (args.WasModified<FoodRecipePrototype>())
            {
                ReloadRecipes();
            }
        }

        public void ReloadRecipes()
        {
            Recipes.Clear();
            foreach (var item in _prototypeManager.EnumeratePrototypes<FoodRecipePrototype>())
            {
                if (!item.SecretRecipe)
                    Recipes.Add(item);
            }

            Recipes.Sort(new RecipeComparer());
        }
        //DS14-end

        /// <summary>
        /// Check if a prototype ids appears in any of the recipes that exist.
        /// </summary>
        public bool SolidAppears(string solidId)
        {
            return Recipes.Any(recipe => recipe.IngredientsSolids.ContainsKey(solidId));
        }

        private sealed class RecipeComparer : Comparer<FoodRecipePrototype>
        {
            public override int Compare(FoodRecipePrototype? x, FoodRecipePrototype? y)
            {
                if (x == null || y == null)
                {
                    return 0;
                }

                var nx = x.IngredientCount();
                var ny = y.IngredientCount();
                return -nx.CompareTo(ny);
            }
        }
    }
}
