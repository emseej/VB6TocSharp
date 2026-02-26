from __future__ import annotations

import argparse
import json
from pathlib import Path

from .engine import VB6Converter
from .models import ConversionOptions, RenameOptions, TargetLanguage
from .scaffold import write_visual_studio_scaffold


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        prog="vb6-modern-converter",
        description="Convert VB6 source files into modern C# or VB.NET output.",
    )
    parser.add_argument("source", type=Path, help="Path to a VB6 source file or folder.")
    parser.add_argument("destination", type=Path, help="Output path (file or folder).")
    parser.add_argument("--target", choices=["csharp", "vbnet"], default="csharp")
    parser.add_argument("--namespace", default="Converted.VB6")
    parser.add_argument("--project-name", default="ConvertedProject")
    parser.add_argument("--rename-config", type=Path, help="JSON file containing rename mappings.")
    parser.add_argument("--create-vs-solution", action="store_true", help="Generate .sln + project file for Visual Studio import.")
    return parser.parse_args()


def load_rename_options(path: Path | None) -> RenameOptions:
    if path is None:
        return RenameOptions()
    data = json.loads(path.read_text(encoding="utf-8"))
    return RenameOptions(
        function_names=data.get("function_names", {}),
        variable_names=data.get("variable_names", {}),
        string_literals=data.get("string_literals", {}),
    )


def run() -> int:
    args = parse_args()
    options = ConversionOptions(
        target=TargetLanguage(args.target),
        namespace=args.namespace,
        project_name=args.project_name,
        rename=load_rename_options(args.rename_config),
    )
    converter = VB6Converter(options)

    if args.source.is_file():
        target_file = args.destination
        if args.destination.is_dir():
            ext = ".cs" if options.target == TargetLanguage.CSHARP else ".vb"
            target_file = args.destination / (args.source.stem + ext)
        converter.convert_file(args.source, target_file)
        print(f"Converted {args.source} -> {target_file}")
        return 0

    if not args.source.is_dir():
        raise FileNotFoundError(f"Source path not found: {args.source}")

    out_dir = args.destination
    out_dir.mkdir(parents=True, exist_ok=True)
    patterns = ("*.bas", "*.frm", "*.cls")
    ext = ".cs" if options.target == TargetLanguage.CSHARP else ".vb"

    for pattern in patterns:
        for src in args.source.rglob(pattern):
            relative = src.relative_to(args.source)
            target = out_dir / relative.with_suffix(ext)
            converter.convert_file(src, target)
            print(f"Converted {src} -> {target}")

    if args.create_vs_solution:
        project_path, sln_path = write_visual_studio_scaffold(out_dir, options)
        print(f"Generated Visual Studio files: {project_path}, {sln_path}")

    return 0


if __name__ == "__main__":
    raise SystemExit(run())
