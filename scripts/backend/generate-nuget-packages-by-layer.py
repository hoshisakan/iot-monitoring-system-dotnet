#!/usr/bin/env python3
from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
BACKEND_DIR = ROOT / "app" / "backend"
SRC_DIR = BACKEND_DIR / "src"
OUTPUT = BACKEND_DIR / "NUGET-PACKAGES-BY-LAYER.txt"
SOLUTION = "app/backend/src/Pico2WH.Pi5.IIoT.FourLayer.sln"

PROJECTS = [
    ("Domain", "Pico2WH.Pi5.IIoT.Domain", SRC_DIR / "Pico2WH.Pi5.IIoT.Domain" / "Pico2WH.Pi5.IIoT.Domain.csproj"),
    ("Application", "Pico2WH.Pi5.IIoT.Application", SRC_DIR / "Pico2WH.Pi5.IIoT.Application" / "Pico2WH.Pi5.IIoT.Application.csproj"),
    ("Infrastructure", "Pico2WH.Pi5.IIoT.Infrastructure", SRC_DIR / "Pico2WH.Pi5.IIoT.Infrastructure" / "Pico2WH.Pi5.IIoT.Infrastructure.csproj"),
    ("Api", "Pico2WH.Pi5.IIoT.Api", SRC_DIR / "Pico2WH.Pi5.IIoT.Api" / "Pico2WH.Pi5.IIoT.Api.csproj"),
]

PROJECT_RELATIONS = {
    "Domain": "(none)",
    "Application": "Domain",
    "Infrastructure": "Application, Domain",
    "Api": "Application, Infrastructure",
}


def parse_packages(csproj_path: Path) -> list[dict[str, str]]:
    tree = ET.parse(csproj_path)
    root = tree.getroot()
    packages: list[dict[str, str]] = []
    for node in root.findall(".//PackageReference"):
        include = (node.attrib.get("Include") or "").strip()
        version = (node.attrib.get("Version") or "").strip()
        if not include:
            continue
        private_assets = (node.findtext("PrivateAssets") or "").strip()
        packages.append(
            {
                "name": include,
                "version": version,
                "private_assets": private_assets,
            }
        )
    packages.sort(key=lambda p: p["name"].lower())
    return packages


def summary_line(pkg: dict[str, str]) -> str:
    base = f"  {pkg['name']:<58} {pkg['version']}"
    if pkg["private_assets"]:
        return f"{base} (PrivateAssets={pkg['private_assets']})"
    return base


def add_command(project_path: Path, pkg: dict[str, str]) -> str:
    rel = project_path.relative_to(ROOT).as_posix()
    return f"dotnet add {rel} package {pkg['name']} --version {pkg['version']}"


def build_output() -> str:
    sections: list[str] = []
    packages_by_layer: dict[str, list[dict[str, str]]] = {}

    for layer, _, path in PROJECTS:
        packages_by_layer[layer] = parse_packages(path)

    sections.append("================================================================================")
    sections.append("  app/backend — 各層 NuGet 套件名稱與版本彙整（自動產生）")
    sections.append("  產生依據：app/backend/src/*/*.csproj 之 <PackageReference>")
    sections.append(f"  Solution：{SOLUTION}")
    sections.append("================================================================================")
    sections.append("")
    sections.append("【專案參考關係（非 NuGet）】")
    for layer in ["Domain", "Application", "Infrastructure", "Api"]:
        sections.append(f"  {layer:<14} -> {PROJECT_RELATIONS[layer]}")
    sections.append("")
    sections.append("--------------------------------------------------------------------------------")
    sections.append("一、各層套件摘要（直接參考）")
    sections.append("--------------------------------------------------------------------------------")
    sections.append("")

    for layer, project_name, _ in PROJECTS:
        sections.append(f"【{layer} — {project_name}】")
        pkgs = packages_by_layer[layer]
        if not pkgs:
            sections.append("  (無 PackageReference)")
        else:
            sections.extend(summary_line(pkg) for pkg in pkgs)
        sections.append("")

    sections.append("--------------------------------------------------------------------------------")
    sections.append("二、一次還原")
    sections.append("--------------------------------------------------------------------------------")
    sections.append(f"  dotnet restore {SOLUTION}")
    sections.append("")
    sections.append("--------------------------------------------------------------------------------")
    sections.append("三、依專案逐一加入套件（dotnet add package）")
    sections.append("--------------------------------------------------------------------------------")
    sections.append("")

    for layer, _, path in PROJECTS:
        sections.append(f"--- {layer} ---")
        pkgs = packages_by_layer[layer]
        if not pkgs:
            sections.append("(此層無 NuGet 套件)")
        else:
            sections.extend(add_command(path, pkg) for pkg in pkgs)
        sections.append("")

    sections.append("--------------------------------------------------------------------------------")
    sections.append("四、選用：EF Core CLI 全域工具（非專案 NuGet）")
    sections.append("--------------------------------------------------------------------------------")
    sections.append("  dotnet tool install --global dotnet-ef --version 8.0.11")
    sections.append("  dotnet tool update --global dotnet-ef --version 8.0.11")
    sections.append("")
    sections.append("================================================================================")
    sections.append("更新完成")
    sections.append("================================================================================")
    sections.append("")

    return "\n".join(sections)


def main() -> None:
    content = build_output()
    OUTPUT.write_text(content, encoding="utf-8")
    print(f"generated: {OUTPUT}")


if __name__ == "__main__":
    main()
