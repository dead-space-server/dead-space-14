// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.DeadSpace.PdaArcade;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.PdaArcade;

public static class PdaArcadeArt
{
    private static SpriteSystem? _sprites;
    private static IPrototypeManager? _prototypes;
    private static IResourceCache? _cache;
    private static readonly Dictionary<int, Font> Fonts = new();

    public static PdaArcadeProfilePrototype Profile =>
        Prototypes.EnumeratePrototypes<PdaArcadeProfilePrototype>().First();

    public static PdaArcadeGamePrototype Game(string id)
    {
        return Prototypes.Index<PdaArcadeGamePrototype>(id);
    }

    public static bool TryGame(string id, [NotNullWhen(true)] out PdaArcadeGamePrototype? game)
    {
        return Prototypes.TryIndex(id, out game);
    }

    public static IEnumerable<PdaArcadeGamePrototype> Games()
    {
        return Prototypes.EnumeratePrototypes<PdaArcadeGamePrototype>().OrderBy(game => game.Order);
    }

    public static Texture Texture(SpriteSpecifier specifier)
    {
        _sprites ??= IoCManager.Resolve<IEntityManager>().System<SpriteSystem>();
        return _sprites.Frame0(specifier);
    }

    public static Font Font(int size)
    {
        size = Math.Clamp(size, 8, 28);
        if (Fonts.TryGetValue(size, out var font))
            return font;

        _cache ??= IoCManager.Resolve<IResourceCache>();
        var resource = _cache.GetResource<FontResource>(Profile.Font);
        font = new VectorFont(resource, size);
        Fonts[size] = font;
        return font;
    }

    private static IPrototypeManager Prototypes => _prototypes ??= IoCManager.Resolve<IPrototypeManager>();
}
