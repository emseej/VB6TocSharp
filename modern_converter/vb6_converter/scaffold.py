from __future__ import annotations

from pathlib import Path

from .models import ConversionOptions, TargetLanguage


def write_visual_studio_scaffold(output_dir: Path, options: ConversionOptions) -> tuple[Path, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)

    if options.target == TargetLanguage.CSHARP:
        project_path = output_dir / f"{options.project_name}.csproj"
        project_text = """<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n    <LangVersion>latest</LangVersion>\n  </PropertyGroup>\n  <ItemGroup>\n    <Compile Include=\"**/*.cs\" Exclude=\"bin/**;obj/**\" />\n  </ItemGroup>\n</Project>\n"""
    else:
        project_path = output_dir / f"{options.project_name}.vbproj"
        project_text = f"""<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n    <RootNamespace>{options.namespace}</RootNamespace>\n    <LangVersion>latest</LangVersion>\n  </PropertyGroup>\n  <ItemGroup>\n    <Compile Include=\"**/*.vb\" Exclude=\"bin/**;obj/**\" />\n  </ItemGroup>\n</Project>\n"""

    sln_path = output_dir / f"{options.project_name}.sln"
    sln_text = """Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Global
    GlobalSection(SolutionProperties) = preSolution
        HideSolutionNode = FALSE
    EndGlobalSection
EndGlobal
"""

    project_path.write_text(project_text, encoding="utf-8")
    sln_path.write_text(sln_text, encoding="utf-8")
    return project_path, sln_path
