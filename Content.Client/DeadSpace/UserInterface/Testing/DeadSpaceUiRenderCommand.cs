// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

#if DEBUG

using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client.Administration.UI.Bwoink;
using Content.Client.Administration.UI.Tabs.AdminTab;
using Content.Client.Cargo.UI;
using Content.Client.Chemistry.UI;
using Content.Client.Communications.UI;
using Content.Client.Communications.UI.Widgets;
using Content.Client.DeadSpace.Photocopier.UI;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.DeadSpace.UserInterface.Controls;
using Content.Client.Fax.UI;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Options.UI;
using Content.Client.Options.UI.Tabs;
using Content.Client.PDA;
using Content.Client.Power.APC.UI;
using Content.Client.SmartFridge;
using Content.Client.Store.Ui;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Client.VendingMachines.UI;
using Content.Shared.Administration;
using Content.Shared.DeadSpace.Photocopier;
using Content.Shared.Fax;
using Content.Shared.Preferences;
using Content.Shared.SmartFridge;
using Content.Shared.StatusIcon;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.Utility;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Content.Client.DeadSpace.UserInterface.Testing;

/// <summary>
/// Developer-only deterministic UI fixture renderer. The runner launches one fixture per client process.
/// </summary>
[AnyCommand]
public sealed class DeadSpaceUiRenderCommand : IConsoleCommand
{
    private static readonly ResPath OutputDirectory = new("/Screenshots/DS14UI");

    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IGameController _gameController = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private BaseWindow? _window;
    private IConsoleShell? _shell;
    private string _outputName = string.Empty;
    private int _layoutFrames;
    private bool _capturing;

    public string Command => "ds14_ui_render";
    public string Description => "Render a deterministic DS14 UI fixture to user data and quit.";
    public string Help => "ds14_ui_render <palette|dropdowns|list-container|vending|smart-fridge|store|lathe|reagent-dispenser|cargo|atmos-power|pda|pda-overflow|photocopier|admin|ahelp|debug-console|appearance-traits|server-list|late-join|role-priorities|options-general|options-footer|ert-admin|ert-admin-pending|ert-admin-manual|ert-admin-codes|fax|communications|chat> [output-name]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_window != null || args.Length is < 1 or > 2)
        {
            shell.WriteError(Help);
            return;
        }

        var fixture = args[0].ToLowerInvariant();
        _outputName = SanitizeFileName(args.Length == 2 ? args[1] : fixture);
        _shell = shell;

        try
        {
            _window = CreateFixture(fixture);
            // The root is not arranged yet when a startup console command runs, so centering here can place
            // the window off-screen. Open first and recenter from the first post-draw callback.
            _window.Open();
            _ui.OnPostDrawUIRoot += OnPostDrawUiRoot;
            shell.WriteLine($"DS14 UI fixture '{fixture}' opened; waiting for layout.");
        }
        catch (Exception exception)
        {
            shell.WriteError($"Unable to create fixture '{fixture}': {exception}");
            _gameController.Shutdown("DS14 UI render fixture failed");
        }
    }

    private BaseWindow CreateFixture(string fixture)
    {
        // Keep factories explicit: content assemblies run in the Robust sandbox and cannot use Activator.
        return fixture switch
        {
            "palette" or "dropdowns" or "list-container" => new DeadSpaceUiFixtureWindow(fixture),
            "vending" => CreateVendingFixture(),
            "smart-fridge" => CreateSmartFridgeFixture(),
            "store" => new StoreMenu(),
            "lathe" => CreateLatheFixture(),
            "reagent-dispenser" => new ReagentDispenserWindow(),
            "cargo" => new CargoShuttleMenu(),
            "atmos-power" => CreateApcFixture(),
            "pda" => CreatePdaFixture(),
            "pda-overflow" => CreatePdaOverflowFixture(),
            "photocopier" => CreatePhotocopierFixture(),
            "admin" => CreateAdminFixture(),
            "ahelp" => CreateAHelpFixture(),
            "debug-console" => CreateDebugConsoleFixture(),
            "appearance-traits" => CreateAppearanceTraitsFixture(),
            "server-list" => CreateServerListFixture(),
            "late-join" => CreateLateJoinFixture(),
            "role-priorities" => CreateRolePriorityFixture(),
            "options-general" => CreateOptionsGeneralFixture(),
            "options-footer" => CreateOptionsFooterFixture(),
            "ert-admin" => CreateErtAdminFixture(mainTab: 0, requestTab: 1),
            "ert-admin-pending" => CreateErtAdminFixture(mainTab: 0, requestTab: 0),
            "ert-admin-manual" => CreateErtAdminFixture(mainTab: 0, requestTab: 2),
            "ert-admin-codes" => CreateErtAdminFixture(mainTab: 1, requestTab: 0),
            "fax" => CreateFaxFixture(),
            "communications" => CreateCommunicationsFixture(),
            "chat" => CreateChatFixture(),
            _ => throw new ArgumentException($"Unknown fixture '{fixture}'.", nameof(fixture)),
        };
    }

    private static BaseWindow CreateVendingFixture()
    {
        var window = new VendingMachineMenu();
        window.PopulateRenderFixture(
        [
            new VendingMachineRenderListData("[4] Банка колы — выдача в правую руку", false),
            new VendingMachineRenderListData("[1] Очень длинное название товара для проверки сжатия", false),
            new VendingMachineRenderListData("[0] Товар закончился", true),
        ]);
        return window;
    }

    private static BaseWindow CreateSmartFridgeFixture()
    {
        var window = new SmartFridgeMenu();
        window.PopulateRenderFixture(
        [
            new SmartFridgeListData(EntityUid.Invalid, new SmartFridgeEntry("Пакет донорской крови O−"), 4),
            new SmartFridgeListData(
                EntityUid.Invalid,
                new SmartFridgeEntry("Очень длинное название предмета для проверки сжатия"),
                1),
            new SmartFridgeListData(EntityUid.Invalid, new SmartFridgeEntry("Пустая позиция"), 0),
        ]);
        return window;
    }

    private static BaseWindow CreateLatheFixture()
    {
        var window = FixtureWindow("Протолат — производственный интерфейс", new Vector2(750, 500));
        var root = Horizontal(8);
        root.VerticalExpand = true;

        var recipes = Vertical(6);
        recipes.SizeFlagsStretchRatio = 1.15f;
        var search = Horizontal(6);
        search.AddChild(new LineEdit
        {
            PlaceHolder = "Поиск чертежей",
            HorizontalExpand = true,
        });
        var filter = new OptionButton { MinWidth = 140 };
        filter.AddItem("Все категории");
        filter.AddItem("Инструменты");
        filter.AddItem("Электроника");
        search.AddChild(filter);
        recipes.AddChild(search);
        recipes.AddChild(SectionLabel("Рецепты: доступный и недоступный"));
        var recipeStates = Vertical(3);
        recipeStates.AddChild(MachineRecipeRow("Батарея малой ёмкости", false));
        recipeStates.AddChild(MachineRecipeRow("Батарея средней ёмкости", true));
        recipeStates.AddChild(MachineRecipeRow(
            "Очень длинное название недоступного производственного рецепта",
            true));
        recipes.AddChild(recipeStates);
        var amount = Horizontal(6);
        amount.AddChild(new Label { Text = "Количество" });
        amount.AddChild(new LineEdit { Text = "1", HorizontalExpand = true });
        amount.AddChild(new Button { Text = "Изготовить" });
        recipes.AddChild(amount);

        var queue = Vertical(6);
        queue.AddChild(SectionLabel("Очередь производства"));
        queue.AddChild(new PanelContainer
        {
            StyleClasses = { StyleClass.Positive },
            Children = { new Label { Text = "Изготавливается: лист стали", Margin = new Thickness(8, 5) } },
        });
        queue.AddChild(InsetList("2× стекло — ожидает", "1× кабель — ожидает"));
        queue.AddChild(SectionLabel("Материалы"));
        queue.AddChild(new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.Inset },
            VerticalExpand = true,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Margin = new Thickness(8),
                    SeparationOverride = 6,
                    Children =
                    {
                        new Label { Text = "Сталь: 1250" },
                        new Label { Text = "Стекло: 640" },
                        new Label { Text = "Плазма: 80" },
                    },
                },
            },
        });

        root.AddChild(recipes);
        root.AddChild(queue);
        window.Contents.AddChild(root);
        return window;
    }

    private static BaseWindow CreateApcFixture()
    {
        var window = new ApcMenu();
        window.FindControl<SwitchButton>("BreakerButton").Pressed = true;
        return window;
    }

    private static BaseWindow CreatePdaFixture()
    {
        var window = new PdaWindow
        {
            SetSize = new Vector2(576, 450),
            MinSize = new Vector2(576, 450),
            BorderColor = DeadSpaceStylePalette.SurfaceStatus.ToHex(),
            AccentHColor = DeadSpaceStylePalette.AccentDim.ToHex(),
        };
        var root = Vertical(6);
        var navigation = Horizontal(0);
        navigation.AddChild(new PdaNavigationButton
        {
            LabelText = "Домой",
            IsCurrent = true,
            MinWidth = 88,
        });
        navigation.AddChild(new PdaNavigationButton
        {
            LabelText = "Программы",
            MinWidth = 112,
        });
        navigation.AddChild(new PdaNavigationButton
        {
            LabelText = "Настройки",
            MinWidth = 112,
        });
        navigation.AddChild(new Control { HorizontalExpand = true });
        root.AddChild(navigation);

        var body = Horizontal(6);
        body.VerticalExpand = true;
        var home = Vertical(2);
        home.AddStyleClass("PdaHomeSummary");
        home.AddChild(BareRow("Владелец: Айзек Кларк", ContainerButton.StylePseudoClassHover));
        home.AddChild(BareRow("ID: инженер"));
        home.AddChild(BareRow("Станция: Ишимура"));
        home.AddChild(BareRow("Тревога: зелёный уровень"));
        home.AddChild(BareRow("Смена: 00:42:17 · 26.08.2710"));
        home.AddChild(BareRow("Инструкции: выполняйте свою работу"));

        var programs = Vertical(4);
        programs.HorizontalExpand = false;
        programs.MinWidth = 285;
        programs.MaxWidth = 285;
        programs.AddChild(new PdaSettingsButton
        {
            Text = "Экипаж станции",
            Description = "Состав и состояние экипажа",
        });
        programs.AddChild(new PdaSettingsButton
        {
            Text = "Беззвучный режим",
            Description = "Отключает звуки уведомлений КПК",
        });
        programs.AddChild(new PdaSettingsButton
        {
            Text = "Мелодия звонка",
            Description = "Сигнал входящих сообщений",
        });

        body.AddChild(home);
        body.AddChild(programs);
        root.AddChild(body);
        window.ContentsContainer.AddChild(root);
        return window;
    }

    private static BaseWindow CreatePdaOverflowFixture()
    {
        var window = new PdaWindow
        {
            SetSize = new Vector2(576, 450),
            MinSize = new Vector2(576, 450),
            BorderColor = "#6F6958",
        };
        var root = Vertical(0);

        var navigation = Horizontal(0);
        navigation.MinHeight = 32;
        navigation.AddChild(new PdaNavigationButton { LabelText = "Домой", IsCurrent = true, MinWidth = 36 });
        navigation.AddChild(new PdaNavigationButton { LabelText = "Программы", MinWidth = 112 });
        navigation.AddChild(new PdaNavigationButton { LabelText = "Настройки", MinWidth = 112 });
        navigation.AddChild(new Control { HorizontalExpand = true });
        root.AddChild(navigation);

        var home = Vertical(2);
        home.VerticalExpand = false;
        home.Margin = new Thickness(8);
        home.AddStyleClass("PdaHomeSummary");
        home.AddChild(PdaSummaryRow("Владелец: Грин Хейтблад"));
        home.AddChild(PdaSummaryRow("ID: Грин Хейтблад, Инженер-стажёр"));
        home.AddChild(PdaSummaryRow("Станция: NTB Fland NX-132"));
        home.AddChild(PdaSummaryRow("Уровень угрозы: Красный"));
        home.AddChild(PdaSummaryRow("Смена: 01:02:57 · 28.08.2710"));
        home.AddChild(PdaSummaryRow(
            "Инструкции: Весь экипаж, за исключением должностных лиц и тех персонала, не может посещать технические тоннели и обязан находиться в пределах коридоров станции на протяжении Красного кода. Экипаж обязан подчиняться правомерным приказам сотрудников службы безопасности. Посетители и гости станции обязаны находиться в баре, библиотеке или жилых отсеках. Весь персонал обязан носить свои ID-карты в своих КПК и хранить при себе в специально отведённом месте на поясе."));

        root.AddChild(new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            VScrollEnabled = true,
            Children = { home },
        });

        var footer = new BoxContainer
        {
            SetHeight = 28,
            HorizontalExpand = true,
            Children =
            {
                new StripeBack
                {
                    HasBottomEdge = false,
                    HasMargins = false,
                    HorizontalExpand = true,
                    Children =
                    {
                        new Label
                        {
                            Text = "Robust#OS ™",
                            Margin = new Thickness(6, 0),
                            VerticalAlignment = Control.VAlignment.Center,
                            StyleClasses = { "PdaContentFooterText" },
                        },
                        new Label
                        {
                            Text = "КОНС-3B9A-89D4",
                            Margin = new Thickness(6, 0),
                            VerticalAlignment = Control.VAlignment.Center,
                            HorizontalAlignment = Control.HAlignment.Right,
                            StyleClasses = { "PdaContentFooterText" },
                        },
                    },
                },
            },
        };
        root.AddChild(footer);
        window.ContentsContainer.AddChild(root);
        return window;
    }

    private static ContainerButton PdaSummaryRow(string text)
    {
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            Margin = new Thickness(6, 3),
        };
        label.SetMessage(text);
        return new ContainerButton
        {
            HorizontalExpand = true,
            Children = { label },
        };
    }

    private static BaseWindow CreatePhotocopierFixture()
    {
        var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
        var window = new PhotocopierWindow(prototypeManager);
        var forms = window.PaperworkFormPrototypes.ToList();
        if (forms.Count == 0)
            return window;

        var selected = forms[0];
        var categories = new HashSet<PhotocopierFormCategory>(forms.Select(form => form.Category));
        window.UpdateState(new PhotocopierUiState(
            canPrint: true,
            isPaperInserted: false,
            chosenForm: selected.ID,
            mode: PhotocopierMode.Print,
            allowedFormCategories: categories,
            wasEmagged: false,
            tonerLeft: 80,
            maxTonerAmount: 100));
        return window;
    }

    private static BaseWindow CreateAdminFixture()
    {
        var window = FixtureWindow("Администрирование", new Vector2(780, 430));
        var tabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        var players = Horizontal(8);
        players.VerticalExpand = true;
        var playerList = Vertical(6);
        playerList.AddChild(new LineEdit { PlaceHolder = "Поиск игрока", HorizontalExpand = true });
        playerList.AddChild(InsetList("JoeGenero", "Dead Space Engineer", "Наблюдатель"));
        var details = Vertical(6);
        details.AddChild(SectionLabel("Карточка игрока"));
        details.AddChild(new Label { Text = "Статус: подключён" });
        details.AddChild(new Label { Text = "Роль: инженер" });
        details.AddChild(new Label { Text = "Время в раунде: 00:31:08" });
        var actions = Horizontal(6);
        actions.AddChild(new Button { Text = "Сообщение", HorizontalExpand = true });
        actions.AddChild(new Button
        {
            Text = "Кик",
            HorizontalExpand = true,
            StyleClasses = { StyleClass.Negative },
        });
        details.AddChild(actions);
        players.AddChild(playerList);
        players.AddChild(new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.Inset },
            HorizontalExpand = true,
            Children = { details },
        });

        tabs.AddChild(players);
        tabs.AddChild(new Label { Text = "Управление раундом", Margin = new Thickness(12) });
        tabs.AddChild(new Label { Text = "Объекты и сущности", Margin = new Thickness(12) });
        tabs.SetTabTitle(0, "Игроки");
        tabs.SetTabTitle(1, "Раунд");
        tabs.SetTabTitle(2, "Объекты");
        window.Contents.AddChild(tabs);
        return window;
    }

    private static BaseWindow CreateAHelpFixture()
    {
        var window = FixtureWindow("AHelp — transcript, ввод и действия", new Vector2(760, 500));
        var root = Vertical(8);
        var panel = new BwoinkPanel(_ => { })
        {
            Margin = new Thickness(10),
            VerticalExpand = true,
        };
        var sentAt = new DateTime(2710, 8, 26, 14, 31, 0);
        panel.ReceiveLine(new SharedBwoinkSystem.BwoinkTextMessage(
            SharedBwoinkSystem.SystemUserId,
            SharedBwoinkSystem.SystemUserId,
            "[bold][color=#8CB7E8]Игрок:[/color][/bold] Не могу открыть шлюз инженерного отдела.",
            sentAt));
        panel.ReceiveLine(new SharedBwoinkSystem.BwoinkTextMessage(
            SharedBwoinkSystem.SystemUserId,
            SharedBwoinkSystem.SystemUserId,
            "[bold][color=#FFB347]Администратор:[/color][/bold] Проверяю доступ и логи взаимодействия.",
            sentAt.AddMinutes(1)));
        panel.FindControl<LineEdit>("SenderLineEdit").Text = "Поле ответа должно отличаться от истории сообщений";
        root.AddChild(panel);

        var actions = Horizontal(6);
        actions.AddChild(new Button { Text = "Заметки", HorizontalExpand = true });
        actions.AddChild(new ConfirmButton
        {
            Text = "Кикнуть",
            ConfirmationText = "Подтвердить",
            HorizontalExpand = true,
            StyleClasses = { DeadSpaceStyleClass.ControlDanger },
        });
        actions.AddChild(new Button
        {
            Text = "Забанить",
            HorizontalExpand = true,
            StyleClasses = { DeadSpaceStyleClass.ControlDanger },
        });
        actions.AddChild(new Button { Text = "Респаун", HorizontalExpand = true });
        root.AddChild(actions);
        window.Contents.AddChild(root);
        return window;
    }

    private static BaseWindow CreateDebugConsoleFixture()
    {
        var window = FixtureWindow("F1-консоль — служебные цвета", new Vector2(900, 420));
        var console = new DebugConsole
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        console.AddLine(
            FormattedMessage.FromUnformatted("[INFO] cfg: config saved to client_config.toml"),
            Robust.Shared.Maths.Color.Cyan);
        console.AddLine(
            FormattedMessage.FromUnformatted("> delaystart"),
            Robust.Shared.Maths.Color.Yellow);
        console.AddLine(
            FormattedMessage.FromUnformatted("Command delaystart with args: []"),
            Robust.Shared.Maths.Color.Red);
        console.AddLine(
            FormattedMessage.FromUnformatted("Начало раунда было приостановлено."),
            Robust.Shared.Maths.Color.Orange);
        window.Contents.AddChild(console);
        return window;
    }

    private static BaseWindow CreateAppearanceTraitsFixture()
    {
        var window = FixtureWindow("Черты внешности", new Vector2(920, 520));
        var columns = Horizontal(8);
        columns.VerticalExpand = true;

        var unusedColumn = Vertical(4);
        unusedColumn.HorizontalExpand = true;
        unusedColumn.AddChild(SectionLabel("Неиспользуемые черты"));
        var category = new OptionButton { HorizontalExpand = true };
        category.AddItem("Грудь");
        category.AddItem("Голова");
        unusedColumn.AddChild(category);
        unusedColumn.AddChild(new LineEdit { PlaceHolder = "Поиск", HorizontalExpand = true });
        var unused = new ItemList
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        unused.AddItem("Унатх, грудь (Подбрюшье)");
        unused.AddItem("Унатх, грудь (Тигр)");
        var unavailable = unused.AddItem("Унатх, грудь, шипы на спине (Четыре)");
        unavailable.Disabled = true;
        unusedColumn.AddChild(unused);

        var usedColumn = Vertical(4);
        usedColumn.HorizontalExpand = true;
        usedColumn.AddChild(SectionLabel("Используемые черты"));
        var used = new ItemList
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        used.AddItem("Унатх, грудь (Обычная)");
        usedColumn.AddChild(used);

        columns.AddChild(unusedColumn);
        columns.AddChild(usedColumn);
        window.Contents.AddChild(columns);
        return window;
    }

    private static BaseWindow CreateServerListFixture()
    {
        var window = FixtureWindow("Серверы Dead Space 14", new Vector2(900, 420));
        window.Contents.AddChild(new ServerListBox
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        });
        return window;
    }

    private static BaseWindow CreateRolePriorityFixture()
    {
        var window = FixtureWindow("Приоритеты должностей", new Vector2(1120, 460));
        var root = Vertical(8);
        root.AddChild(SectionLabel("Центральное командование"));

        var selections = new[]
        {
            ("Офицер «Синий Щит»", JobPriority.Never),
            ("Капитан", JobPriority.Low),
            ("Глава персонала", JobPriority.Medium),
            ("Старший инженер", JobPriority.High),
        };
        var items = new[]
        {
            ("humanoid-profile-editor-job-priority-never-button", (int) JobPriority.Never),
            ("humanoid-profile-editor-job-priority-low-button", (int) JobPriority.Low),
            ("humanoid-profile-editor-job-priority-medium-button", (int) JobPriority.Medium),
            ("humanoid-profile-editor-job-priority-high-button", (int) JobPriority.High),
        };

        foreach (var (title, selected) in selections)
        {
            var selector = new RequirementsSelector
            {
                UseJobPriorityColors = true,
                HorizontalExpand = true,
            };
            selector.Setup(items, title, 250, null);
            selector.Select((int) selected);
            root.AddChild(selector);
        }

        window.Contents.AddChild(root);
        return window;
    }

    private static BaseWindow CreateOptionsFooterFixture()
    {
        var window = FixtureWindow("Настройки — состояния нижней панели", new Vector2(1040, 300));
        var root = Vertical(14);
        root.AddChild(SectionLabel("Без изменений — кнопки отключены"));

        var disabled = new OptionsTabControlRow();
        SetOptionsFooterDisabled(disabled, true);
        root.AddChild(disabled);

        root.AddChild(SectionLabel("После изменения — кнопки включены, высота не меняется"));
        var enabled = new OptionsTabControlRow();
        SetOptionsFooterDisabled(enabled, false);
        root.AddChild(enabled);

        window.Contents.AddChild(root);
        return window;
    }

    private static void SetOptionsFooterDisabled(OptionsTabControlRow row, bool disabled)
    {
        row.FindControl<Button>("DefaultButton").Disabled = disabled;
        row.FindControl<Button>("ResetButton").Disabled = disabled;
        row.FindControl<Button>("ApplyButton").Disabled = disabled;
    }

    private static BaseWindow CreateErtAdminFixture(int mainTab, int requestTab)
    {
        var window = new ERTCallWindow(renderFixture: true);
        window.FindControl<TabContainer>("MainTabContainer").CurrentTab = mainTab;
        window.FindControl<TabContainer>("RequestsTabContainer").CurrentTab = requestTab;
        return window;
    }

    private static BaseWindow CreateLateJoinFixture()
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var window = FixtureWindow("Позднее присоединение", new Vector2(500, 620));
        var root = Vertical(8);

        var station = new OptionButton { HorizontalExpand = true };
        station.AddItem("NTVG Packed PR-499");
        root.AddChild(station);
        root.AddChild(new Button { Text = "Манифест экипажа", HorizontalExpand = true });

        var list = Vertical(0);
        AddLateJoinDepartment(list, prototypes, "Центральное Командование",
            new[]
            {
                ("Офицер «Синий Щит»", "JobIconBlueShieldOfficer"),
            });
        AddLateJoinDepartment(list, prototypes, "Командование",
            new[]
            {
                ("Капитан", "JobIconCaptain"),
                ("Глава персонала", "JobIconHeadOfPersonnel"),
                ("Старший инженер", "JobIconSeniorEngineer"),
                ("Главный врач", "JobIconChiefMedicalOfficer"),
                ("Глава службы безопасности", "JobIconHeadOfSecurity"),
                ("Квартирмейстер", "JobIconQuarterMaster"),
                ("Научный руководитель", "JobIconResearchDirector"),
                ("Агент внутренних дел", "JobIconIAA"),
            });

        root.AddChild(new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.Inset },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children =
            {
                new ScrollContainer
                {
                    HorizontalExpand = true,
                    VerticalExpand = true,
                    HScrollEnabled = false,
                    Children = { list },
                },
            },
        });
        window.Contents.AddChild(root);
        return window;
    }

    private static void AddLateJoinDepartment(
        BoxContainer list,
        IPrototypeManager prototypes,
        string title,
        IReadOnlyList<(string Name, string Icon)> entries)
    {
        var department = Vertical(0);
        department.VerticalExpand = false;
        department.Margin = new Thickness(0, 0, 0, 12);
        department.AddChild(new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.ListHeader },
            Margin = new Thickness(0, 0, 0, 4),
            Children =
            {
                new Label
                {
                    Text = title,
                    Align = Label.AlignMode.Center,
                    HorizontalExpand = true,
                    StyleClasses = { DeadSpaceStyleClass.ListHeader },
                },
            },
        });

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var label = new Label
            {
                Text = $"{entry.Name} (1 доступна)",
                Margin = new Thickness(5, 0, 0, 0),
                VerticalAlignment = Control.VAlignment.Center,
            };
            var button = new JobButton(label, $"FixtureJob{index}", entry.Name, 1)
            {
                HorizontalExpand = true,
            };
            button.AddStyleClass(LateJoinGui.GetJobRowStyleClass(index));

            var row = Horizontal(6);
            var iconPrototype = prototypes.Index<JobIconPrototype>(entry.Icon);
            row.AddChild(LateJoinGui.CreateJobIcon(iconPrototype.Icon.Frame0()));
            row.AddChild(label);
            button.AddChild(row);
            department.AddChild(button);
        }

        list.AddChild(department);
    }

    private static BaseWindow CreateOptionsGeneralFixture()
    {
        var window = FixtureWindow("Настройки — общие", new Vector2(720, 620));
        window.Contents.AddChild(new MiscTab
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        });
        return window;
    }

    private static BaseWindow CreateFaxFixture()
    {
        var window = new FaxWindow();
        // Keep this as explicit Add calls. Collection expressions for tuple lists are lowered to
        // CollectionsMarshal.SetCount by .NET 10, which is intentionally unavailable in the Robust sandbox.
        var history = new List<(string, string)>();
        history.Add(("00:00:00", "Получено: Неизвестно"));
        history.Add(("00:04:18", "Отправлено: Медицинский отдел"));

        window.UpdateState(new FaxUiState(
            "Офис капитана",
            new Dictionary<string, string>
            {
                ["medical"] = "Медицинский отдел",
                ["security"] = "Служба безопасности",
            },
            canSend: true,
            canCopy: true,
            isPaperInserted: false,
            destAddress: "medical",
            historyList: history));
        return window;
    }

    private BaseWindow CreateCommunicationsFixture()
    {
        var window = FixtureWindow("Консоль связи — экран", new Vector2(500, 460));
        var messaging = new MessagingControls(renderFixture: true)
        {
            CurrentTab = 1,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        messaging.FindControl<TextEdit>("ScreenMessageInput").TextRope =
            new Rope.Leaf("ПРОВЕРКА\nЭКРАНА");

        // A live SpriteView requires entity systems that do not exist in the standalone main-menu renderer.
        // Replace only the fixture preview with the exact RSI state used by ScreenDummy.
        var spriteView = messaging.FindControl<SpriteView>("BroadcastEntityDisplay");
        var spriteParent = spriteView.Parent!;
        var spritePosition = spriteView.GetPositionInParent();
        spriteParent.RemoveChild(spriteView);

        var screenRsi = _resourceCache
            .GetResource<RSIResource>(new ResPath("/Textures/Structures/Wallmounts/screen.rsi"))
            .RSI;
        var preview = new TextureRect
        {
            Texture = screenRsi["screen"].Frame0,
            TextureScale = new Vector2(5, 5),
            Stretch = TextureRect.StretchMode.KeepCentered,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        spriteParent.AddChild(preview);
        preview.SetPositionInParent(spritePosition);

        messaging.Margin = new Thickness(8);
        window.Contents.AddChild(messaging);
        return window;
    }

    private static BaseWindow CreateChatFixture()
    {
        var window = FixtureWindow("Чат — прозрачность transcript 22%", new Vector2(650, 520));
        var stage = new LayoutContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Robust.Shared.Maths.Color.FromHex("#394A5C"),
            },
            Children =
            {
                new Label
                {
                    Text = "ФОН ПОД ТЕКСТОМ ДОЛЖЕН БЫТЬ ВИДЕН",
                    HorizontalAlignment = Control.HAlignment.Center,
                    VerticalAlignment = Control.VAlignment.Center,
                },
            },
        };
        LayoutContainer.SetAnchorPreset(backdrop, LayoutContainer.LayoutPreset.Wide);
        stage.AddChild(backdrop);

        var chat = new ResizableChatBox
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Margin = new Thickness(10),
        };
        chat.ChatWindowPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = DeadSpaceStylePalette.SurfaceTranscript.WithAlpha(0.22f),
        };
        chat.AddLine("АДМИН: Проверка важного системного сообщения", Robust.Shared.Maths.Color.FromHex("#FF4A4A"));
        chat.AddLine("Добро пожаловать на Космическую Станцию 14!", Robust.Shared.Maths.Color.FromHex("#FFB347"));
        chat.AddLine("[bold][color=#DA8BC9]Капитан Айзек Кларк:[/color][/bold] Работа продолжается в штатном режиме.", Robust.Shared.Maths.Color.FromHex("#D7DEE8"));
        chat.AddLine("[bold][color=#8BDA8E]Николь Бреннан:[/color][/bold] Цвет ника остаётся читаемым в обеих темах.", Robust.Shared.Maths.Color.FromHex("#D7DEE8"));
        chat.AddLine("[bold][color=#8CB7E8]Кендра Дэниелс:[/color][/bold] Ещё один цвет из ChatNames.", Robust.Shared.Maths.Color.FromHex("#D7DEE8"));
        LayoutContainer.SetAnchorPreset(chat, LayoutContainer.LayoutPreset.Wide);
        stage.AddChild(chat);
        window.Contents.AddChild(stage);
        return window;
    }

    private static DefaultWindow FixtureWindow(string title, Vector2 size)
    {
        return new DefaultWindow
        {
            Title = title,
            MinSize = size,
            SetSize = size,
        };
    }

    private static BoxContainer Vertical(int separation)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = separation,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
    }

    private static BoxContainer Horizontal(int separation)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = separation,
            HorizontalExpand = true,
        };
    }

    private static Label SectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            StyleClasses = { DeadSpaceStyleClass.SectionTitle },
        };
    }

    private static Button MachineRecipeRow(string text, bool disabled)
    {
        var content = Horizontal(4);
        content.AddChild(new Label { Text = "▣", MinWidth = 24 });
        content.AddChild(new Label
        {
            Text = text,
            HorizontalExpand = true,
            ClipText = true,
        });

        return new Button
        {
            StyleClasses = { StyleClass.ButtonSquare },
            HorizontalExpand = true,
            Disabled = disabled,
            Children = { content },
        };
    }

    private static PanelContainer InsetList(params string[] rows)
    {
        var content = Vertical(3);
        content.Margin = new Thickness(5);
        foreach (var row in rows)
            content.AddChild(BareRow(row));

        return new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.Inset },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { content },
        };
    }

    private static ContainerButton BareRow(string text, string? pseudo = null)
    {
        var button = new FixtureContainerButton
        {
            HorizontalExpand = true,
            Children =
            {
                new Label
                {
                    Text = text,
                    Margin = new Thickness(7, 4),
                    ClipText = true,
                },
            },
        };
        if (pseudo != null)
            button.ForcePseudo(pseudo);
        return button;
    }

    private void OnPostDrawUiRoot(PostDrawUIRootEventArgs args)
    {
        if (_window == null || args.Root != _window.Root || _capturing)
            return;

        if (_layoutFrames++ == 0)
        {
            _window.RecenterWindow(new Vector2(0.5f, 0.5f));
            return;
        }

        // Let styles, fonts and nested XAML controls settle after the deferred recenter.
        if (_layoutFrames < 4)
            return;

        _capturing = true;
        _ui.OnPostDrawUIRoot -= OnPostDrawUiRoot;

        var crop = UIBox2i.FromDimensions(_window.GlobalPixelPosition, _window.PixelSize);
        var rootSize = _window.Root?.PixelSize ?? crop.Size;
        _shell?.WriteLine($"DS14_UI_RENDER_CROP pos={crop.TopLeft} size={crop.Size} root={rootSize}");

        // Clyde currently ignores a screenshot sub-region's offset and only applies its size.
        // Capture the whole framebuffer, then translate the UI-root rectangle and crop it here.
        _clyde.Screenshot(ScreenshotType.Final, image => SaveAndQuit(image, crop, rootSize));
    }

    private void SaveAndQuit<T>(Image<T> image, UIBox2i crop, Vector2i rootSize)
        where T : unmanaged, SixLabors.ImageSharp.PixelFormats.IPixel<T>
    {
        try
        {
            var scaleX = rootSize.X > 0 ? (float) image.Width / rootSize.X : 1f;
            var scaleY = rootSize.Y > 0 ? (float) image.Height / rootSize.Y : 1f;
            var left = Math.Clamp((int) MathF.Floor(crop.Left * scaleX), 0, image.Width - 1);
            var top = Math.Clamp((int) MathF.Floor(crop.Top * scaleY), 0, image.Height - 1);
            var right = Math.Clamp((int) MathF.Ceiling(crop.Right * scaleX), left + 1, image.Width);
            var bottom = Math.Clamp((int) MathF.Ceiling(crop.Bottom * scaleY), top + 1, image.Height);
            var rectangle = Rectangle.FromLTRB(left, top, right, bottom);

            _resourceManager.UserData.CreateDir(OutputDirectory);
            var path = OutputDirectory / $"{_outputName}.png";
            using var stream = _resourceManager.UserData.Open(path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var cropped = image.Clone(context => context.Crop(rectangle));
            cropped.SaveAsPng(stream);
            _shell?.WriteLine(
                $"DS14_UI_RENDER_OK {path} {cropped.Width}x{cropped.Height} framebuffer={image.Width}x{image.Height}");
        }
        catch (Exception exception)
        {
            _shell?.WriteError($"DS14_UI_RENDER_FAILED {exception}");
        }
        finally
        {
            _window?.Close();
            _window = null;
            _gameController.Shutdown("DS14 UI render complete");
        }
    }

    private static string SanitizeFileName(string name)
    {
        var safe = new string(name.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "fixture" : safe;
    }

    private sealed class FixtureContainerButton : ContainerButton
    {
        public void ForcePseudo(string pseudo)
        {
            SetOnlyStylePseudoClass(pseudo);
        }
    }
}

internal sealed class DeadSpaceUiFixtureWindow : DefaultWindow
{
    public DeadSpaceUiFixtureWindow(string fixture)
    {
        Title = $"DS14 UI fixture — {fixture}";
        MinSize = new Vector2(720, 520);
        SetSize = new Vector2(760, 620);

        Contents.AddChild(fixture switch
        {
            "palette" => CreatePaletteFixture(),
            "dropdowns" => CreateDropdownFixture(),
            "list-container" => CreateListFixture(),
            _ => throw new ArgumentOutOfRangeException(nameof(fixture)),
        });
    }

    private static Control CreatePaletteFixture()
    {
        var root = VerticalRoot();
        root.AddChild(Section("Surfaces"));

        var surfaces = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
        surfaces.AddChild(Surface("Surface", DeadSpaceStyleClass.Surface));
        surfaces.AddChild(Surface("Surface dark", DeadSpaceStyleClass.SurfaceDark));
        surfaces.AddChild(Surface("Inset", DeadSpaceStyleClass.Inset));
        root.AddChild(surfaces);

        root.AddChild(Section("Base button pseudo-states"));
        root.AddChild(StateRow(null));
        root.AddChild(Section("Action pseudo-states"));
        root.AddChild(StateRow(DeadSpaceStyleClass.Action));

        root.AddChild(Section("Top actions on section header"));
        var header = new PanelContainer
        {
            StyleClasses = { DeadSpaceStyleClass.SectionHeader },
            HorizontalExpand = true,
        };
        var headerActions = HorizontalRow();
        headerActions.Margin = new Thickness(8);
        headerActions.AddChild(Button("Статистика", DeadSpaceStyleClass.TopAction));
        headerActions.AddChild(Button("Правила", DeadSpaceStyleClass.TopAction));
        headerActions.AddChild(Button("Закрыть", DeadSpaceStyleClass.TopAction));
        header.AddChild(headerActions);
        root.AddChild(header);

        root.AddChild(Section("Semantic states"));
        var semantic = HorizontalRow();
        semantic.AddChild(Button("Accent", DeadSpaceStyleClass.ControlAccent));
        semantic.AddChild(Button("Positive", DeadSpaceStyleClass.ControlPositive));
        semantic.AddChild(Button("Warning", DeadSpaceStyleClass.ControlWarning));
        semantic.AddChild(Button("Negative", DeadSpaceStyleClass.ControlDanger));
        root.AddChild(semantic);

        return Scroll(root);
    }

    private static Control CreateDropdownFixture()
    {
        var root = VerticalRoot();
        root.AddChild(Section("Closed dropdown controls"));

        var option = new OptionButton { HorizontalExpand = true };
        option.AddItem("Long ru-RU option: инженер атмосферного отдела");
        option.AddItem("Second option");
        root.AddChild(option);

        var headed = new HeadedOptionButton { HorizontalExpand = true };
        headed.AddItem("Headed option — обычная строка");
        headed.AddItem("Headed option — длинная строка локализации");
        root.AddChild(headed);

        root.AddChild(Section("Popup row states (same global Button baseline)"));
        root.AddChild(StateRow(null));

        root.AddChild(Section("Input controls"));
        root.AddChild(new LineEdit
        {
            Text = "Обычное поле ввода",
            HorizontalExpand = true,
        });
        root.AddChild(new TextEdit
        {
            TextRope = new Rope.Leaf("Многострочное поле\nс длинной ru-RU локализацией без отдельного style class."),
            MinHeight = 100,
            HorizontalExpand = true,
        });

        root.AddChild(Section("Options rows (4 px vertical rhythm)"));
        var optionRow = new OptionDropDown { Title = "Тема HUD:" };
        optionRow.Button.AddItem("По умолчанию");
        root.AddChild(optionRow);
        root.AddChild(new OptionSlider { Title = "Громкость интерфейса:" });

        root.AddChild(Section("Checkbox values (same neutral row surface)"));
        var checkBoxes = HorizontalRow();
        checkBoxes.AddChild(new CheckBox
        {
            Text = "False — выключено",
            HorizontalExpand = true,
        });
        checkBoxes.AddChild(new CheckBox
        {
            Text = "True — включено",
            Pressed = true,
            HorizontalExpand = true,
        });
        root.AddChild(checkBoxes);
        return root;
    }

    private static Control CreateListFixture()
    {
        var root = VerticalRoot();
        root.AddChild(Section("ListContainer target rows"));
        root.AddChild(new Label
        {
            Text = "Эти строки используют настоящий ListContainerButton и не должны становиться белыми.",
        });

        var list = new ListContainer
        {
            MinHeight = 300,
            HorizontalExpand = true,
            VerticalExpand = true,
            GenerateItem = (data, button) =>
            {
                var entry = (FixtureListData) data;
                button.AddChild(new Label
                {
                    Text = entry.Text,
                    HorizontalExpand = true,
                });
                button.Disabled = entry.Disabled;
            },
        };
        list.PopulateList(
        [
            new FixtureListData("Цель выдачи: правая рука", false),
            new FixtureListData("Цель выдачи: левый карман", false),
            new FixtureListData("Недоступная строка", true),
            new FixtureListData("Очень длинная строка цели для проверки сжатия и clipping", false),
        ]);
        root.AddChild(list);
        return root;
    }

    private static BoxContainer VerticalRoot()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
    }

    private static BoxContainer HorizontalRow()
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            HorizontalExpand = true,
        };
    }

    private static Label Section(string text)
    {
        return new Label
        {
            Text = text,
            StyleClasses = { DeadSpaceStyleClass.SectionTitle },
        };
    }

    private static PanelContainer Surface(string text, string styleClass)
    {
        return new PanelContainer
        {
            StyleClasses = { styleClass },
            HorizontalExpand = true,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    Children =
                    {
                        new Label { Text = text },
                        new Label { Text = "Aa Бб 0123", StyleClasses = { DeadSpaceStyleClass.Subtitle } },
                    },
                },
            },
        };
    }

    private static BoxContainer StateRow(string? styleClass)
    {
        var row = HorizontalRow();
        row.AddChild(Button("Normal", styleClass));
        row.AddChild(Button("Hovered", styleClass, ContainerButton.StylePseudoClassHover));
        row.AddChild(Button("Pressed", styleClass, ContainerButton.StylePseudoClassPressed));
        row.AddChild(Button("Disabled", styleClass, ContainerButton.StylePseudoClassDisabled));
        return row;
    }

    private static FixtureButton Button(string text, string? styleClass, string? pseudo = null)
    {
        var button = new FixtureButton
        {
            Text = text,
            HorizontalExpand = true,
        };
        if (styleClass != null)
            button.AddStyleClass(styleClass);
        if (pseudo != null)
            button.ForcePseudo(pseudo);
        return button;
    }

    private static ScrollContainer Scroll(Control child)
    {
        return new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { child },
        };
    }

    private sealed class FixtureButton : Button
    {
        public void ForcePseudo(string pseudo)
        {
            SetOnlyStylePseudoClass(pseudo);
        }
    }

    private sealed record FixtureListData(string Text, bool Disabled) : ListData;
}

#endif
