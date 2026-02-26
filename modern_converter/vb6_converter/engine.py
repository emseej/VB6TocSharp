from __future__ import annotations

from pathlib import Path
import re

from .models import ConversionOptions, ConversionResult
from .rules import (
    CONST_RE,
    DIM_RE,
    FOR_RE,
    apply_identifier_renames,
    apply_string_literal_renames,
    convert_expression,
    convert_parameters,
    normalize_type,
    parse_signature,
    strip_set_keyword,
    trim_inline_comment,
)


class VB6Converter:
    """Converts VB6 source code into C# or VB.NET using rule-based transforms."""

    def __init__(self, options: ConversionOptions) -> None:
        self.options = options

    def convert_file(self, source: Path, destination: Path) -> ConversionResult:
        converted = self.convert_text(source.read_text(encoding="utf-8", errors="ignore"), source.stem)
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_text(converted, encoding="utf-8")
        return ConversionResult(str(source), str(destination), converted)

    def convert_text(self, code: str, module_name: str = "Module1") -> str:
        indent_level = 0
        output: list[str] = []
        target = self.options.target.value

        if target == "csharp":
            output.extend([
                "using System;",
                "",
                f"namespace {self.options.namespace};",
                "",
                f"public static class {module_name}",
                "{",
            ])
            indent_level = 1
        else:
            output.extend([f"Namespace {self.options.namespace}", f"Public Module {module_name}"])
            indent_level = 1

        for raw_line in code.splitlines():
            line, comment = trim_inline_comment(raw_line.rstrip())
            stripped = line.strip()

            if not stripped:
                output.append("")
                continue

            if stripped.startswith("Attribute "):
                continue

            if stripped.startswith("'"):
                output.append(self._emit_comment(stripped[1:].strip(), indent_level))
                continue

            lowered = stripped.lower()
            if lowered.startswith(("end if", "next", "loop", "wend", "end sub", "end function", "end select")):
                indent_level = max(1, indent_level - 1)

            converted = self._convert_statement(stripped, target)

            if comment:
                converted = f"{converted} {self._inline_comment(comment)}"

            output.append(f"{self.options.indent * indent_level}{converted}".rstrip())

            if self._opens_block(lowered):
                indent_level += 1

        if target == "csharp":
            output.append("}")
        else:
            output.extend(["End Module", "End Namespace"])

        return "\n".join(output) + "\n"

    def _convert_statement(self, line: str, target: str) -> str:
        line = strip_set_keyword(line)
        line = apply_identifier_renames(line, self.options.rename.function_names | self.options.rename.variable_names)
        line = apply_string_literal_renames(line, self.options.rename.string_literals)

        signature = parse_signature(line)
        if signature:
            params = convert_parameters(signature.params, target)
            name = signature.name
            if target == "csharp":
                access = signature.access.lower()
                if signature.return_type:
                    ret = normalize_type(signature.return_type, target)
                    return f"{access} static {ret} {name}({params}) {{"
                return f"{access} static void {name}({params}) {{"
            if signature.return_type:
                return f"{signature.access} Function {name}({params}) As {signature.return_type}"
            return f"{signature.access} Sub {name}({params})"

        if line.lower().startswith("exit sub"):
            return "return;" if target == "csharp" else "Exit Sub"
        if line.lower().startswith("exit function"):
            return "return;" if target == "csharp" else "Exit Function"

        if line.lower().startswith("end sub") or line.lower().startswith("end function"):
            return "}" if target == "csharp" else line

        dim_match = DIM_RE.match(line)
        if dim_match:
            name = dim_match.group("name")
            vb_type = dim_match.group("type")
            if target == "csharp":
                return f"{normalize_type(vb_type, target)} {name};"
            return f"Dim {name} As {vb_type}"

        const_match = CONST_RE.match(line)
        if const_match:
            name = const_match.group("name")
            vb_type = const_match.group("type")
            value = convert_expression(const_match.group("value"), target)
            if target == "csharp":
                return f"const {normalize_type(vb_type, target)} {name} = {value};"
            return f"Const {name} As {vb_type} = {value}"

        for_match = FOR_RE.match(line)
        if for_match:
            v = for_match.group("var")
            start = convert_expression(for_match.group("start"), target)
            end = convert_expression(for_match.group("end"), target)
            step = (for_match.group("step") or "1").strip()
            if target == "csharp":
                comparator = ">=" if step.startswith("-") else "<="
                op = "-=" if step.startswith("-") else "+="
                step_value = step.lstrip("-")
                return f"for (var {v} = {start}; {v} {comparator} {end}; {v} {op} {step_value}) {{"
            return line

        if line.lower().startswith("if ") and line.lower().endswith(" then"):
            cond = convert_expression(line[3:-5], target)
            return f"if ({cond}) {{" if target == "csharp" else f"If {cond} Then"

        if line.lower().startswith("elseif ") and line.lower().endswith(" then"):
            cond = convert_expression(line[7:-5], target)
            return f"}} else if ({cond}) {{" if target == "csharp" else f"ElseIf {cond} Then"

        if line.lower() == "else":
            return "} else {" if target == "csharp" else "Else"

        if line.lower().startswith("end if"):
            return "}" if target == "csharp" else "End If"

        if line.lower().startswith("select case "):
            expr = convert_expression(line[12:], target)
            return f"switch ({expr}) {{" if target == "csharp" else f"Select Case {expr}"

        if line.lower().startswith("case else"):
            return "default:" if target == "csharp" else "Case Else"

        if line.lower().startswith("case "):
            value = convert_expression(line[5:], target)
            return f"case {value}:" if target == "csharp" else f"Case {value}"

        if line.lower().startswith("end select"):
            return "}" if target == "csharp" else "End Select"

        if line.lower().startswith("do while "):
            cond = convert_expression(line[9:], target)
            return f"while ({cond}) {{" if target == "csharp" else f"Do While {cond}"

        if line.lower() == "loop":
            return "}" if target == "csharp" else "Loop"

        if line.lower().startswith("next"):
            return "}" if target == "csharp" else line

        assignment = re.match(r"^(?P<lhs>[\w\.]+)\s*=\s*(?P<rhs>.+)$", line)
        if assignment:
            lhs = assignment.group("lhs")
            rhs = convert_expression(assignment.group("rhs"), target)
            end = ";" if target == "csharp" else ""
            return f"{lhs} = {rhs}{end}"

        call_match = re.match(r"^Call\s+(?P<rest>.+)$", line, flags=re.IGNORECASE)
        if call_match:
            rest = call_match.group("rest")
            return f"{rest};" if target == "csharp" else rest

        if target == "csharp" and self.options.include_todo_comments:
            return f"// TODO: verify conversion: {line}"
        return line

    def _opens_block(self, lowered_line: str) -> bool:
        return lowered_line.startswith(("if ", "for ", "do while", "select case", "sub ", "public sub", "private sub", "friend sub", "function ", "public function", "private function", "friend function")) or lowered_line in {"else"} or lowered_line.startswith("elseif")

    def _emit_comment(self, comment: str, indent_level: int) -> str:
        if self.options.target.value == "csharp":
            return f"{self.options.indent * indent_level}// {comment}".rstrip()
        return f"{self.options.indent * indent_level}' {comment}".rstrip()

    def _inline_comment(self, comment: str) -> str:
        return f"// {comment}" if self.options.target.value == "csharp" else f"' {comment}"
