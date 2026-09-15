#!/usr/bin/env python3
"""Static audit and status registry generator for DS14 player-facing UI."""

from __future__ import annotations

import argparse
import collections
import dataclasses
import pathlib
import re
import sys
import xml.etree.ElementTree as etree


REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
CLIENT_ROOT = REPO_ROOT / "Content.Client"
DEFAULT_STATUS = REPO_ROOT.parent / "DS14_UI_STATUS.md"
DEFAULT_REPORT = REPO_ROOT.parent / "DS14_UI_RENDERS" / "ui-audit.md"

REGISTRY_START = "<!-- AUTO-REGISTRY:START -->"
REGISTRY_END = "<!-- AUTO-REGISTRY:END -->"

EXCLUDED_PATH_PARTS = {
    "bql",
    "mapping",
    "sandbox",
    "viewvariables",
    "devwindow",
    "debug",
}

SINGLE_LABEL_PANEL_ALLOW_NAMES = {
    # Shared warning overlays whose panel color changes with the active system state.
    "SystemWarningPanel",
}

MIGRATION_MARKERS = (
    "DeadSpaceStyleClass.",
    "DeadSpaceWindow",
    "DeadSpaceSurface",
    "DeadSpaceAction",
    "DeadSpaceControlPositive",
    "DeadSpaceControlDanger",
    "DeadSpaceList",
    "DeadSpaceInset",
    "DeadSpaceSection",
)

INLINE_PATTERNS = {
    "style-box": re.compile(r"(?:StyleBoxFlat|StyleBoxTexture|StyleBoxOverride)"),
    "panel-override": re.compile(r"PanelOverride\s*="),
    "background-color": re.compile(r"BackgroundColor\s*="),
    "border-color": re.compile(r"BorderColor\s*="),
    "legacy-style": re.compile(r"(?:DeadSpaceMenuSheetlet|DS14Menu[A-Za-z0-9_]*)"),
}

CODE_INLINE_PATTERNS = {
    "cs-style-box": re.compile(r"(?:new\s+StyleBox(?:Flat|Texture)|StyleBoxOverride\s*=)"),
    "cs-panel-override": re.compile(r"PanelOverride\s*="),
    "cs-background-color": re.compile(r"BackgroundColor\s*="),
    "cs-border-color": re.compile(r"BorderColor\s*="),
    "legacy-style": INLINE_PATTERNS["legacy-style"],
}

PURE_WHITE = re.compile(
    r"(?:"
    r"BackgroundColor\s*=\s*\"?(?:Color\.)?(?:White|#(?:FFF|FFFFFF|FFFFFFFF))"
    r"|StyleBoxOverride[^\n]*(?:Color\.White|#(?:FFF|FFFFFF|FFFFFFFF))"
    r"|PanelOverride[^\n]*(?:Color\.White|#(?:FFF|FFFFFF|FFFFFFFF))"
    r"|new\s+StyleBoxFlat\s*\(\s*Color\.White"
    r")",
    re.IGNORECASE,
)
INTERACTIVE = re.compile(r"(?:Button|OptionButton|ListContainer|ItemList|Tree)")
CLEAR_OVERRIDE = re.compile(r"(?:StyleBoxOverride|PanelOverride)\s*=\s*null\s*;")

# Exact, reviewed exceptions for data visualizations, intentionally themed controls and color-only transition
# infrastructure. Each entry is capped at the current number of decorative findings and guarded by stable symbols.
# Adding another inline style to one of these files therefore makes the audit fail until it is reviewed explicitly.
DOCUMENTED_EXCEPTIONS: dict[str, tuple[int, tuple[str, ...], str]] = {
    "Content.Client/Administration/UI/AdminAnnounceWindow.xaml.cs": (
        5,
        ("BuildColorPreviewStyle", "_colorPreviewBtn"),
        "announcement color preview selected by an administrator",
    ),
    "Content.Client/Administration/UI/BanPanel/BanPanel.xaml.cs": (
        3,
        ("RolesContainer", "BackgroundColor = color"),
        "role-group data color strip",
    ),
    "Content.Client/Administration/UI/GamePreset/GamePresetWindow.xaml.cs": (
        10,
        ("ResolveBackgroundColor", "CreateActivePresetCard", "CreateCustomPresetCard"),
        "game-mode category and pending-selection states",
    ),
    "Content.Client/Arcade/BlockGameMenu.cs": (
        18,
        ("SetupGameGrid", "SetupNextBox", "SetupHoldBox", "GetColorForPosition"),
        "arcade board, preview bezel and dynamic block cells",
    ),
    "Content.Client/Atmos/Consoles/AtmosMonitoringEntryContainer.xaml": (
        2,
        ("NetworkColorStripe", "#d7d7d7"),
        "neutral backing modulated by the atmosphere network data color",
    ),
    "Content.Client/Cargo/UI/CargoConsoleMenu.xaml.cs": (
        3,
        ("PopulateOrders", "account.Color", "CargoOrderRow"),
        "cargo account color strip",
    ),
    "Content.Client/Changeling/UI/ChangelingTransformBoundUserInterface.cs": (
        4,
        ("SelectedOptionBackground", "DisabledOptionBackground", "ConvertToButtons"),
        "selected and unavailable radial identity states",
    ),
    "Content.Client/Chemistry/UI/ChemMasterWindow.xaml.cs": (
        3,
        ("BuildReagentRow", "reagentColor"),
        "reagent substance-color strip",
    ),
    "Content.Client/Chemistry/UI/ReagentCardControl.xaml.cs": (
        3,
        ("ColorPanel", "item.ReagentColor"),
        "reagent inventory data color",
    ),
    "Content.Client/DeadSpace/Arena/ArenaLoadoutWindow.cs": (
        6,
        ("_selectedStyle", "_equippedStyle", "PositiveBorderHover"),
        "arena selected and equipped card states",
    ),
    "Content.Client/DeadSpace/CodeLock/CodeLockMenu.xaml": (
        2,
        ("CodeStatusDisplay", "#001c00"),
        "intentionally green code-lock terminal display",
    ),
    "Content.Client/DeadSpace/Communications/UI/EmagCommunicationsInterface.xaml.cs": (
        3,
        ("ColorPreview", "BackgroundColor = color"),
        "communications message color preview",
    ),
    "Content.Client/DeadSpace/UserInterface/DeadSpaceHoverTransitionUIController.cs": (
        8,
        ("BeginTransition", "Color.InterpolateBetween", "StyleBoxOverride"),
        "color-only interpolation between stylesheet-resolved button states",
    ),
    "Content.Client/DeadSpace/Lavaland/Bosses/LavalandBossHudControl.cs": (
        2,
        ("ForegroundStyleBoxOverride", "NegativeBorder"),
        "boss-health semantic foreground",
    ),
    "Content.Client/DeadSpace/Prison/PrisonFactionWindow.cs": (
        3,
        ("PrisonFactionRow", "option.Color"),
        "faction-provided choice color",
    ),
    "Content.Client/DeadSpace/StationAI/UI/CameraNavMapControl.cs": (
        1,
        ("BackgroundColor",),
        "station-AI navigation map canvas",
    ),
    "Content.Client/DeadSpace/StationAI/UI/StationAiCentCommFaxWindow.xaml": (
        3,
        ("DeactivationNoticePanel", "#17150f", "#4b3d20"),
        "themed deactivation warning notice",
    ),
    "Content.Client/DeadSpace/TheCircle/Dreadnought/DreadnoughtLastStandTimerControl.cs": (
        4,
        ("SurfaceStatus", "#7A1717"),
        "dreadnought last-stand timer with domain red border",
    ),
    "Content.Client/DeadSpace/TheCircle/Shuttles/CircleShuttleTimerControl.cs": (
        4,
        ("SurfaceStatus", "BorderColor"),
        "Circle shuttle timer with domain border",
    ),
    "Content.Client/Guidebook/Controls/GuideReagentEmbed.xaml": (
        1,
        ("NameBackground",),
        "single reagent header whose runtime background is the substance color",
    ),
    "Content.Client/Guidebook/Controls/GuideReagentEmbed.xaml.cs": (
        3,
        ("NameBackground", "reagent.SubstanceColor", "GenerateControl"),
        "reagent header and calculated black/white text contrast",
    ),
    "Content.Client/Guidebook/Controls/GuideTechnologyEmbed.xaml.cs": (
        3,
        ("DisciplineColorBackground", "discipline.Color", "GenerateControl"),
        "research-discipline data header",
    ),
    "Content.Client/Lobby/UI/HumanoidProfileEditor.xaml.cs": (
        8,
        ("outlineColor", "CreateAntagCategory"),
        "prototype-defined antagonist category outlines",
    ),
    "Content.Client/Medical/CrewMonitoring/CrewMonitoringNavMapControl.cs": (
        4,
        ("PanelOverride", "BackgroundColor"),
        "crew-monitoring navigation map canvas",
    ),
    "Content.Client/Medical/Cryogenics/CryoPodWindow.xaml": (
        6,
        ("BorderColor=\"orange\"",),
        "cryogenic beaker warning outlines",
    ),
    "Content.Client/Nuke/NukeMenu.xaml": (
        2,
        ("NukeStatusDisplay", "#001c00"),
        "intentionally green nuclear-device terminal display",
    ),
    "Content.Client/PDA/PdaNavigationButton.xaml.cs": (
        4,
        ("ActiveBgColor", "InactiveBgColor", "Background.PanelOverride"),
        "current/active PDA navigation state",
    ),
    "Content.Client/Paper/UI/PaperSheetlet.cs": (
        2,
        ("PaperContainer", "PaperEditBackground", "paperBox"),
        "paper document texture sheetlet",
    ),
    "Content.Client/Paper/UI/PaperWindow.xaml.cs": (
        4,
        ("InitVisuals", "PaperBackground", "_paperContentTex"),
        "prototype-provided paper background and content textures",
    ),
    "Content.Client/Paper/UI/StampWidget.xaml.cs": (
        4,
        ("_borderTexture", "StampPatternTexture", "StampedColor"),
        "stamp texture, pattern and applied ink color",
    ),
    "Content.Client/Pinpointer/UI/NavMapControl.cs": (
        1,
        ("TileColor.WithAlpha",),
        "navigation map tile canvas",
    ),
    "Content.Client/Pinpointer/UI/StationMapBeaconControl.xaml.cs": (
        3,
        ("beacon.Color", "ColorPanel.PanelOverride"),
        "station-map beacon data color",
    ),
    "Content.Client/Power/APC/UI/ApcMenu.xaml.cs": (
        3,
        ("ChargeBar.ForegroundStyleBoxOverride",),
        "APC charge-level foreground",
    ),
    "Content.Client/Power/Battery/BatteryMenu.xaml.cs": (
        9,
        ("_chargeMeterBoxes", "StorageColors", "_activePowerLineStyleBox"),
        "battery charge bands and active power-line state",
    ),
    "Content.Client/Power/PowerMonitoringConsoleNavMapControl.cs": (
        1,
        ("BackgroundColor",),
        "power-monitoring map canvas",
    ),
    "Content.Client/Remotes/UI/DoorRemoteBoundUserInterface.cs": (
        2,
        ("SelectedOptionColor", "SelectedOptionHoverColor", "CreateButtons"),
        "selected radial door-remote mode",
    ),
    "Content.Client/RoundEnd/RoundEndSummaryWindow.cs": (
        4,
        ("MakeManifestDoll", "SurfaceInset", "BorderInset"),
        "fixed-size character preview viewport",
    ),
    "Content.Client/Shuttles/UI/MapScreen.xaml.cs": (
        8,
        ("_ftlStyle", "FTLBar", "Color.LimeGreen"),
        "FTL progress and shuttle map data colors",
    ),
    "Content.Client/Silicons/StationAi/StationAiFixerConsoleWindow.xaml": (
        1,
        ("StationAiStatus",),
        "single dynamic station-AI repair status panel",
    ),
    "Content.Client/Silicons/StationAi/StationAiFixerConsoleWindow.xaml.cs": (
        3,
        ("StationAiStatus", "statusColor"),
        "dynamic station-AI repair status color",
    ),
    "Content.Client/Tips/TippyUI.xaml.cs": (
        2,
        ("LabelPanel", "PaperVisualsComponent", "InitLabel"),
        "prototype-provided paper speech texture",
    ),
    "Content.Client/UserInterface/Controls/HLine.cs": (
        3,
        ("PanelOverride", "value!.Value"),
        "explicit-color line primitive; ordinary dividers use the sheetlet",
    ),
    "Content.Client/UserInterface/Controls/SimpleRadialMenu.xaml.cs": (
        2,
        ("model.BackgroundColor", "model.HoverBackgroundColor"),
        "model-provided radial sector colors",
    ),
    "Content.Client/UserInterface/Controls/SplitBar.xaml.cs": (
        3,
        ("PanelOverride", "BackgroundColor = color"),
        "per-entry data bar",
    ),
    "Content.Client/UserInterface/Systems/Chat/ChatUIController.cs": (
        3,
        ("color.WithAlpha(opacity)", "panel.PanelOverride"),
        "user-configurable chat background opacity",
    ),
    "Content.Client/UserInterface/Systems/Chat/ChatWindow.xaml.cs": (
        2,
        ("poppedOutRoot.BackgroundColor", "SurfaceDark"),
        "background of the separate native chat window root",
    ),
    "Content.Client/UserInterface/Systems/Inventory/Controls/ItemStatusPanel.xaml": (
        2,
        ("StyleBoxTexture", "HighlightPanel"),
        "runtime-textured left/right hand contour and highlight",
    ),
    "Content.Client/_Donate/Emerald/EmeraldBaseWindow.cs": (
        1,
        ("BorderColor", "#6d5a8a"),
        "intentional Emerald donor-window theme border",
    ),
}

DOCUMENTABLE_KINDS = frozenset({
    "style-box",
    "background-color",
    "border-color",
    "cs-style-box",
    "cs-panel-override",
    "cs-background-color",
    "cs-border-color",
    "single-label-panel",
})


@dataclasses.dataclass(frozen=True)
class Finding:
    path: pathlib.Path
    line: int
    kind: str
    detail: str
    allowed: bool = False
    reason: str | None = None


@dataclasses.dataclass
class UiFile:
    path: pathlib.Path
    root_type: str
    status: str
    findings: list[Finding]


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1].split(":", 1)[-1]


def is_excluded(path: pathlib.Path) -> bool:
    return any(part.lower() in EXCLUDED_PATH_PARTS for part in path.parts)


def line_number(raw: str, needle: str) -> int:
    index = raw.find(needle)
    return raw.count("\n", 0, max(index, 0)) + 1


def element_children(element: etree.Element) -> list[etree.Element]:
    return [child for child in list(element) if isinstance(child.tag, str)]


def panel_depth_findings(path: pathlib.Path, root: etree.Element) -> list[Finding]:
    findings: list[Finding] = []

    def visit(element: etree.Element, panel_depth: int) -> None:
        name = local_name(element.tag)
        next_depth = panel_depth + 1 if name == "PanelContainer" else 0
        children = element_children(element)

        if name == "PanelContainer" and len(children) == 1:
            child_name = local_name(children[0].tag)
            if child_name in {"Label", "RichTextLabel"}:
                panel_name = element.attrib.get("Name", "")
                style_classes = element.attrib.get("StyleClasses", "").split()
                allowed = panel_name in SINGLE_LABEL_PANEL_ALLOW_NAMES or "DeadSpaceModalScrim" in style_classes
                findings.append(Finding(
                    path,
                    0,
                    "single-label-panel",
                    f"{panel_name or '<unnamed>'}: {child_name}",
                    allowed,
                    "functional status/interaction overlay" if allowed else None,
                ))

        if next_depth >= 3:
            findings.append(Finding(path, 0, "nested-panels", f"depth={next_depth}"))

        for child in children:
            visit(child, next_depth)

    visit(root, 0)
    return findings


def apply_documented_exception(path: pathlib.Path, raw: str, findings: list[Finding]) -> list[Finding]:
    exception = DOCUMENTED_EXCEPTIONS.get(path.as_posix())
    if exception is None:
        return findings

    maximum, anchors, reason = exception
    candidates = [finding for finding in findings if not finding.allowed and finding.kind in DOCUMENTABLE_KINDS]
    if len(candidates) > maximum or not all(anchor in raw for anchor in anchors):
        return findings

    return [
        dataclasses.replace(finding, allowed=True, reason=reason)
        if finding in candidates
        else finding
        for finding in findings
    ]


def audit_file(path: pathlib.Path) -> UiFile:
    raw = path.read_text(encoding="utf-8-sig")
    relative = path.relative_to(REPO_ROOT)
    excluded = is_excluded(relative)
    status = "исключено" if excluded else (
        "в работе" if any(marker in raw for marker in MIGRATION_MARKERS) else "унаследованный стиль"
    )
    findings: list[Finding] = []
    root_type = "не распознано"

    try:
        root = etree.fromstring(raw)
        root_type = local_name(root.tag)
        if not excluded:
            findings.extend(panel_depth_findings(relative, root))
    except etree.ParseError as error:
        findings.append(Finding(relative, error.position[0], "invalid-xaml", str(error)))

    if not excluded:
        for line_no, line in enumerate(raw.splitlines(), 1):
            for kind, pattern in INLINE_PATTERNS.items():
                if kind == "style-box" and line.lstrip().startswith("</"):
                    continue
                if not pattern.search(line):
                    continue
                findings.append(Finding(relative, line_no, kind, line.strip()))

            if PURE_WHITE.search(line) and INTERACTIVE.search(raw):
                findings.append(Finding(relative, line_no, "pure-white-interactive", line.strip()))

    return UiFile(relative, root_type, status, apply_documented_exception(relative, raw, findings))


def audit() -> list[UiFile]:
    return [audit_file(path) for path in sorted(CLIENT_ROOT.rglob("*.xaml"))]


def is_ui_code(path: pathlib.Path) -> bool:
    if path.name.endswith(".xaml.cs"):
        return True

    lowered_parts = {part.lower() for part in path.parts}
    if lowered_parts.intersection({"ui", "userinterface"}):
        return True

    return bool(re.search(r"(?:Window|Menu|Control|Panel|Popup|Fragment|Widget|Entry)\.cs$", path.name))


def audit_code() -> tuple[int, list[Finding]]:
    findings: list[Finding] = []
    count = 0
    for path in sorted(CLIENT_ROOT.rglob("*.cs")):
        relative = path.relative_to(REPO_ROOT)
        if not is_ui_code(relative) or is_excluded(relative):
            continue
        if "Stylesheets" in relative.parts or "Testing" in relative.parts:
            continue

        count += 1
        raw = path.read_text(encoding="utf-8-sig")
        file_findings: list[Finding] = []
        for line_no, line in enumerate(raw.splitlines(), 1):
            if line.lstrip().startswith("//"):
                continue
            for kind, pattern in CODE_INLINE_PATTERNS.items():
                if not pattern.search(line):
                    continue
                if CLEAR_OVERRIDE.search(line) and kind in {"cs-style-box", "cs-panel-override"}:
                    continue
                file_findings.append(Finding(relative, line_no, kind, line.strip()))
            if PURE_WHITE.search(line) and INTERACTIVE.search(raw):
                file_findings.append(Finding(relative, line_no, "pure-white-interactive", line.strip()))

        findings.extend(apply_documented_exception(relative, raw, file_findings))

    return count, findings


def family(path: pathlib.Path) -> str:
    parts = path.parts
    return parts[1] if len(parts) > 2 else "Content.Client"


def registry_markdown(files: list[UiFile], verified: bool = False) -> str:
    statuses = {
        item.path: "готово" if verified and item.status != "исключено" else item.status
        for item in files
    }
    counts = collections.Counter(statuses.values())
    lines = [
        f"Всего XAML: **{len(files)}**; "
        + ", ".join(f"{key}: **{counts.get(key, 0)}**" for key in ("ожидает", "унаследованный стиль", "в работе", "готово", "исключено")),
        "",
        "| Файл | Корневой control | Статус | Findings | Рендер |",
        "|---|---|---|---:|---|",
    ]

    for item in files:
        blocking = sum(not finding.allowed for finding in item.findings)
        lines.append(
            f"| `{item.path.as_posix()}` | `{item.root_type}` | {statuses[item.path]} | {blocking} | — |"
        )

    return "\n".join(lines)


def report_markdown(files: list[UiFile], code_count: int, code_findings: list[Finding]) -> str:
    all_findings = [finding for item in files for finding in item.findings] + code_findings
    blocking = [finding for finding in all_findings if not finding.allowed]
    allowed = [finding for finding in all_findings if finding.allowed]
    kinds = collections.Counter(finding.kind for finding in blocking)
    families = collections.Counter(family(finding.path) for finding in blocking)

    lines = [
        "# DS14 static UI audit",
        "",
        f"Проверено XAML: **{len(files)}**; UI C#: **{code_count}**.",
        f"Необъяснённых findings: **{len(blocking)}**; документированных доменных исключений: **{len(allowed)}**.",
        "",
        "## Сводка по типам",
        "",
    ]
    lines.extend(f"- `{kind}`: {count}" for kind, count in sorted(kinds.items()))
    lines.extend(["", "## Сводка по семействам", ""])
    lines.extend(f"- `{name}`: {count}" for name, count in families.most_common())
    lines.extend(["", "## Findings", ""])

    for finding in blocking:
        location = f":{finding.line}" if finding.line else ""
        detail = finding.detail.replace("|", "\\|")
        lines.append(f"- `{finding.path}{location}` — **{finding.kind}** — `{detail}`")

    if allowed:
        lines.extend(["", "## Разрешённые доменные исключения", ""])
        grouped: dict[tuple[pathlib.Path, str], list[Finding]] = collections.defaultdict(list)
        for finding in allowed:
            grouped[(finding.path, finding.reason or "documented exception")].append(finding)

        for (path, reason), path_findings in sorted(grouped.items(), key=lambda item: str(item[0][0])):
            kinds = ", ".join(sorted({finding.kind for finding in path_findings}))
            lines.append(f"- `{path}` — {len(path_findings)} finding(s): {kinds}; {reason}.")

    return "\n".join(lines) + "\n"


def replace_registry(status_path: pathlib.Path, registry: str) -> None:
    text = status_path.read_text(encoding="utf-8")
    if REGISTRY_START not in text or REGISTRY_END not in text:
        raise RuntimeError(f"registry markers are missing in {status_path}")

    before, rest = text.split(REGISTRY_START, 1)
    _, after = rest.split(REGISTRY_END, 1)
    status_path.write_text(
        before + REGISTRY_START + "\n\n" + registry + "\n\n" + REGISTRY_END + after,
        encoding="utf-8",
    )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--write-status", type=pathlib.Path, nargs="?", const=DEFAULT_STATUS)
    parser.add_argument("--report", type=pathlib.Path, default=DEFAULT_REPORT)
    parser.add_argument("--fail-on-findings", action="store_true")
    parser.add_argument(
        "--verified",
        action="store_true",
        help="mark every in-scope XAML ready after the external build/layout/render gates have passed",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    files = audit()
    code_count, code_findings = audit_code()
    report = report_markdown(files, code_count, code_findings)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(report, encoding="utf-8")

    blocking_count = sum(not finding.allowed for item in files for finding in item.findings)
    blocking_count += sum(not finding.allowed for finding in code_findings)

    if args.write_status:
        replace_registry(
            args.write_status.resolve(),
            registry_markdown(files, verified=args.verified and blocking_count == 0),
        )

    print(
        f"audited_xaml={len(files)} audited_ui_cs={code_count} "
        f"blocking_findings={blocking_count} report={args.report}"
    )
    return 1 if args.fail_on_findings and blocking_count else 0


if __name__ == "__main__":
    sys.exit(main())
