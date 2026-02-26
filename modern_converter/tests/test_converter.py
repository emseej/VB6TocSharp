from pathlib import Path

from vb6_converter.engine import VB6Converter
from vb6_converter.models import ConversionOptions, RenameOptions, TargetLanguage
from vb6_converter.scaffold import write_visual_studio_scaffold


def test_csharp_basic_conversion() -> None:
    source = """Private Function Add(ByVal x As Integer, ByVal y As Integer) As Integer
Dim total As Integer
If x > 10 Then
    total = x + y
Else
    total = x - y
End If
Add = total
End Function
"""

    converter = VB6Converter(ConversionOptions(target=TargetLanguage.CSHARP))
    result = converter.convert_text(source, module_name="MathModule")

    assert "namespace Converted.VB6;" in result
    assert "public static class MathModule" in result
    assert "private static int Add(int x, int y) {" in result
    assert "if (x > 10) {" in result
    assert "Add = total;" in result


def test_rename_options_are_applied() -> None:
    source = """Public Sub PrintMessage()
Dim oldName As String
oldName = \"hello\"
End Sub
"""
    converter = VB6Converter(
        ConversionOptions(
            target=TargetLanguage.CSHARP,
            rename=RenameOptions(
                function_names={"PrintMessage": "RenderMessage"},
                variable_names={"oldName": "newName"},
                string_literals={"hello": "greetings"},
            ),
        )
    )
    result = converter.convert_text(source)

    assert "RenderMessage" in result
    assert "newName" in result
    assert '"greetings"' in result


def test_scaffold_generation(tmp_path: Path) -> None:
    options = ConversionOptions(target=TargetLanguage.CSHARP, project_name="MigratedApp")
    project, sln = write_visual_studio_scaffold(tmp_path, options)

    assert project.exists()
    assert sln.exists()
    assert "TargetFramework" in project.read_text(encoding="utf-8")
