#!/usr/bin/env python3
"""Build and render DS14 UI fixtures through the content client."""

from __future__ import annotations

import argparse
import os
import pathlib
import shutil
import struct
import subprocess
import sys
import tempfile


REPO_ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_OUTPUT = REPO_ROOT.parent / "DS14_UI_RENDERS"
CLIENT_PROJECT = REPO_ROOT / "Content.Client" / "Content.Client.csproj"
SELF_CONTAINED_OUTPUT = REPO_ROOT / "bin" / "Content.Client" / "user_data" / "Screenshots" / "DS14UI"

DEFAULT_FIXTURES = (
    "palette",
    "dropdowns",
    "list-container",
    "vending",
    "smart-fridge",
    "store",
    "lathe",
    "reagent-dispenser",
    "cargo",
    "atmos-power",
    "pda",
    "pda-overflow",
    "photocopier",
    "admin",
    "ahelp",
    "debug-console",
    "appearance-traits",
    "server-list",
    "late-join",
    "role-priorities",
    "options-general",
    "ert-admin",
    "ert-admin-pending",
    "ert-admin-manual",
    "ert-admin-codes",
    "fax",
    "communications",
    "chat",
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("fixtures", nargs="*", default=list(DEFAULT_FIXTURES))
    parser.add_argument("--dotnet", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path, default=DEFAULT_OUTPUT)
    parser.add_argument("--width", type=int, default=1280)
    parser.add_argument("--height", type=int, default=720)
    parser.add_argument("--scale", type=float, default=1.0)
    parser.add_argument("--theme", choices=("Dark", "Light", "Classic"))
    parser.add_argument("--timeout", type=int, default=120)
    parser.add_argument("--no-build", action="store_true")
    return parser.parse_args()


def run(command: list[str], env: dict[str, str], timeout: int | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        cwd=REPO_ROOT,
        env=env,
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        timeout=timeout,
        check=False,
    )


def png_dimensions(path: pathlib.Path) -> tuple[int, int] | None:
    with path.open("rb") as stream:
        header = stream.read(24)

    if len(header) != 24 or header[:8] != b"\x89PNG\r\n\x1a\n" or header[12:16] != b"IHDR":
        return None

    return struct.unpack(">II", header[16:24])


def find_dotnet(explicit: pathlib.Path | None) -> pathlib.Path | None:
    executable = "dotnet.exe" if os.name == "nt" else "dotnet"
    candidates: list[pathlib.Path] = []
    if explicit is not None:
        candidates.append(explicit)
    if configured := os.environ.get("DS14_DOTNET"):
        candidates.append(pathlib.Path(configured))
    if discovered := shutil.which("dotnet"):
        candidates.append(pathlib.Path(discovered))
    candidates.append(REPO_ROOT.parents[1] / ".dotnet-ds14" / executable)

    for candidate in candidates:
        resolved = candidate.expanduser().resolve()
        if resolved.is_file():
            return resolved
    return None


def main() -> int:
    args = parse_args()
    dotnet = find_dotnet(args.dotnet)
    if dotnet is None:
        print("dotnet SDK host not found; pass --dotnet or set DS14_DOTNET", file=sys.stderr)
        return 2

    env = os.environ.copy()
    env.setdefault("DOTNET_CLI_HOME", str(pathlib.Path(tempfile.gettempdir()) / "ds14-dotnet-cli"))
    env.setdefault("DOTNET_CLI_TELEMETRY_OPTOUT", "1")
    local_packages = REPO_ROOT.parents[1] / ".nuget-ds14"
    if local_packages.is_dir():
        env.setdefault("NUGET_PACKAGES", str(local_packages))
    env.setdefault("DOTNET_ROOT", str(dotnet.parent))
    env["PATH"] = str(dotnet.parent) + os.pathsep + env.get("PATH", "")

    if not args.no_build:
        build = run([str(dotnet), "build", str(CLIENT_PROJECT), "--nologo", "--verbosity:minimal"], env)
        print(build.stdout)
        if build.returncode:
            return build.returncode

    profile = f"{args.width}x{args.height}-scale-{args.scale:g}".replace(".", "_")
    if args.theme is not None:
        profile = f"{args.theme.lower()}-{profile}"
    output_dir = args.output.resolve() / profile
    output_dir.mkdir(parents=True, exist_ok=True)
    failures: list[str] = []

    for fixture in args.fixtures:
        output_name = f"{fixture}-{profile}"
        source = SELF_CONTAINED_OUTPUT / f"{output_name}.png"
        if source.exists():
            source.unlink()

        command = [
            str(dotnet),
            "run",
            "--no-build",
            "--project",
            str(CLIENT_PROJECT),
            "--",
            "--self-contained",
            "--cvar",
            f"display.width={args.width}",
            "--cvar",
            f"display.height={args.height}",
            "--cvar",
            f"display.uiScale={args.scale}",
            "--cvar",
            "interface.resolutionAutoScaleEnabled=false",
            "--cvar",
            "display.windowmode=0",
        ]
        if args.theme is not None:
            command.extend(("--cvar", f"ui.style_theme={args.theme}"))
        command.append("+ds14_ui_render " + fixture + " " + output_name)

        try:
            result = run(command, env, timeout=args.timeout)
        except subprocess.TimeoutExpired as error:
            print(f"[{fixture}] timeout after {args.timeout}s\n{error.stdout or ''}")
            failures.append(fixture)
            continue

        (output_dir / f"{output_name}.log").write_text(result.stdout, encoding="utf-8")
        print(f"[{fixture}] exit={result.returncode}")
        if "DS14_UI_RENDER_OK" not in result.stdout or not source.is_file():
            print(result.stdout)
            failures.append(fixture)
            continue

        dimensions = png_dimensions(source)
        if dimensions is None or dimensions[0] < 100 or dimensions[1] < 100:
            print(f"[{fixture}] invalid or unexpectedly small PNG: {dimensions}")
            failures.append(fixture)
            continue

        destination = output_dir / source.name
        shutil.copy2(source, destination)
        print(f"{destination} ({dimensions[0]}x{dimensions[1]})")

    if failures:
        print("failed fixtures: " + ", ".join(failures), file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    sys.exit(main())
