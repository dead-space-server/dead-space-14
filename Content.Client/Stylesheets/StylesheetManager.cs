using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.DeadSpace.Stylesheets; // DS14
using Content.Client.Stylesheets.Stylesheets;
using Content.Shared.DeadSpace.CCCCVars; // DS14
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration; // DS14
using Robust.Shared.Reflection;

namespace Content.Client.Stylesheets
{
    public sealed class StylesheetManager : IStylesheetManager
    {
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private readonly IReflectionManager _reflection = default!;
        [Dependency] private readonly IConfigurationManager _configuration = default!; // DS14

        [Dependency]
        private readonly IResourceCache
            _resCache = default!; // TODO: REMOVE (obsolete; used to construct StyleNano/StyleSpace)

        public Stylesheet SheetNanotrasen { get; private set; } = default!;
        public Stylesheet SheetSystem { get; private set; } = default!;

        [Obsolete("Update to use SheetNanotrasen instead")]
        public Stylesheet SheetNano { get; private set; } = default!;

        [Obsolete("Update to use SheetSystem instead")]
        public Stylesheet SheetSpace { get; private set; } = default!;

        private Dictionary<string, Stylesheet> Stylesheets { get; set; } = default!;

        public bool TryGetStylesheet(string name, [MaybeNullWhen(false)] out Stylesheet stylesheet)
        {
            return Stylesheets.TryGetValue(name, out stylesheet);
        }

        public HashSet<Type> UnusedSheetlets { get; private set; } = [];

        public void Initialize()
        {
            var sawmill = _logManager.GetSawmill("style");
            sawmill.Debug("Initializing Stylesheets...");
            var sw = Stopwatch.StartNew();

            // add all sheetlets to the hashset
            var tys = _reflection.FindTypesWithAttribute<CommonSheetletAttribute>();
            UnusedSheetlets = [..tys];

            Stylesheets = new Dictionary<string, Stylesheet>();
            // DS14-start
            SelectInterfaceStyle(_configuration.GetCVar(CCCCVars.InterfaceStyle));
            // DS14-end
            SheetNanotrasen = Init(new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetSystem = Init(new SystemStylesheet(new BaseStylesheet.NoConfig(), this));
            SheetNano = new StyleNano(_resCache).Stylesheet; // TODO: REMOVE (obsolete)
            SheetSpace = new StyleSpace(_resCache).Stylesheet; // TODO: REMOVE (obsolete)

            _userInterfaceManager.Stylesheet = SheetNanotrasen;
            ApplyDebugConsoleTheme(_userInterfaceManager.RootControl); // DS14
            _userInterfaceManager.WindowRoot.OnChildAdded += ApplyDebugConsoleTheme; // DS14
            _configuration.OnValueChanged(CCCCVars.InterfaceStyle, OnInterfaceStyleChanged); // DS14

            // warn about unused sheetlets
            if (UnusedSheetlets.Count > 0)
            {
                var sheetlets = UnusedSheetlets.AsEnumerable()
                    .Take(5)
                    .Select(t => t.FullName ?? "<could not get FullName>")
                    .ToArray();
                sawmill.Error($"There are unloaded sheetlets: {string.Join(", ", sheetlets)}");
            }

            sawmill.Debug($"Initialized {_styleRuleCount} style rules in {sw.Elapsed}");
        }

        private int _styleRuleCount;

        // DS14-start
        private void OnInterfaceStyleChanged(string theme)
        {
            SelectInterfaceStyle(theme);

            var baseSheet = new NanotrasenStylesheet(new BaseStylesheet.NoConfig(), this);
            SheetNanotrasen = baseSheet.Stylesheet;
            Stylesheets[baseSheet.StylesheetName] = SheetNanotrasen;
            _userInterfaceManager.Stylesheet = SheetNanotrasen;
            ApplyDebugConsoleTheme(_userInterfaceManager.RootControl);
        }

        private void SelectInterfaceStyle(string theme)
        {
            if (DeadSpaceStylePalette.TrySetTheme(theme))
                return;

            _logManager.GetSawmill("style")
                .Warning($"Unknown DS14 interface style '{theme}', using '{DeadSpaceStylePalette.CurrentTheme}'.");
        }

        private static void ApplyDebugConsoleTheme(Control control)
        {
            if (control is DebugConsole console)
            {
                console.FindControl<OutputPanel>("Output").StyleBoxOverride = new StyleBoxFlat
                {
                    BackgroundColor = DeadSpaceStylePalette.SurfaceTranscript,
                    ContentMarginLeftOverride = 3,
                    ContentMarginRightOverride = 3,
                    ContentMarginTopOverride = 3,
                    ContentMarginBottomOverride = 3,
                };
            }

            foreach (var child in control.Children)
                ApplyDebugConsoleTheme(child);
        }
        // DS14-end

        private Stylesheet Init(BaseStylesheet baseSheet)
        {
            Stylesheets.Add(baseSheet.StylesheetName, baseSheet.Stylesheet);
            _styleRuleCount += baseSheet.Stylesheet.Rules.Count;
            return baseSheet.Stylesheet;
        }
    }
}
