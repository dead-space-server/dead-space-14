// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System;
using System.Collections.Generic;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;

namespace Content.Client.DeadSpace.UserInterface.Constructor;

public static class UiGameWindowFinder
{
    public readonly record struct Entry(string Title, string TypeName, string FullName, Type Type);

    private static List<Entry>? _all;

    public static IReadOnlyList<Entry> Load(IDynamicTypeFactory factory)
    {
        if (_all != null)
            return _all;

        _all = new List<Entry>();
        foreach (var type in typeof(UiConstructorWindow).Assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsGenericType || !type.IsSubclassOf(typeof(BaseWindow)))
                continue;

            if (!type.IsPublic && !type.IsNestedPublic)
                continue;

            if (type.Namespace == typeof(UiConstructorWindow).Namespace)
                continue;

            _all.Add(new Entry(ReadTitle(factory, type), type.Name, type.FullName ?? type.Name, type));
        }

        _all.Sort(static (left, right) => string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase));
        return _all;
    }

    private static string ReadTitle(IDynamicTypeFactory factory, Type type)
    {
        BaseWindow window;
        try
        {
            if (factory.CreateInstance(type, oneOff: true) is not BaseWindow created)
                return type.Name;

            window = created;
        }
        catch (Exception)
        {
            return type.Name;
        }

        try
        {
            if (window is DefaultWindow styled && !string.IsNullOrWhiteSpace(styled.Title))
                return styled.Title!;

            if (window is FancyWindow fancy && !string.IsNullOrWhiteSpace(fancy.Title))
                return fancy.Title!;

            return type.Name;
        }
        finally
        {
            try
            {
                if (window.IsOpen)
                    window.Close();

                window.Orphan();
                window.Dispose();
            }
            catch (Exception)
            {
            }
        }
    }
}
