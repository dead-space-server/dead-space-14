using System.Collections.Generic;
using System.Linq;
// DS14-start: shared UI layout and style regression coverage
using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Content.Client.DeadSpace.UserInterface.Controls;
using Content.Client.Options.UI;
using Content.Client.PDA;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
// DS14-end
using Content.Client.LateJoin;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.Tests.UserInterface;

[TestFixture]
public sealed class UiControlTest
{
    // You should not be adding to this.
    private Type[] _ignored = new Type[]
    {
        typeof(LateJoinGui),
    };

    /// <summary>
    /// Tests that all windows can be instantiated successfully.
    /// </summary>
    [Test]
    public async Task TestWindows()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings()
        {
            Connected = true,
        });
        var activator = pair.Client.ResolveDependency<IDynamicTypeFactory>();
        var refManager = pair.Client.ResolveDependency<IReflectionManager>();
        var loader = pair.Client.ResolveDependency<IModLoader>();

        await pair.Client.WaitAssertion(() =>
        {
            foreach (var type in refManager.GetAllChildren(typeof(BaseWindow)))
            {
                if (type.IsAbstract || _ignored.Contains(type))
                    continue;

                if (!loader.IsContentType(type))
                    continue;

                // If it has no empty ctor then skip it instead of figuring out what args it needs.
                var ctor = type.GetConstructor(Type.EmptyTypes);

                if (ctor == null)
                    continue;

                // Don't inject because the control themselves have to do it.
                // DS14-start: exercise narrow/wide logical sizes corresponding to every supported UI scale
                var window = (BaseWindow) activator.CreateInstance(type, oneOff: true, inject: false);
                ValidateLayout(type, window);
                // DS14-end
            }
        });

        await pair.CleanReturnAsync();
    }

    // DS14-start: central controls must never regress to a pure-white interactive backing surface
    [Test]
    public async Task TestDeadSpaceInteractiveSurfaces()
    {
        var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();

        await pair.Client.WaitAssertion(() =>
        {
            var root = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Children =
                {
                    new OptionButton(),
                    new HeadedOptionButton(),
                },
            };
            ui.WindowRoot.AddChild(root);

            try
            {
                var option = (OptionButton) root.GetChild(0);
                option.AddItem("Обычная строка dropdown");
                var headed = (HeadedOptionButton) root.GetChild(1);
                headed.AddItem("Popup-строка HeadedOptionButton");

                var listButton = new ListContainerButton(new FixtureListData(), 0);
                listButton.AddChild(new Label { Text = "Цель выдачи" });
                root.AddChild(listButton);

                var pdaSettings = new PdaSettingsButton
                {
                    Text = "Беззвучный режим",
                    Description = "Проверка type-owned pseudo states",
                };
                var pdaProgram = new PdaProgramItem();
                pdaProgram.ProgramName.Text = "Мониторинг станции";
                root.AddChild(pdaSettings);
                root.AddChild(pdaProgram);

                var uncheckedBox = new CheckBox { Text = "false" };
                var checkedBox = new CheckBox { Text = "true", Pressed = true };
                root.AddChild(uncheckedBox);
                root.AddChild(checkedBox);

                var optionRow = new OptionDropDown { Title = "dropdown spacing" };
                optionRow.Button.AddItem("value");
                var sliderRow = new OptionSlider { Title = "slider spacing" };
                root.AddChild(optionRow);
                root.AddChild(sliderRow);

                var pseudoButtons = new[]
                {
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassHover),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassPressed),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassDisabled),
                };
                foreach (var button in pseudoButtons)
                    root.AddChild(button);

                var barePseudoButtons = new[]
                {
                    FixtureContainerButton.ForPseudo(ContainerButton.StylePseudoClassNormal),
                    FixtureContainerButton.ForPseudo(ContainerButton.StylePseudoClassHover),
                    FixtureContainerButton.ForPseudo(ContainerButton.StylePseudoClassPressed),
                    FixtureContainerButton.ForPseudo(ContainerButton.StylePseudoClassDisabled),
                };
                foreach (var button in barePseudoButtons)
                    root.AddChild(button);

                // Semantic colors and selected radio values must not alter the global button geometry.
                // This guards both the options footer (negative disabled -> enabled) and the job-priority rows.
                var semanticGeometryButtons = new[]
                {
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, StyleClass.Positive),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassDisabled, StyleClass.Positive),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, StyleClass.Negative),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassDisabled, StyleClass.Negative),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, DeadSpaceStyleClass.ControlPositive),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassDisabled, DeadSpaceStyleClass.ControlPositive),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, DeadSpaceStyleClass.ControlWarning),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassDisabled, DeadSpaceStyleClass.ControlWarning),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, DeadSpaceStyleClass.ControlDanger),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassDisabled, DeadSpaceStyleClass.ControlDanger),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassPressed, DeadSpaceStyleClass.JobPriorityPreferred),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassPressed, DeadSpaceStyleClass.JobPriorityNever),
                };
                var semanticGeometryRow = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                };
                foreach (var button in semanticGeometryButtons)
                    semanticGeometryRow.AddChild(button);
                root.AddChild(semanticGeometryRow);

                var actionGeometryButtons = new[]
                {
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, DeadSpaceStyleClass.Action),
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassPressed,
                        DeadSpaceStyleClass.Action,
                        DeadSpaceStyleClass.ActionPositive),
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassNormal,
                        DeadSpaceStyleClass.Action,
                        DeadSpaceStyleClass.Ready),
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassPressed,
                        DeadSpaceStyleClass.Action,
                        DeadSpaceStyleClass.Ready),
                };
                var actionGeometryRow = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                };
                foreach (var button in actionGeometryButtons)
                    actionGeometryRow.AddChild(button);
                root.AddChild(actionGeometryRow);

                var topActionGeometryButtons = new[]
                {
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, DeadSpaceStyleClass.TopAction),
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassNormal,
                        DeadSpaceStyleClass.TopAction,
                        StyleClass.Negative),
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassPressed,
                        DeadSpaceStyleClass.TopAction,
                        StyleClass.Negative),
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassDisabled,
                        DeadSpaceStyleClass.TopAction,
                        StyleClass.Negative),
                };
                var topActionGeometryRow = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                };
                foreach (var button in topActionGeometryButtons)
                    topActionGeometryRow.AddChild(button);
                root.AddChild(topActionGeometryRow);

                var unreadGeometryButtons = new[]
                {
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, DeadSpaceStyleClass.ListItem),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassNormal, DeadSpaceStyleClass.ListItemUnread),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassHover, DeadSpaceStyleClass.ListItemUnread),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassPressed, DeadSpaceStyleClass.ListItemUnread),
                    FixtureButton.ForPseudo(ContainerButton.StylePseudoClassDisabled, DeadSpaceStyleClass.ListItemUnread),
                };
                var unreadGeometryRow = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                };
                foreach (var button in unreadGeometryButtons)
                    unreadGeometryRow.AddChild(button);
                root.AddChild(unreadGeometryRow);

                var lateJoinGeometryButtons = new[]
                {
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassNormal,
                        LateJoinGui.GetJobRowStyleClass(0)),
                    FixtureButton.ForPseudo(
                        ContainerButton.StylePseudoClassNormal,
                        LateJoinGui.GetJobRowStyleClass(1)),
                };
                var lateJoinGeometryRow = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                };
                foreach (var button in lateJoinGeometryButtons)
                    lateJoinGeometryRow.AddChild(button);
                root.AddChild(lateJoinGeometryRow);

                root.ForceRunStyleUpdate();
                // This is a synthetic control catalogue rather than a viewport fixture; keep its test rows
                // unconstrained vertically so later controls are not measured against exhausted space.
                root.Measure(new Vector2(1280, 2000));
                root.Arrange(UIBox2.FromDimensions(Vector2.Zero, root.DesiredSize));

                AssertDarkInteractiveSurface(option, "OptionButton closed state");
                AssertDarkInteractiveSurface(headed, "HeadedOptionButton closed state");
                AssertDarkInteractiveSurface(headed._buttonData[0].Button, "HeadedOptionButton popup row");
                AssertDarkInteractiveSurface(listButton, "ListContainerButton target row");
                Assert.That(listButton.StyleBoxOverride, Is.Null, "ListContainerButton must rely on the stylesheet");
                AssertDarkInteractiveSurface(pdaSettings, "PDA settings row");
                AssertDarkInteractiveSurface(pdaProgram, "PDA program row");
                Assert.That(pdaSettings.StyleBoxOverride, Is.Null, "PDA settings row must rely on its type sheetlet");
                Assert.That(pdaProgram.StyleBoxOverride, Is.Null, "PDA program row must rely on its type sheetlet");
                AssertTransparentInteractiveSurface(uncheckedBox, "unchecked CheckBox row");
                AssertTransparentInteractiveSurface(checkedBox, "checked CheckBox row");
                Assert.That(optionRow.DesiredSize.Y - optionRow.Button.DesiredSize.Y, Is.GreaterThanOrEqualTo(4));
                Assert.That(sliderRow.DesiredSize.Y - sliderRow.Slider.DesiredSize.Y, Is.GreaterThanOrEqualTo(4));
                foreach (var button in pseudoButtons)
                    AssertDarkInteractiveSurface(button, $"Button pseudo-state {string.Join(',', button.StylePseudoClass)}");
                foreach (var button in barePseudoButtons)
                    AssertDarkInteractiveSurface(button, $"bare ContainerButton pseudo-state {string.Join(',', button.StylePseudoClass)}");
                AssertSameDesiredHeight(semanticGeometryButtons, "semantic/priority button states");
                AssertSameDesiredHeight(actionGeometryButtons, "lobby action button states");
                AssertSameDesiredHeight(topActionGeometryButtons, "top action button states");
                AssertSameDesiredHeight(unreadGeometryButtons, "unread list button states");
                AssertSameDesiredHeight(lateJoinGeometryButtons, "late-join alternating rows");
                AssertSameContentMargins(lateJoinGeometryButtons, "late-join alternating rows");
            }
            finally
            {
                root.Orphan();
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void ValidateLayout(Type type, BaseWindow window)
    {
        var viewports = new[]
        {
            new Vector2(320, 180),
            new Vector2(1280, 720),
            new Vector2(1920, 1080),
        };
        var scales = new[] { 0.75f, 1f, 1.25f, 1.5f };

        foreach (var scale in scales)
        {
            foreach (var viewport in viewports)
            {
                var logicalSize = viewport / scale;
                window.Measure(logicalSize);
                AssertFinite(type, window, "measure");

                var finalSize = Vector2.Min(Vector2.Max(window.DesiredSize, Vector2.One), logicalSize);
                window.Arrange(UIBox2.FromDimensions(Vector2.Zero, finalSize));
                AssertFinite(type, window, $"arrange {viewport.X}x{viewport.Y} @ {scale}");
            }
        }
    }

    private static void AssertFinite(Type windowType, Control control, string stage)
    {
        Assert.Multiple(() =>
        {
            Assert.That(float.IsFinite(control.DesiredSize.X), Is.True, $"{windowType.Name}: DesiredSize.X at {stage}");
            Assert.That(float.IsFinite(control.DesiredSize.Y), Is.True, $"{windowType.Name}: DesiredSize.Y at {stage}");
            Assert.That(float.IsFinite(control.Size.X), Is.True, $"{windowType.Name}: Size.X at {stage}");
            Assert.That(float.IsFinite(control.Size.Y), Is.True, $"{windowType.Name}: Size.Y at {stage}");
            Assert.That(float.IsFinite(control.Position.X), Is.True, $"{windowType.Name}: Position.X at {stage}");
            Assert.That(float.IsFinite(control.Position.Y), Is.True, $"{windowType.Name}: Position.Y at {stage}");
            Assert.That(control.Size.X, Is.GreaterThanOrEqualTo(0), $"{windowType.Name}: negative width at {stage}");
            Assert.That(control.Size.Y, Is.GreaterThanOrEqualTo(0), $"{windowType.Name}: negative height at {stage}");
        });

        foreach (var child in control.Children)
            AssertFinite(windowType, child, stage);
    }

    private static void AssertDarkInteractiveSurface(ContainerButton button, string description)
    {
        button.ForceRunStyleUpdate();
        Assert.That(
            button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var styleBox),
            Is.True,
            $"{description} has no stylesheet surface");
        Assert.That(styleBox, Is.TypeOf<StyleBoxFlat>(), $"{description} must use the flat DS baseline");

        var background = ((StyleBoxFlat) styleBox).BackgroundColor;
        var pureWhite = background.R >= 0.99f && background.G >= 0.99f && background.B >= 0.99f;
        Assert.That(pureWhite, Is.False, $"{description} resolved to a pure-white surface");
    }

    private static void AssertTransparentInteractiveSurface(ContainerButton button, string description)
    {
        button.ForceRunStyleUpdate();
        Assert.That(
            button.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var styleBox),
            Is.True,
            $"{description} has no stylesheet surface");
        Assert.That(
            styleBox,
            Is.TypeOf<StyleBoxEmpty>(),
            $"{description} must not draw a full-row selection background");
    }

    private static void AssertSameDesiredHeight(IReadOnlyList<Control> controls, string description)
    {
        var expected = controls[0].DesiredSize.Y;
        foreach (var control in controls)
        {
            control.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var resolvedBox);
            var margins = resolvedBox == null
                ? "none"
                : $"{resolvedBox.GetContentMargin(StyleBox.Margin.Top)}/" +
                  $"{resolvedBox.GetContentMargin(StyleBox.Margin.Bottom)}";
            Assert.That(
                control.DesiredSize.Y,
                Is.EqualTo(expected).Within(0.001f),
                $"{description} must not resize when its pseudo-state or semantic color changes " +
                $"(classes: {string.Join(',', control.StyleClasses)}; " +
                $"pseudo: {string.Join(',', control.StylePseudoClass)}; margins: {margins})");
        }
    }

    private static void AssertSameContentMargins(IReadOnlyList<Control> controls, string description)
    {
        controls[0].TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var expectedBox);
        Assert.That(expectedBox, Is.Not.Null, $"{description}: reference row has no stylesheet surface");

        foreach (var control in controls)
        {
            control.TryGetStyleProperty<StyleBox>(ContainerButton.StylePropertyStyleBox, out var actualBox);
            Assert.That(actualBox, Is.Not.Null, $"{description}: row has no stylesheet surface");
            foreach (var margin in new[]
                     {
                         StyleBox.Margin.Top,
                         StyleBox.Margin.Bottom,
                         StyleBox.Margin.Left,
                         StyleBox.Margin.Right,
                     })
            {
                Assert.That(
                    actualBox!.GetContentMargin(margin),
                    Is.EqualTo(expectedBox!.GetContentMargin(margin)).Within(0.001f),
                    $"{description}: {margin} content margin differs");
            }
        }
    }

    private sealed class FixtureButton : Button
    {
        public static FixtureButton ForPseudo(string pseudo, params string[] styleClasses)
        {
            var button = new FixtureButton { Text = "Geometry" };
            foreach (var styleClass in styleClasses)
                button.AddStyleClass(styleClass);
            button.SetOnlyStylePseudoClass(pseudo);
            return button;
        }
    }

    private sealed class FixtureContainerButton : ContainerButton
    {
        public static FixtureContainerButton ForPseudo(string pseudo)
        {
            var button = new FixtureContainerButton
            {
                Children = { new Label { Text = pseudo } },
            };
            button.SetOnlyStylePseudoClass(pseudo);
            return button;
        }
    }

    private sealed record FixtureListData : ListData;
    // DS14-end
}
