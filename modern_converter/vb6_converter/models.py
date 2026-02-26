from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum


class TargetLanguage(str, Enum):
    CSHARP = "csharp"
    VBNET = "vbnet"


@dataclass(slots=True)
class RenameOptions:
    function_names: dict[str, str] = field(default_factory=dict)
    variable_names: dict[str, str] = field(default_factory=dict)
    string_literals: dict[str, str] = field(default_factory=dict)


@dataclass(slots=True)
class ConversionOptions:
    target: TargetLanguage
    indent: str = "    "
    include_todo_comments: bool = True
    namespace: str = "Converted.VB6"
    project_name: str = "ConvertedProject"
    rename: RenameOptions = field(default_factory=RenameOptions)


@dataclass(slots=True)
class ConversionResult:
    source_path: str
    target_path: str
    converted_code: str
