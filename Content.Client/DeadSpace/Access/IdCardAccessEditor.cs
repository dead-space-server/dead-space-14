// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using System.Linq;
using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.Access;
using Content.Shared.DeadSpace.Access;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.DeadSpace.Access;

public sealed class IdCardAccessEditor : BoxContainer
{
    public readonly Dictionary<ProtoId<AccessLevelPrototype>, Button> ButtonsList = new();
    public event Action? AccessChanged;

    private readonly BoxContainer _categories;
    private readonly BoxContainer _accessLists;
    private readonly Label _title;
    private readonly Dictionary<string, Button> _categoryButtons = new();
    private readonly Dictionary<string, GridContainer> _grids = new();
    private readonly Dictionary<string, List<ProtoId<AccessLevelPrototype>>> _categoryAccess = new();
    private string? _selectedCategory;

    public IEnumerable<ProtoId<AccessLevelPrototype>> CurrentAccessLevels =>
        _selectedCategory != null && _categoryAccess.TryGetValue(_selectedCategory, out var access)
            ? access : Array.Empty<ProtoId<AccessLevelPrototype>>();

    public IdCardAccessEditor()
    {
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 12;

        _categories = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 6 };
        AddChild(new PanelContainer
        {
            SetWidth = 250,
            VerticalExpand = true,
            StyleClasses = { DeadSpaceStyleClass.SurfaceFlat },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    SeparationOverride = 10,
                    Children =
                    {
                        new Label { Text = Loc.GetString("id-card-console-window-sections"), StyleClasses = { DeadSpaceStyleClass.SectionTitle } },
                        new ScrollContainer { VerticalExpand = true, Children = { _categories } },
                    },
                },
            },
        });

        _title = new Label { StyleClasses = { DeadSpaceStyleClass.ListHeader } };
        _accessLists = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
        AddChild(new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            StyleClasses = { DeadSpaceStyleClass.SurfaceDark },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    SeparationOverride = 10,
                    Children =
                    {
                        new PanelContainer { StyleClasses = { DeadSpaceStyleClass.SectionHeader }, Children = { _title } },
                        new ScrollContainer { HorizontalExpand = true, VerticalExpand = true, Children = { _accessLists } },
                    },
                },
            },
        });
    }

    public void Populate(List<ProtoId<AccessLevelPrototype>> accessLevels, IPrototypeManager prototypes)
    {
        if (ButtonsList.Keys.ToHashSet().SetEquals(accessLevels))
            return;

        _categories.RemoveAllChildren();
        _accessLists.RemoveAllChildren();
        ButtonsList.Clear();
        _categoryButtons.Clear();
        _categoryAccess.Clear();
        _grids.Clear();

        var remaining = accessLevels.ToHashSet();
        foreach (var category in prototypes.EnumeratePrototypes<IdCardAccessCategoryPrototype>()
                     .OrderBy(category => category.Order).ThenBy(category => category.ID))
        {
            var members = category.AccessLevels.Where(remaining.Contains).ToList();
            if (members.Count == 0)
                continue;

            remaining.ExceptWith(members);
            AddCategory(category.ID, Loc.GetString(category.Name), members, prototypes);
        }

        if (remaining.Count != 0)
            AddCategory("uncategorized", Loc.GetString("id-card-console-category-other"), remaining.ToList(), prototypes);

        if (_selectedCategory == null || !_grids.ContainsKey(_selectedCategory))
            _selectedCategory = _grids.Keys.FirstOrDefault();
        if (_selectedCategory != null)
            SelectCategory(_selectedCategory);
    }

    private void AddCategory(string id, string name, List<ProtoId<AccessLevelPrototype>> members, IPrototypeManager prototypes)
    {
        var tab = new Button
        {
            Text = name,
            ToolTip = name,
            ToggleMode = true,
            MinHeight = 32,
            HorizontalExpand = true,
            TextAlign = Label.AlignMode.Left,
            StyleClasses = { DeadSpaceStyleClass.ListItem },
        };
        tab.Label.ClipText = true;
        tab.OnPressed += _ => SelectCategory(id);
        _categoryButtons.Add(id, tab);
        _categories.AddChild(tab);
        _categoryAccess.Add(id, members);

        var grid = new GridContainer
        {
            Columns = 2,
            HSeparationOverride = 8,
            VSeparationOverride = 6,
            HorizontalExpand = true,
            Visible = false,
        };
        _grids.Add(id, grid);
        _accessLists.AddChild(grid);
        foreach (var member in members.Select(member => prototypes.Index(member)).OrderBy(level => level.GetAccessLevelName()))
        {
            var button = new Button
            {
                Text = member.GetAccessLevelName(),
                ToolTip = member.GetAccessLevelName(),
                ToggleMode = true,
                Disabled = true,
                MinHeight = 30,
                TextAlign = Label.AlignMode.Left,
                HorizontalExpand = true,
            };
            button.Label.ClipText = true;
            button.OnPressed += _ => AccessChanged?.Invoke();
            ButtonsList.Add(member.ID, button);
            grid.AddChild(button);
        }
    }

    private void SelectCategory(string id)
    {
        _selectedCategory = id;
        _title.Text = _categoryButtons[id].Text;
        foreach (var (category, grid) in _grids)
        {
            grid.Visible = category == id;
            _categoryButtons[category].Pressed = category == id;
        }
    }

    public void UpdateState(List<ProtoId<AccessLevelPrototype>> selected, List<ProtoId<AccessLevelPrototype>> editable)
    {
        var selectedSet = selected.ToHashSet();
        var editableSet = editable.ToHashSet();
        foreach (var (id, button) in ButtonsList)
        {
            button.Pressed = selectedSet.Contains(id);
            button.Disabled = !editableSet.Contains(id);
            if (button.Pressed && button.Disabled)
                button.AddStyleClass(DeadSpaceStyleClass.LockedSelected);
            else
                button.RemoveStyleClass(DeadSpaceStyleClass.LockedSelected);
        }
    }
}
