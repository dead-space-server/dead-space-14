// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Client.Stylesheets.Colorspace;
using Content.Client.Stylesheets.Palette;
using Content.Shared.DeadSpace.CCCCVars;

namespace Content.Client.DeadSpace.Stylesheets;

/// <summary>
/// Shared DS14 UI colors. A theme switch replaces this palette before rebuilding
/// the content stylesheet, while direct runtime users always resolve the active colors.
/// </summary>
public static class DeadSpaceStylePalette
{
    public const float NeutralLightnessOffset = -0.002f;

    private static readonly ThemePalette Dark = CreateDark();
    private static readonly ThemePalette Light = CreateLight();
    private static readonly ThemePalette Classic = CreateClassic();
    private static ThemePalette _current = Dark;

    public static string CurrentTheme { get; private set; } = CCCCVars.InterfaceStyleDark;
    public static bool ClassicChrome => _current.ClassicChrome;
    public static bool LightChrome => ReferenceEquals(_current, Light);

    public static Color Surface => _current.Surface;
    public static Color SurfaceDark => _current.SurfaceDark;
    public static Color SurfaceFlat => _current.SurfaceFlat;
    public static Color SurfaceHeader => _current.SurfaceHeader;
    public static Color SurfaceInset => _current.SurfaceInset;
    public static Color SurfaceStatus => _current.SurfaceStatus;
    public static Color SurfacePopup => _current.SurfacePopup;
    public static Color SurfaceIcon => _current.SurfaceIcon;
    public static Color SurfaceTabs => _current.SurfaceTabs;
    public static Color SurfaceTabActive => _current.SurfaceTabActive;
    public static Color SurfaceTabInactive => _current.SurfaceTabInactive;
    public static Color SurfaceTranscript => _current.SurfaceTranscript;
    public static Color ModalScrim => _current.ModalScrim;

    public static Color Control => _current.Control;
    public static Color ControlHover => _current.ControlHover;
    public static Color ControlPressed => _current.ControlPressed;
    public static Color ControlDisabled => _current.ControlDisabled;
    public static Color Action => _current.Action;
    public static Color ActionHover => _current.ActionHover;
    public static Color ActionPressed => _current.ActionPressed;
    public static Color ActionDisabled => _current.ActionDisabled;
    public static Color ListItem => _current.ListItem;
    public static Color ListItemAlternate => _current.ListItemAlternate;
    public static Color ListItemHover => _current.ListItemHover;
    public static Color ListItemPressed => _current.ListItemPressed;
    public static Color Input => _current.Input;

    public static Color Border => _current.Border;
    public static Color BorderDark => _current.BorderDark;
    public static Color BorderHeader => _current.BorderHeader;
    public static Color BorderInset => _current.BorderInset;
    public static Color BorderControl => _current.BorderControl;
    public static Color BorderDisabled => _current.BorderDisabled;
    public static Color BorderIcon => _current.BorderIcon;
    public static Color BorderTabActive => _current.BorderTabActive;
    public static Color BorderTabInactive => _current.BorderTabInactive;
    public static Color HoverOutline => _current.HoverOutline;
    public static Color PressedOutline => _current.PressedOutline;

    public static Color CyanDim => _current.CyanDim;
    public static Color Cyan => _current.Cyan;
    public static Color CyanBright => _current.CyanBright;
    public static Color CyanSelection => _current.CyanSelection;
    public static Color Amber => _current.Amber;
    public static Color AccentDim => _current.AccentDim;

    public static Color Text => _current.Text;
    public static Color TextInactive => _current.TextInactive;
    public static Color TextMuted => _current.TextMuted;
    public static Color TextPlaceholder => _current.TextPlaceholder;
    public static Color TextOnTranscript => _current.TextOnTranscript;
    public static Color TextOnTranscriptMuted => _current.TextOnTranscriptMuted;
    public static Color TextOnTranscriptPlaceholder => _current.TextOnTranscriptPlaceholder;

    public static Color Positive => _current.Positive;
    public static Color PositiveHover => _current.PositiveHover;
    public static Color PositivePressed => _current.PositivePressed;
    public static Color PositiveBorder => _current.PositiveBorder;
    public static Color PositiveBorderHover => _current.PositiveBorderHover;
    public static Color PositiveBorderPressed => _current.PositiveBorderPressed;
    public static Color Negative => _current.Negative;
    public static Color NegativeHover => _current.NegativeHover;
    public static Color NegativeStrong => _current.NegativeStrong;
    public static Color NegativeStrongHover => _current.NegativeStrongHover;
    public static Color NegativeBorder => _current.NegativeBorder;
    public static Color NegativeBorderStrong => _current.NegativeBorderStrong;
    public static Color NegativeBorderHover => _current.NegativeBorderHover;
    public static Color Warning => _current.Warning;
    public static Color WarningControl => _current.WarningControl;
    public static Color WarningControlHover => _current.WarningControlHover;
    public static Color WarningControlPressed => _current.WarningControlPressed;
    public static Color WarningBorder => _current.WarningBorder;
    public static Color WarningBorderHover => _current.WarningBorderHover;

    public static ColorPalette PrimaryPalette => _current.PrimaryPalette;
    public static ColorPalette SecondaryPalette => _current.SecondaryPalette;

    /// <summary>
    /// Selects a canonical palette. Invalid archived values safely fall back to the current dark style.
    /// </summary>
    public static bool TrySetTheme(string theme)
    {
        if (string.Equals(theme, CCCCVars.InterfaceStyleLight, StringComparison.OrdinalIgnoreCase))
        {
            _current = Light;
            CurrentTheme = CCCCVars.InterfaceStyleLight;
            return true;
        }

        if (string.Equals(theme, CCCCVars.InterfaceStyleClassic, StringComparison.OrdinalIgnoreCase))
        {
            _current = Classic;
            CurrentTheme = CCCCVars.InterfaceStyleClassic;
            return true;
        }

        _current = Dark;
        CurrentTheme = CCCCVars.InterfaceStyleDark;
        return string.Equals(theme, CCCCVars.InterfaceStyleDark, StringComparison.OrdinalIgnoreCase);
    }

    private static ThemePalette CreateDark()
    {
        var palette = new ThemePalette
        {
            Surface = Neutral("#181D23"),
            SurfaceDark = Neutral("#10151B"),
            SurfaceFlat = Neutral("#20262D"),
            SurfaceHeader = Neutral("#262E37"),
            SurfaceInset = Neutral("#0C1117"),
            SurfaceStatus = Neutral("#1D252E"),
            SurfacePopup = Neutral("#121920"),
            SurfaceIcon = Neutral("#070A0E"),
            SurfaceTabs = Neutral("#131A21"),
            SurfaceTabActive = Neutral("#2B3540"),
            SurfaceTabInactive = Neutral("#171E26"),
            SurfaceTranscript = Neutral("#090E14"),
            ModalScrim = Color.FromHex("#000000AA"),
            // Controls must remain distinct from SurfaceHeader even without a permanent border.
            Control = Neutral("#35434F"),
            ControlHover = Neutral("#405160"),
            ControlPressed = Neutral("#4C6374"),
            // Disabled controls are deliberately lighter than enabled controls. Dense machine recipe lists
            // otherwise make an unavailable row disappear into the surrounding dark surface.
            ControlDisabled = Neutral("#333B44"),
            Action = Neutral("#303E4A"),
            ActionHover = Neutral("#3E5261"),
            ActionPressed = Neutral("#4C6576"),
            ActionDisabled = Neutral("#333B44"),
            ListItem = Neutral("#212B35"),
            ListItemAlternate = Neutral("#283440"),
            ListItemHover = Neutral("#33424F"),
            ListItemPressed = Neutral("#3D5060"),
            Input = Neutral("#111A23"),
            Border = Color.FromHex("#49545F"),
            BorderDark = Color.FromHex("#2B343D"),
            BorderHeader = Color.FromHex("#6B573D"),
            BorderInset = Color.FromHex("#303A44"),
            BorderControl = Color.FromHex("#52606D"),
            BorderDisabled = Color.FromHex("#59636D"),
            BorderIcon = Color.FromHex("#394550"),
            BorderTabActive = Color.FromHex("#C09258"),
            BorderTabInactive = Color.FromHex("#343E48"),
            HoverOutline = Color.FromHex("#C09258"),
            PressedOutline = Color.FromHex("#DDAA65"),
            CyanDim = Color.FromHex("#1D5B73"),
            Cyan = Color.FromHex("#1D8BAD"),
            CyanBright = Color.FromHex("#2EA7D0"),
            CyanSelection = Color.FromHex("#1D7E9D88"),
            Amber = Palettes.Gold.Text,
            AccentDim = Color.FromHex("#514431"),
            Text = Color.FromHex("#F0F2F4"),
            TextInactive = Color.FromHex("#C4C9CF"),
            TextMuted = Color.FromHex("#A9B0B8"),
            TextPlaceholder = Color.FromHex("#89939E"),
            TextOnTranscript = Color.FromHex("#F0F2F4"),
            TextOnTranscriptMuted = Color.FromHex("#B6C0CA"),
            TextOnTranscriptPlaceholder = Color.FromHex("#8D99A5"),
            Positive = Color.FromHex("#1D4B2EF4"),
            PositiveHover = Color.FromHex("#245F39F8"),
            PositivePressed = Color.FromHex("#2B7A40F8"),
            PositiveBorder = Color.FromHex("#2EA043"),
            PositiveBorderHover = Color.FromHex("#3FB950"),
            PositiveBorderPressed = Color.FromHex("#56D364"),
            Negative = Color.FromHex("#3B1C23F2"),
            NegativeHover = Color.FromHex("#51242CF6"),
            NegativeStrong = Color.FromHex("#652A33F4"),
            NegativeStrongHover = Color.FromHex("#7A303AF8"),
            NegativeBorder = Color.FromHex("#9D3F49"),
            NegativeBorderStrong = Color.FromHex("#C44B55"),
            NegativeBorderHover = Color.FromHex("#F85149"),
            Warning = Color.FromHex("#947300"),
            WarningControl = Color.FromHex("#4A2A16F4"),
            WarningControlHover = Color.FromHex("#66361BF8"),
            WarningControlPressed = Color.FromHex("#84451FF8"),
            WarningBorder = Color.FromHex("#D86F32"),
            WarningBorderHover = Color.FromHex("#F0883E"),
        };

        return palette with
        {
            PrimaryPalette = ColorPalette.FromHexBase(
                "#6A573F",
                lightnessShift: 0.05f,
                chromaShift: 0.003f,
                element: palette.Control,
                background: palette.SurfaceDark,
                text: palette.Text),
            SecondaryPalette = ColorPalette.FromHexBase(
                "#34373B",
                lightnessShift: 0.05f,
                element: palette.SurfaceHeader,
                background: palette.Surface,
                text: palette.TextMuted),
        };
    }

    private static ThemePalette CreateLight()
    {
        var palette = new ThemePalette
        {
            Surface = Color.FromHex("#D0D5D9"),
            SurfaceDark = Color.FromHex("#B8C0C7"),
            SurfaceFlat = Color.FromHex("#DEE2E5"),
            SurfaceHeader = Color.FromHex("#C2C9CF"),
            SurfaceInset = Color.FromHex("#AEB7C0"),
            SurfaceStatus = Color.FromHex("#C4CBD1"),
            SurfacePopup = Color.FromHex("#E7EAEC"),
            SurfaceIcon = Color.FromHex("#28313A"),
            SurfaceTabs = Color.FromHex("#BAC2C9"),
            SurfaceTabActive = Color.FromHex("#E4E7E9"),
            SurfaceTabInactive = Color.FromHex("#BCC4CB"),
            // Chat, logs and ahelp retain a dark transcript in Light because their markup colors are
            // intentionally authored for a dark background and cannot be recolored safely by a stylesheet.
            SurfaceTranscript = Color.FromHex("#111A23"),
            ModalScrim = Color.FromHex("#10131888"),
            // A neutral mid-grey gives controls hierarchy without the washed-out blue cast of the old fill.
            Control = Color.FromHex("#A8AEB2"),
            ControlHover = Color.FromHex("#C9BEAD"),
            ControlPressed = Color.FromHex("#B69A70"),
            // The light neutral fill is intentionally separated from the darker enabled control fill.
            ControlDisabled = Color.FromHex("#E2E6E9"),
            Action = Color.FromHex("#9FA6AB"),
            ActionHover = Color.FromHex("#C4B69F"),
            ActionPressed = Color.FromHex("#AB8C5F"),
            ActionDisabled = Color.FromHex("#E2E6E9"),
            ListItem = Color.FromHex("#DCE0E3"),
            ListItemAlternate = Color.FromHex("#CCD3D8"),
            ListItemHover = Color.FromHex("#E3D8C8"),
            ListItemPressed = Color.FromHex("#D5C09E"),
            Input = Color.FromHex("#ECEFF1"),
            Border = Color.FromHex("#67727D"),
            BorderDark = Color.FromHex("#89949E"),
            BorderHeader = Color.FromHex("#87663C"),
            BorderInset = Color.FromHex("#7D8994"),
            BorderControl = Color.FromHex("#697680"),
            BorderDisabled = Color.FromHex("#A5AEB5"),
            BorderIcon = Color.FromHex("#52606B"),
            BorderTabActive = Color.FromHex("#8A5D27"),
            BorderTabInactive = Color.FromHex("#8C969F"),
            HoverOutline = Color.FromHex("#8A5D27"),
            PressedOutline = Color.FromHex("#653D10"),
            CyanDim = Color.FromHex("#356D80"),
            Cyan = Color.FromHex("#126A86"),
            CyanBright = Color.FromHex("#006F92"),
            CyanSelection = Color.FromHex("#1D7E9D55"),
            Amber = Color.FromHex("#694314"),
            AccentDim = Color.FromHex("#92734C"),
            Text = Color.FromHex("#171C21"),
            TextInactive = Color.FromHex("#303840"),
            TextMuted = Color.FromHex("#46515B"),
            TextPlaceholder = Color.FromHex("#626D77"),
            TextOnTranscript = Color.FromHex("#F0F2F4"),
            TextOnTranscriptMuted = Color.FromHex("#B6C0CA"),
            TextOnTranscriptPlaceholder = Color.FromHex("#8D99A5"),
            Positive = Color.FromHex("#BBD8C2"),
            PositiveHover = Color.FromHex("#A7CDAF"),
            PositivePressed = Color.FromHex("#91C09C"),
            PositiveBorder = Color.FromHex("#347E48"),
            PositiveBorderHover = Color.FromHex("#286D3C"),
            PositiveBorderPressed = Color.FromHex("#1E5C31"),
            Negative = Color.FromHex("#DDBFC4"),
            NegativeHover = Color.FromHex("#D1A7AE"),
            NegativeStrong = Color.FromHex("#D2AAB1"),
            NegativeStrongHover = Color.FromHex("#C79099"),
            NegativeBorder = Color.FromHex("#9D3F49"),
            NegativeBorderStrong = Color.FromHex("#A73541"),
            NegativeBorderHover = Color.FromHex("#842C36"),
            Warning = Color.FromHex("#C8A34B"),
            WarningControl = Color.FromHex("#E2CCAE"),
            WarningControlHover = Color.FromHex("#D5B98F"),
            WarningControlPressed = Color.FromHex("#C5A371"),
            WarningBorder = Color.FromHex("#9A552C"),
            WarningBorderHover = Color.FromHex("#7F421F"),
        };

        return palette with
        {
            PrimaryPalette = new ColorPalette(
                Color.FromHex("#8A6A42"),
                0.05f,
                0.003f,
                palette.Control,
                palette.ControlHover,
                palette.ControlPressed,
                palette.ControlDisabled,
                palette.Surface,
                palette.SurfaceFlat,
                palette.SurfaceDark,
                palette.Text,
                palette.TextMuted),
            SecondaryPalette = new ColorPalette(
                Color.FromHex("#9A9DA0"),
                0.05f,
                0f,
                palette.SurfaceHeader,
                palette.SurfaceFlat,
                palette.SurfaceDark,
                palette.ControlDisabled,
                palette.Surface,
                palette.SurfaceFlat,
                palette.SurfaceDark,
                palette.TextMuted,
                palette.TextPlaceholder),
        };
    }

    private static ThemePalette CreateClassic()
    {
        return new ThemePalette
        {
            ClassicChrome = true,
            // The legacy option is the pre-7074b42 UI: ordinary Wizards/Nanotrasen palettes, not the
            // later cyan DS14 menu sheetlet. These values only bridge layout classes that did not exist then.
            Surface = Palettes.Slate.Background,
            SurfaceDark = Color.FromHex("#25252A"),
            SurfaceFlat = Palettes.Slate.Background,
            SurfaceHeader = Palettes.Slate.Element,
            SurfaceInset = Palettes.Slate.BackgroundDark,
            SurfaceStatus = Palettes.Slate.Background,
            SurfacePopup = Palettes.Slate.BackgroundDark,
            SurfaceIcon = Color.Black,
            SurfaceTabs = Palettes.Slate.Background,
            SurfaceTabActive = Palettes.Slate.Element,
            SurfaceTabInactive = Palettes.Slate.Background,
            SurfaceTranscript = Palettes.Slate.BackgroundDark,
            ModalScrim = Color.FromHex("#000000AA"),
            Control = Palettes.Navy.Element,
            ControlHover = Palettes.Navy.HoveredElement,
            ControlPressed = Palettes.Navy.PressedElement,
            ControlDisabled = Palettes.Navy.DisabledElement,
            Action = Palettes.Navy.Element,
            ActionHover = Palettes.Navy.HoveredElement,
            ActionPressed = Palettes.Navy.PressedElement,
            ActionDisabled = Palettes.Navy.DisabledElement,
            ListItem = Palettes.Navy.Element,
            ListItemAlternate = Palettes.Slate.Element,
            ListItemHover = Palettes.Navy.HoveredElement,
            ListItemPressed = Palettes.Navy.PressedElement,
            Input = Palettes.Navy.BackgroundDark,
            Border = Color.FromHex("#525252"),
            BorderDark = Color.FromHex("#3F3F43"),
            BorderHeader = Color.FromHex("#525252"),
            BorderInset = Color.FromHex("#3F3F43"),
            BorderControl = Color.FromHex("#525252"),
            BorderDisabled = Color.FromHex("#38383D"),
            BorderIcon = Color.FromHex("#525252"),
            BorderTabActive = Palettes.Slate.HoveredElement,
            BorderTabInactive = Color.Transparent,
            HoverOutline = Palettes.Navy.HoveredElement,
            PressedOutline = Palettes.Navy.PressedElement,
            CyanDim = Color.FromHex("#75838E"),
            Cyan = Color.FromHex("#789B8C"),
            CyanBright = Color.FromHex("#ACBAC6"),
            CyanSelection = Color.FromHex("#789B8C88"),
            Amber = Palettes.Gold.Text,
            AccentDim = Color.FromHex("#525252"),
            Text = Color.White,
            TextInactive = Color.FromHex("#99A7B3"),
            TextMuted = Color.FromHex("#757575"),
            TextPlaceholder = Color.FromHex("#5A5A5A"),
            TextOnTranscript = Color.White,
            TextOnTranscriptMuted = Color.FromHex("#99A7B3"),
            TextOnTranscriptPlaceholder = Color.FromHex("#757575"),
            Positive = Palettes.Green.Element,
            PositiveHover = Palettes.Green.HoveredElement,
            PositivePressed = Palettes.Green.PressedElement,
            PositiveBorder = Palettes.Green.Element,
            PositiveBorderHover = Palettes.Green.HoveredElement,
            PositiveBorderPressed = Palettes.Green.PressedElement,
            Negative = Palettes.Red.Element,
            NegativeHover = Palettes.Red.HoveredElement,
            NegativeStrong = Palettes.Red.Element,
            NegativeStrongHover = Palettes.Red.HoveredElement,
            NegativeBorder = Palettes.Red.Element,
            NegativeBorderStrong = Palettes.Red.PressedElement,
            NegativeBorderHover = Palettes.Red.HoveredElement,
            Warning = Palettes.Amber.Background,
            WarningControl = Palettes.Amber.Element,
            WarningControlHover = Palettes.Amber.HoveredElement,
            WarningControlPressed = Palettes.Amber.PressedElement,
            WarningBorder = Palettes.Amber.Element,
            WarningBorderHover = Palettes.Amber.HoveredElement,
            PrimaryPalette = Palettes.Navy,
            SecondaryPalette = Palettes.Slate,
        };
    }

    private static Color Neutral(string hex)
    {
        return Color.FromHex(hex).NudgeLightness(NeutralLightnessOffset);
    }

    private sealed record ThemePalette
    {
        public bool ClassicChrome { get; init; }
        public Color Surface { get; init; }
        public Color SurfaceDark { get; init; }
        public Color SurfaceFlat { get; init; }
        public Color SurfaceHeader { get; init; }
        public Color SurfaceInset { get; init; }
        public Color SurfaceStatus { get; init; }
        public Color SurfacePopup { get; init; }
        public Color SurfaceIcon { get; init; }
        public Color SurfaceTabs { get; init; }
        public Color SurfaceTabActive { get; init; }
        public Color SurfaceTabInactive { get; init; }
        public Color SurfaceTranscript { get; init; }
        public Color ModalScrim { get; init; }
        public Color Control { get; init; }
        public Color ControlHover { get; init; }
        public Color ControlPressed { get; init; }
        public Color ControlDisabled { get; init; }
        public Color Action { get; init; }
        public Color ActionHover { get; init; }
        public Color ActionPressed { get; init; }
        public Color ActionDisabled { get; init; }
        public Color ListItem { get; init; }
        public Color ListItemAlternate { get; init; }
        public Color ListItemHover { get; init; }
        public Color ListItemPressed { get; init; }
        public Color Input { get; init; }
        public Color Border { get; init; }
        public Color BorderDark { get; init; }
        public Color BorderHeader { get; init; }
        public Color BorderInset { get; init; }
        public Color BorderControl { get; init; }
        public Color BorderDisabled { get; init; }
        public Color BorderIcon { get; init; }
        public Color BorderTabActive { get; init; }
        public Color BorderTabInactive { get; init; }
        public Color HoverOutline { get; init; }
        public Color PressedOutline { get; init; }
        public Color CyanDim { get; init; }
        public Color Cyan { get; init; }
        public Color CyanBright { get; init; }
        public Color CyanSelection { get; init; }
        public Color Amber { get; init; }
        public Color AccentDim { get; init; }
        public Color Text { get; init; }
        public Color TextInactive { get; init; }
        public Color TextMuted { get; init; }
        public Color TextPlaceholder { get; init; }
        public Color TextOnTranscript { get; init; }
        public Color TextOnTranscriptMuted { get; init; }
        public Color TextOnTranscriptPlaceholder { get; init; }
        public Color Positive { get; init; }
        public Color PositiveHover { get; init; }
        public Color PositivePressed { get; init; }
        public Color PositiveBorder { get; init; }
        public Color PositiveBorderHover { get; init; }
        public Color PositiveBorderPressed { get; init; }
        public Color Negative { get; init; }
        public Color NegativeHover { get; init; }
        public Color NegativeStrong { get; init; }
        public Color NegativeStrongHover { get; init; }
        public Color NegativeBorder { get; init; }
        public Color NegativeBorderStrong { get; init; }
        public Color NegativeBorderHover { get; init; }
        public Color Warning { get; init; }
        public Color WarningControl { get; init; }
        public Color WarningControlHover { get; init; }
        public Color WarningControlPressed { get; init; }
        public Color WarningBorder { get; init; }
        public Color WarningBorderHover { get; init; }
        public ColorPalette PrimaryPalette { get; init; } = null!;
        public ColorPalette SecondaryPalette { get; init; } = null!;
    }
}
