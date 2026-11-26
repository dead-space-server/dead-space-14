// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.DeadSpace.UserInterface.Hologram.Options;

/// <summary>
/// Uppercase captions of the "Dead Space" style: while it is active the tracked texts are shown in capitals, otherwise
/// as written. The stylesheet can't change letter case, so the owner calls <see cref="Update"/> every frame (or on
/// restyle). A text changed by other code in the meantime is picked up as the new original.
/// </summary>
public sealed class HologramCaps
{
    private readonly List<Entry> _entries = [];

    public void Add(Button button)
    {
        _entries.Add(new Entry(() => button.Text, text => button.Text = text));
    }

    public void Add(Label label)
    {
        _entries.Add(new Entry(() => label.Text, text => label.Text = text));
    }

    /// <summary>Tracks every <see cref="Button"/> under <paramref name="root"/>, the root included.</summary>
    public void AddButtons(Control root)
    {
        if (root is Button button)
            Add(button);

        foreach (var child in root.Children)
        {
            AddButtons(child);
        }
    }

    /// <summary>Tracks the title of the tab <paramref name="index"/>.</summary>
    public void AddTab(TabContainer tabs, int index)
    {
        _entries.Add(new Entry(
            () => TabContainer.GetTabTitle(tabs.GetChild(index)),
            text =>
            {
                tabs.SetTabTitle(index, text);
                // A tab title is an attached property: the strip doesn't remeasure by itself.
                tabs.InvalidateMeasure();
            }));
    }

    public void Update()
    {
        var caps = DeadSpaceStylePalette.Hologram;

        foreach (var entry in _entries)
        {
            var current = entry.Get();
            if (!string.Equals(current, entry.Applied, StringComparison.Ordinal))
                entry.SetSource(current);

            var wanted = caps ? entry.SourceUpper : entry.Source;
            if (wanted != null && !string.Equals(current, wanted, StringComparison.Ordinal))
                entry.Set(wanted);

            entry.Applied = wanted ?? current;
        }
    }

    private sealed class Entry(Func<string?> get, Action<string> set)
    {
        public readonly Func<string?> Get = get;
        public readonly Action<string> Set = set;

        public string? Source;
        public string? SourceUpper;
        public string? Applied;

        public void SetSource(string? source)
        {
            Source = source;
            SourceUpper = source?.ToUpperInvariant();
        }
    }
}
