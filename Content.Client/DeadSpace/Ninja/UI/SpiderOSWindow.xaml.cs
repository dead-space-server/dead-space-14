// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.UserInterface.Controls;
using Content.Client.DeadSpace.Stylesheets;
using Content.Shared.DeadSpace.Ninja.Components;
using Content.Shared.DeadSpace.Ninja.Prototypes;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Utility;
using System.Numerics;
using Robust.Client.GameObjects;

namespace Content.Client.DeadSpace.Ninja.UI;

public sealed partial class SpiderOSWindow : FancyWindow
{
    [Dependency] private readonly IResourceCache _resCache = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;

    private static readonly string[] Categories = { "Ghost", "Snake", "Steel" };

    private const string PreviewBasePath = "/Textures/_DeadSpace/Interface/SpiderOS/";

    private static readonly Color HeaderColor = Color.FromHex("#1bcc15");

    public static readonly Color BootLogColor = Color.FromHex("#1bcc15");

    public static readonly Color BootErrorColor = Color.FromHex("#cc1515");

    private static readonly Dictionary<string, Color> CategoryColors = new()
    {
        ["Ghost"] = Color.FromHex("#000038"),
        ["Snake"] = Color.FromHex("#003800"),
        ["Steel"] = Color.FromHex("#380000"),
    };

    private static readonly Dictionary<string, Color> DividerColors = new()
    {
        ["Ghost"] = Color.FromHex("#156acc"),
        ["Snake"] = Color.FromHex("#1bcc15"),
        ["Steel"] = Color.FromHex("#cc1515"),
    };

    private GridContainer _moduleGrid = default!;

    private Button _activateButton = default!;

    private Button _shuttleButton = default!;

    private OptionButton _styleOption = default!;

    private OptionButton _colorOption = default!;

    private OptionButton _hoodOrScarfOption = default!;

    private ProgressBar _loadProgressBar = default!;

    private TextureRect _stylePreview = default!;

    private BoxContainer _mainView = default!;

    private LayoutContainer _loadView = default!;

    private OutputPanel _loadOutput = default!;

    private readonly List<(TextureButton Button, int Tier)> _moduleButtons = new();

    private bool _suitActivated;

    private bool _bootActive;

    private HashSet<int> _lockedTiers = new();

    private Dictionary<int, NinjaSkillsCategory> _selectedModules = new();

    public event Action<int, NinjaSkillsCategory>? OnModuleSelected;

    public event Action<bool>? OnSuitPowerChanged;

    public event Action? OnShuttleControl;

    public event Action<NinjaColorway, bool, NinjaStyle>? OnAppearanceChanged;

    public SpiderOSWindow()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        AddStyleClass(DeadSpaceStyleClass.SpiderOSWindow);
        HeaderClass = DeadSpaceStyleClass.SpiderOSWindowHeader;

        _moduleGrid = FindControl<GridContainer>("ModuleGrid");
        _moduleGrid.HSeparationOverride = 0;
        _moduleGrid.VSeparationOverride = 0;

        _activateButton = FindControl<Button>("ActivateButton");
        _activateButton.OnPressed += _ => OnSuitPowerChanged?.Invoke(!_suitActivated);

        _shuttleButton = FindControl<Button>("ShuttleButton");
        _shuttleButton.OnPressed += _ => OnShuttleControl?.Invoke();

        _styleOption = FindControl<OptionButton>("StyleOption");
        _styleOption.AddItem(Loc.GetString("spider-os-style-old"));
        _styleOption.AddItem(Loc.GetString("spider-os-style-new"));
        _styleOption.OnItemSelected += args =>
        {
            var style = StyleFromIndex(args.Id);
            UpdateHoodOrScarfForStyle(style);
            OnAppearanceChanged?.Invoke(ColorFromIndex(_colorOption.SelectedId), _hoodOrScarfOption.SelectedId == 0, style);
        };

        _colorOption = FindControl<OptionButton>("ColorOption");
        _colorOption.AddItem(Loc.GetString("spider-os-color-green"));
        _colorOption.AddItem(Loc.GetString("spider-os-color-red"));
        _colorOption.AddItem(Loc.GetString("spider-os-color-blue"));
        _colorOption.OnItemSelected += args => OnAppearanceChanged?.Invoke(ColorFromIndex(args.Id), _hoodOrScarfOption.SelectedId == 0, StyleFromIndex(_styleOption.SelectedId));

        _hoodOrScarfOption = FindControl<OptionButton>("HoodOrScarfOption");
        _hoodOrScarfOption.AddItem(Loc.GetString("spider-os-head-helmet"));
        _hoodOrScarfOption.AddItem(Loc.GetString("spider-os-head-scarf"));
        _hoodOrScarfOption.OnItemSelected += args => OnAppearanceChanged?.Invoke(ColorFromIndex(_colorOption.SelectedId), args.Id == 0, StyleFromIndex(_styleOption.SelectedId));

        _stylePreview = FindControl<TextureRect>("StylePreview");
        _stylePreview.TextureScale = new Vector2(4, 4);
        UpdatePreview(NinjaColorway.Green, true, NinjaStyle.Old);

        _mainView = FindControl<BoxContainer>("MainSpiderOS");
        _loadView = FindControl<LayoutContainer>("LoadSpiderOS");
        _loadOutput = FindControl<OutputPanel>("LoadOutput");

        _loadProgressBar = FindControl<ProgressBar>("LoadProgressBar");
        _loadProgressBar.Value = 0f;

        _bootActive = false;
        ShowMainView();
    }

    public void ShowBootView()
    {
        _bootActive = true;

        _mainView.Visible = false;
        _loadView.Visible = true;

        _loadOutput.Clear();
        _loadProgressBar.Value = 0f;

        _activateButton.Disabled = true;
        _shuttleButton.Disabled = true;
        _styleOption.Disabled = true;
        _colorOption.Disabled = true;
        _hoodOrScarfOption.Disabled = true;
        SetModulesEnabled(false);
    }

    public void ShowMainView()
    {
        _bootActive = false;

        _mainView.Visible = true;
        _loadView.Visible = false;

        _loadOutput.Clear();
        _loadProgressBar.Value = 0f;

        _activateButton.Disabled = false;
        _shuttleButton.Disabled = false;
        _styleOption.Disabled = _suitActivated;
        _colorOption.Disabled = _suitActivated;
        _hoodOrScarfOption.Disabled = _suitActivated;
        SetModulesEnabled(!_suitActivated);
    }

    public void AppendBootLog(string text, Color color)
    {
        _loadOutput.AddMessage(FormattedMessage.FromMarkupOrThrow($"[color={color.ToHex()}]{text}[/color]"));
        _loadOutput.ScrollToBottom();
    }

    public void SetBootProgress(float value)
    {
        _loadProgressBar.Value = Math.Clamp(value, 0f, 1f);
    }

    private void SetModulesEnabled(bool enabled)
    {
        foreach (var (button, tier) in _moduleButtons)
        {
            button.Disabled = !enabled || _lockedTiers.Contains(tier) || _selectedModules.ContainsKey(tier);
        }
    }

    public void UpdateState(
        HashSet<int> lockedTiers,
        Dictionary<int, NinjaSkillsCategory> selectedModules,
        HashSet<int> activatedTiers,
        List<NinjaSkill> skills,
        NinjaColorway pendingColorway,
        bool pendingHelmet,
        NinjaStyle pendingStyle,
        bool suitActivated)
    {
        _suitActivated = suitActivated;
        _lockedTiers = lockedTiers;
        _selectedModules = selectedModules;

        RebuildSkills(skills, lockedTiers, selectedModules, suitActivated);

        _activateButton.Text = Loc.GetString(suitActivated
            ? "spider-os-personalization-deactivate"
            : "spider-os-personalization-activate");

        _styleOption.Disabled = suitActivated || _bootActive;
        _styleOption.SelectId(IndexFromStyle(pendingStyle));

        _colorOption.Disabled = suitActivated || _bootActive;
        _colorOption.SelectId(IndexFromColor(pendingColorway));

        _hoodOrScarfOption.Disabled = suitActivated || _bootActive;
        _hoodOrScarfOption.SetItemDisabled(1, pendingStyle == NinjaStyle.New);
        _hoodOrScarfOption.SelectId(pendingStyle == NinjaStyle.New || pendingHelmet ? 0 : 1);

        UpdatePreview(pendingColorway, pendingHelmet, pendingStyle);
    }

    private void UpdateHoodOrScarfForStyle(NinjaStyle style)
    {
        _hoodOrScarfOption.SetItemDisabled(1, style == NinjaStyle.New);

        if (style == NinjaStyle.New)
            _hoodOrScarfOption.SelectId(0);
    }

    private void RebuildSkills(
        List<NinjaSkill> skills,
        HashSet<int> lockedTiers,
        Dictionary<int, NinjaSkillsCategory> selectedModules,
        bool suitActivated)
    {
        _moduleGrid.RemoveAllChildren();
        _moduleButtons.Clear();

        foreach (var category in Categories)
        {
            _moduleGrid.AddChild(MakeCategoryHeader(category));
        }

        foreach (var category in Categories)
        {
            _moduleGrid.AddChild(MakeDivider(DividerColors[category]));
        }

        var tiers = new SortedSet<int>();
        foreach (var skill in skills)
        {
            tiers.Add(skill.Tier);
        }

        foreach (var tier in tiers)
        {
            foreach (var category in Categories)
            {
                var skill = FindSkill(skills, category, tier);
                _moduleGrid.AddChild(MakeCell(category, tier, skill, lockedTiers, selectedModules, suitActivated));
            }
        }
    }

    private Control MakeCategoryHeader(string category)
    {
        var panel = new PanelContainer { MinSize = new Vector2(120, 50) };
        panel.PanelOverride = new StyleBoxFlat { BackgroundColor = CategoryColors[category] };
        panel.AddChild(new Label
        {
            Text = Loc.GetString($"spider-os-modules-{category.ToLower()}"),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Modulate = HeaderColor,
            ToolTip = null,
            StyleClasses = { StyleNano.StyleClassConsoleHeading },
        });
        return panel;
    }

    private Control MakeDivider(Color color)
    {
        var panel = new PanelContainer { MinSize = new Vector2(0, 2) };
        panel.PanelOverride = new StyleBoxFlat { BackgroundColor = color };
        return panel;
    }

    private Control MakeCell(
        string column,
        int tier,
        NinjaSkill? skill,
        HashSet<int> lockedTiers,
        Dictionary<int, NinjaSkillsCategory> selectedModules,
        bool suitActivated)
    {
        var holder = new PanelContainer { MinSize = new Vector2(80, 70) };
        holder.PanelOverride = new StyleBoxFlat { BackgroundColor = CategoryColors[column] };

        if (skill is not { } s)
        {
            return holder;
        }

        var button = new TextureButton
        {
            MinSize = new Vector2(64, 64),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        var iconOk = ApplyIcon(button, s);
        button.ToolTip = BuildTooltip(s);
        button.Disabled = !iconOk || lockedTiers.Contains(tier) || selectedModules.ContainsKey(tier) || suitActivated || _bootActive;

        var capturedTier = tier;
        button.OnPressed += _ => OnModuleSelected?.Invoke(capturedTier, s.Category);

        holder.AddChild(button);
        _moduleButtons.Add((button, tier));
        return holder;
    }

    private static NinjaSkill? FindSkill(List<NinjaSkill> skills, string column, int tier)
    {
        foreach (var skill in skills)
        {
            if (CategoryToColumn(skill.Category) == column && skill.Tier == tier)
            {
                return skill;
            }
        }

        return null;
    }

    private bool ApplyIcon(TextureButton button, NinjaSkill skill)
    {
        if (skill.Icon == null)
        {
            button.TextureNormal = null;
            return false;
        }

        var spriteSys = _sysMan.GetEntitySystem<SpriteSystem>();
        var texture = spriteSys.Frame0(skill.Icon);

        if (texture == null)
        {
            button.TextureNormal = null;
            return false;
        }

        button.TextureNormal = texture;
        return true;
    }

    private static string BuildTooltip(NinjaSkill skill)
    {
        var name = string.IsNullOrEmpty(skill.Name) ? string.Empty : Loc.GetString(skill.Name);
        var description = string.IsNullOrEmpty(skill.Description) ? string.Empty : Loc.GetString(skill.Description);

        if (string.IsNullOrEmpty(description))
            return name;

        return string.IsNullOrEmpty(name) ? description : $"{name}\n{description}";
    }

    private static string CategoryToColumn(NinjaSkillsCategory category) => category switch
    {
        NinjaSkillsCategory.Snake => "Snake",
        NinjaSkillsCategory.Steel => "Steel",
        _ => "Ghost",
    };

    private void UpdatePreview(NinjaColorway colorway, bool helmet, NinjaStyle style)
    {
        var color = colorway switch
        {
            NinjaColorway.Red => "red",
            NinjaColorway.Blue => "blue",
            _ => "green",
        };

        var head = helmet ? "helmet" : "scarf";

        if (style == NinjaStyle.New)
        {
            var newPath = new ResPath($"{PreviewBasePath}preview-{color}-new-helmet.png");
            if (_resCache.TryGetResource<TextureResource>(newPath, out var newPreview))
            {
                _stylePreview.Texture = newPreview.Texture;
                return;
            }

            ShowNoPreview();
            return;
        }

        var path = new ResPath($"{PreviewBasePath}preview-{color}-{head}.png");
        if (_resCache.TryGetResource<TextureResource>(path, out var preview))
        {
            _stylePreview.Texture = preview.Texture;
            return;
        }

        ShowNoPreview();
    }

    private void ShowNoPreview()
    {
        if (_resCache.TryGetResource<TextureResource>(new ResPath($"{PreviewBasePath}no-preview.png"), out var noPreview))
            _stylePreview.Texture = noPreview.Texture;
    }

    private static NinjaColorway ColorFromIndex(int index) => index switch
    {
        1 => NinjaColorway.Red,
        2 => NinjaColorway.Blue,
        _ => NinjaColorway.Green,
    };

    private static int IndexFromColor(NinjaColorway color) => color switch
    {
        NinjaColorway.Red => 1,
        NinjaColorway.Blue => 2,
        _ => 0,
    };

    private static NinjaStyle StyleFromIndex(int index) => index switch
    {
        1 => NinjaStyle.New,
        _ => NinjaStyle.Old,
    };

    private static int IndexFromStyle(NinjaStyle style) => style switch
    {
        NinjaStyle.New => 1,
        _ => 0,
    };
}