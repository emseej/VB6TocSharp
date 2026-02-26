from __future__ import annotations

import re
from dataclasses import dataclass

VB_TYPE_MAP = {
    "Integer": "int",
    "Long": "int",
    "Double": "double",
    "Single": "float",
    "String": "string",
    "Boolean": "bool",
    "Variant": "dynamic",
    "Byte": "byte",
    "Date": "DateTime",
    "Object": "object",
}


@dataclass(slots=True)
class Signature:
    access: str
    kind: str
    name: str
    params: str
    return_type: str | None


SIG_RE = re.compile(
    r"^(?P<access>Public|Private|Friend)?\s*(?P<kind>Sub|Function)\s+(?P<name>\w+)\s*\((?P<params>[^)]*)\)\s*(?:As\s+(?P<ret>\w+))?$",
    re.IGNORECASE,
)

DIM_RE = re.compile(r"^(Dim|Private|Public)\s+(?P<name>\w+)\s+As\s+(?P<type>\w+)$", re.IGNORECASE)
CONST_RE = re.compile(r"^(Public|Private)?\s*Const\s+(?P<name>\w+)\s+As\s+(?P<type>\w+)\s*=\s*(?P<value>.+)$", re.IGNORECASE)
FOR_RE = re.compile(
    r"^For\s+(?P<var>\w+)\s*=\s*(?P<start>.+?)\s+To\s+(?P<end>.+?)(?:\s+Step\s+(?P<step>.+))?$",
    re.IGNORECASE,
)


def normalize_type(vb_type: str, target: str) -> str:
    if target == "vbnet":
        return vb_type
    return VB_TYPE_MAP.get(vb_type, vb_type)


def parse_signature(line: str) -> Signature | None:
    match = SIG_RE.match(line.strip())
    if not match:
        return None
    access = (match.group("access") or "Private").title()
    return Signature(
        access=access,
        kind=match.group("kind").title(),
        name=match.group("name"),
        params=match.group("params"),
        return_type=match.group("ret"),
    )


def convert_parameters(params: str, target: str) -> str:
    if not params.strip():
        return ""
    converted: list[str] = []
    for raw in params.split(","):
        token = raw.strip()
        token = re.sub(r"^(ByVal|ByRef|Optional)\s+", "", token, flags=re.IGNORECASE)
        parts = re.split(r"\s+As\s+", token, flags=re.IGNORECASE)
        if len(parts) == 2:
            name, vb_type = parts
            if target == "csharp":
                converted.append(f"{normalize_type(vb_type.strip(), target)} {name.strip()}")
            else:
                converted.append(f"{name.strip()} As {vb_type.strip()}")
        else:
            converted.append(token)
    return ", ".join(converted)


def convert_expression(expr: str, target: str) -> str:
    output = expr.strip()
    if target == "csharp":
        replacements = {
            " AndAlso ": " && ",
            " And ": " && ",
            " OrElse ": " || ",
            " Or ": " || ",
            " Not ": " !",
            "<>": "!=",
            "&": "+",
            "True": "true",
            "False": "false",
            "Nothing": "null",
        }
        for old, new in replacements.items():
            output = output.replace(old, new)
    return output


def trim_inline_comment(line: str) -> tuple[str, str | None]:
    in_string = False
    for idx, ch in enumerate(line):
        if ch == '"':
            in_string = not in_string
        elif ch == "'" and not in_string:
            return line[:idx].rstrip(), line[idx + 1 :].strip()
    return line.rstrip(), None


def strip_set_keyword(line: str) -> str:
    return re.sub(r"^Set\s+", "", line, flags=re.IGNORECASE)


def apply_identifier_renames(text: str, mapping: dict[str, str]) -> str:
    if not mapping:
        return text
    out = text
    for src, dst in mapping.items():
        out = re.sub(rf"\b{re.escape(src)}\b", dst, out)
    return out


def apply_string_literal_renames(text: str, mapping: dict[str, str]) -> str:
    if not mapping:
        return text
    out = text
    for src, dst in mapping.items():
        out = out.replace(f'"{src}"', f'"{dst}"')
    return out
