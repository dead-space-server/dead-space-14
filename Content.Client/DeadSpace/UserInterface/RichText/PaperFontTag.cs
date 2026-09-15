// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.UserInterface.RichText;

/// <summary>
/// Switches the font of the enclosed text to one of the whitelisted fonts
/// from Resources/Fonts. Usage: <c>[pfont="ComicSans"]text[/pfont]</c>.
/// </summary>
public sealed class PaperFontTag : IMarkupTagHandler
{
    public const string DefaultFontId = FontTag.DefaultFont;
    public const string ComicSansFontId = "ComicSans";

    public static readonly string[] AllowedFonts =
    [
        DefaultFontId,
        "NotoSansDisplay",
        "BoxRound",
        "OpenLukyanov",
        "Bedstead",
        "ComicSans",
    ];

    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public string Name => "pfont";

    public void PushDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        var fontId = node.Value.StringValue ?? DefaultFontId;
        if (!AllowedFonts.Contains(fontId))
            fontId = DefaultFontId;

        node.Attributes["size"] = new MarkupParameter(GetCurrentSize(context));

        var font = FontTag.CreateFont(context.Font, node, _resourceCache, _prototypeManager, fontId);
        context.Font.Push(font);
    }

    public void PopDrawContext(MarkupNode node, MarkupDrawingContext context)
    {
        context.Font.Pop();
    }

    private static int GetCurrentSize(MarkupDrawingContext context)
    {
        foreach (var font in context.Font)
        {
            if (font is VectorFont vectorFont)
                return vectorFont.Size;
        }

        return FontTag.DefaultSize;
    }
}
