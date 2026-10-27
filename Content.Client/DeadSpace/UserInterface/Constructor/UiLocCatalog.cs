// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public static class UiLocCatalog
{
    private static readonly Dictionary<string, string?> ByText = new();
    private static string _culture = "";

    public static string? FindId(IResourceManager resources, ILocalizationManager loc, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        Ensure(resources, loc);
        return ByText.TryGetValue(text, out var id) ? id : null;
    }

    public static List<string> IdsIn(IResourceManager resources, ResPath file)
    {
        var ids = new List<string>();
        try
        {
            using var reader = resources.ContentFileReadText(file);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length == 0 || line[0] == '#' || line[0] == ' ' || line[0] == '-')
                    continue;

                var split = line.IndexOf('=');
                if (split <= 0)
                    continue;

                var id = line[..split].Trim();
                if (id.Length > 0 && id.IndexOf(' ') < 0)
                    ids.Add(id);
            }
        }
        catch (Exception)
        {
            return ids;
        }

        return ids;
    }

    private static void Ensure(IResourceManager resources, ILocalizationManager loc)
    {
        var culture = loc.DefaultCulture?.Name ?? "";
        if (_culture == culture && ByText.Count > 0)
            return;

        ByText.Clear();
        _culture = culture;
        if (culture.Length == 0)
            return;

        foreach (var file in resources.ContentFindFiles(new ResPath("/Locale/" + culture)))
        {
            if (!file.ToString().EndsWith(".ftl", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var id in IdsIn(resources, file))
            {
                if (!loc.TryGetString(id, out var value) || value.Length == 0 || value.Contains('{'))
                    continue;

                if (ByText.TryGetValue(value, out var previous))
                {
                    if (previous != id)
                        ByText[value] = null;
                }
                else
                {
                    ByText[value] = id;
                }
            }
        }
    }
}
