using System.Text;
using System.Text.RegularExpressions;

namespace Vb6ModernConverter.Core;

public sealed class Vb6Converter(ConversionOptions options)
{
    private static readonly Regex SigRe = new(@"^(?<access>Public|Private|Friend)?\s*(?<kind>Sub|Function)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?:As\s+(?<ret>[\w\.]+))?$", RegexOptions.IgnoreCase);
    private static readonly Regex PropertySigRe = new(@"^(?<access>Public|Private|Friend)?\s*Property\s+(?<kind>Set|Let|Get)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?:As\s+(?<ret>[\w\.]+))?$", RegexOptions.IgnoreCase);
    private static readonly Regex DimRe = new(@"^(?<scope>Dim|Private|Public)\s+(?<withEvents>WithEvents\s+)?(?<name>\w+)\s+As\s+(?<type>[\w\.]+)(?<array>\(\))?$", RegexOptions.IgnoreCase);
    private static readonly Regex ConstRe = new(@"^(Public|Private)?\s*Const\s+(?<name>\w+)\s+As\s+(?<type>[\w\.]+)\s*=\s*(?<value>.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex AssignmentRe = new(@"^(?<lhs>[\w\.\(\)]+)\s*=\s*(?<rhs>.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex SetAssignmentRe = new(@"^Set\s+(?<lhs>[\w\.\(\)]+)\s*=\s*(?<rhs>.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex CallRe = new(@"^Call\s+(?<call>.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex LabelRe = new(@"^(?<label>[A-Za-z_]\w*):$", RegexOptions.IgnoreCase);
    private static readonly Regex GoToRe = new(@"^GoTo\s+(?<label>[A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
    private static readonly Regex ForRe = new(@"^For\s+(?<var>\w+)\s*=\s*(?<start>.+?)\s+To\s+(?<end>.+?)(?:\s+Step\s+(?<step>.+))?$", RegexOptions.IgnoreCase);
    private static readonly Regex ForEachRe = new(@"^For\s+Each\s+(?<var>\w+)\s+In\s+(?<expr>.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex InlineIfRe = new(@"^If\s+(?<cond>.+?)\s+Then\s+(?<stmt>.+)$", RegexOptions.IgnoreCase);
    private static readonly Regex NewAssignRe = new(@"^(?<lhs>[\w\.\(\)]+)\s*=\s*New\s+(?<type>[\w\.]+)$", RegexOptions.IgnoreCase);
    private static readonly Regex BracketMemberRe = new(@"\[(?<name>[^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex MeMemberRe = new(@"\bMe\.(?<name>\w+)\b", RegexOptions.IgnoreCase);
    private static readonly Regex LineNumberRe = new(@"^\s*\d+\s*:?\s+(?<body>.*)$", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Integer"] = "int", ["Long"] = "int", ["Double"] = "double", ["Single"] = "float",
        ["String"] = "string", ["Boolean"] = "bool", ["Date"] = "DateTime", ["Object"] = "object", ["Variant"] = "dynamic", ["Byte"] = "byte"
    };

    public string ConvertText(string code, string moduleName)
    {
        var sb = new StringBuilder();
        var indent = 1;

        if (options.Target == TargetLanguage.CSharp)
        {
            sb.AppendLine("using System;");
            sb.AppendLine("using Microsoft.VisualBasic;");
            sb.AppendLine();
            sb.AppendLine($"namespace {options.Namespace};");
            sb.AppendLine();
            sb.AppendLine($"public static class {moduleName}");
            sb.AppendLine("{");
        }
        else
        {
            sb.AppendLine($"Namespace {options.Namespace}");
            sb.AppendLine($"Public Module {moduleName}");
        }

        foreach (var raw in code.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                sb.AppendLine();
                continue;
            }

            var converted = ConvertLine(NormalizeInputLine(line));
            if (converted.CloseBefore) indent = Math.Max(1, indent - 1);
            sb.AppendLine($"{string.Concat(Enumerable.Repeat(options.Indent, indent))}{converted.Text}");
            if (converted.OpenAfter) indent++;
        }

        if (options.Target == TargetLanguage.CSharp) sb.AppendLine("}");
        else { sb.AppendLine("End Module"); sb.AppendLine("End Namespace"); }
        return sb.ToString();
    }

    private (string Text, bool CloseBefore, bool OpenAfter) ConvertLine(string input)
    {
        var line = NormalizeMemberAccess(ApplyRenames(input));
        if (options.AggressiveCompatibilityMode) line = CompatibilityCatalog.Apply(line);
        var lower = line.ToLowerInvariant();
        var closeBefore = lower.StartsWith("end if") || lower.StartsWith("end sub") || lower.StartsWith("end function") || lower == "loop" || lower.StartsWith("next") || lower.StartsWith("end select") || lower.StartsWith("end property") || lower.StartsWith("wend") || lower.StartsWith("loop until");

        if (lower.StartsWith("#if") || lower.StartsWith("#elseif") || lower.StartsWith("#else") || lower.StartsWith("#end if"))
            return (AsComment($"conditional compilation: {line}"), false, false);

        if (IsMetadataLine(lower)) return (AsComment($"metadata: {line}"), false, false);
        if (line.StartsWith("'")) return (AsComment(line.TrimStart('\'', ' ')), false, false);

        var label = LabelRe.Match(line);
        if (label.Success) return (options.Target == TargetLanguage.CSharp ? $"{label.Groups["label"].Value}: ;" : line, false, false);

        if (lower.StartsWith("on error goto")) return (AsComment($"exception routing: {line}"), false, false);
        if (lower.StartsWith("on error resume next")) return (AsComment("exception routing: resume next"), false, false);

        var gotoMatch = GoToRe.Match(line);
        if (gotoMatch.Success) return (options.Target == TargetLanguage.CSharp ? $"goto {gotoMatch.Groups["label"].Value};" : line, false, false);

        if (lower.StartsWith("resume next")) return (AsComment("resume next"), false, false);
        if (lower == "resume") return (AsComment("resume"), false, false);
        if (lower.StartsWith("resume "))
        {
            var dest = line[7..].Trim();
            return (options.Target == TargetLanguage.CSharp ? $"goto {dest};" : line, false, false);
        }

        var propertySig = PropertySigRe.Match(line);
        if (propertySig.Success) return ConvertPropertySignature(propertySig, closeBefore);

        var sig = SigRe.Match(line);
        if (sig.Success)
        {
            var access = NormalizeAccess(sig.Groups["access"].Value);
            var name = sig.Groups["name"].Value;
            var pars = ConvertParameters(sig.Groups["params"].Value);
            var ret = sig.Groups["ret"].Value;
            if (options.Target == TargetLanguage.CSharp)
            {
                var t = string.IsNullOrWhiteSpace(ret) ? "void" : MapType(ret);
                return ($"{access} static {t} {name}({pars}) {{", closeBefore, true);
            }
            return (string.IsNullOrWhiteSpace(ret) ? $"{access} Sub {name}({pars})" : $"{access} Function {name}({pars}) As {ret}", closeBefore, true);
        }

        var inlineIf = InlineIfRe.Match(line);
        if (inlineIf.Success && !line.TrimEnd().EndsWith("Then", StringComparison.OrdinalIgnoreCase))
        {
            var cond = ConvertExpr(inlineIf.Groups["cond"].Value);
            var stmt = inlineIf.Groups["stmt"].Value.Trim();
            return (options.Target == TargetLanguage.CSharp ? $"if ({cond}) {stmt};" : line, closeBefore, false);
        }

        if (lower.StartsWith("exit sub") || lower.StartsWith("exit function") || lower.StartsWith("exit property")) return (options.Target == TargetLanguage.CSharp ? "return;" : line, false, false);
        if (lower.StartsWith("end sub") || lower.StartsWith("end function") || lower.StartsWith("end property")) return (options.Target == TargetLanguage.CSharp ? "}" : line, closeBefore, false);
        if (lower.StartsWith("end if")) return (options.Target == TargetLanguage.CSharp ? "}" : "End If", closeBefore, false);
        if (lower == "else") return (options.Target == TargetLanguage.CSharp ? "} else {" : "Else", false, true);
        if (lower.StartsWith("elseif ") && lower.EndsWith(" then"))
        {
            var c = ConvertExpr(line[7..^5]);
            return (options.Target == TargetLanguage.CSharp ? $"}} else if ({c}) {{" : $"ElseIf {c} Then", false, true);
        }
        if (lower.StartsWith("if ") && lower.EndsWith(" then"))
        {
            var c = ConvertExpr(line[3..^5]);
            return (options.Target == TargetLanguage.CSharp ? $"if ({c}) {{" : $"If {c} Then", closeBefore, true);
        }

        if (lower.StartsWith("select case "))
        {
            var expr = ConvertExpr(line[12..]);
            return (options.Target == TargetLanguage.CSharp ? $"switch ({expr}) {{" : line, closeBefore, true);
        }
        if (lower.StartsWith("case else")) return (options.Target == TargetLanguage.CSharp ? "default:" : line, false, false);
        if (lower.StartsWith("case "))
        {
            var expr = ConvertExpr(line[5..]);
            return (options.Target == TargetLanguage.CSharp ? $"case {expr}:" : line, false, false);
        }
        if (lower.StartsWith("end select")) return (options.Target == TargetLanguage.CSharp ? "}" : line, true, false);

        var forMatch = ForRe.Match(line);
        if (forMatch.Success)
        {
            var variable = forMatch.Groups["var"].Value;
            var start = ConvertExpr(forMatch.Groups["start"].Value);
            var end = ConvertExpr(forMatch.Groups["end"].Value);
            var step = forMatch.Groups["step"].Success ? ConvertExpr(forMatch.Groups["step"].Value) : "1";
            if (options.Target == TargetLanguage.CSharp)
            {
                var negative = step.StartsWith("-", StringComparison.Ordinal);
                var comparator = negative ? ">=" : "<=";
                var op = negative ? "-=" : "+=";
                var normalizedStep = negative ? step[1..] : step;
                return ($"for (var {variable} = {start}; {variable} {comparator} {end}; {variable} {op} {normalizedStep}) {{", closeBefore, true);
            }
            return (line, closeBefore, true);
        }
        var forEachMatch = ForEachRe.Match(line);
        if (forEachMatch.Success)
        {
            var variable = forEachMatch.Groups["var"].Value;
            var expr = ConvertExpr(forEachMatch.Groups["expr"].Value);
            return (options.Target == TargetLanguage.CSharp ? $"foreach (var {variable} in {expr}) {{" : line, closeBefore, true);
        }
        if (lower.StartsWith("next")) return (options.Target == TargetLanguage.CSharp ? "}" : line, true, false);

        if (lower.StartsWith("do while ")) return (options.Target == TargetLanguage.CSharp ? $"while ({ConvertExpr(line[9..])}) {{" : line, closeBefore, true);
        if (lower == "do") return (options.Target == TargetLanguage.CSharp ? "while (true) {" : line, closeBefore, true);
        if (lower == "loop") return (options.Target == TargetLanguage.CSharp ? "}" : line, true, false);
        if (lower.StartsWith("loop until ")) return (options.Target == TargetLanguage.CSharp ? $"if ({ConvertExpr(line[11..])}) break;" : line, true, false);
        if (lower.StartsWith("while ")) return (options.Target == TargetLanguage.CSharp ? $"while ({ConvertExpr(line[6..])}) {{" : line, closeBefore, true);
        if (lower == "wend") return (options.Target == TargetLanguage.CSharp ? "}" : line, true, false);

        var dim = DimRe.Match(line);
        if (dim.Success)
        {
            var n = dim.Groups["name"].Value;
            var t = dim.Groups["type"].Value;
            var isArray = dim.Groups["array"].Success;
            if (options.Target == TargetLanguage.CSharp)
            {
                var csharpAccess = dim.Groups["scope"].Value.Equals("Public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
                var typeName = MapType(t) + (isArray ? "[]" : string.Empty);
                return ($"{csharpAccess} static {typeName} {n};", closeBefore, false);
            }
            return ($"Dim {n} As {t}{(isArray ? "()" : string.Empty)}", closeBefore, false);
        }

        var cons = ConstRe.Match(line);
        if (cons.Success)
        {
            var n = cons.Groups["name"].Value;
            var t = cons.Groups["type"].Value;
            var v = ConvertExpr(cons.Groups["value"].Value);
            return (options.Target == TargetLanguage.CSharp ? $"const {MapType(t)} {n} = {v};" : $"Const {n} As {t} = {v}", closeBefore, false);
        }

        var newAssign = NewAssignRe.Match(line);
        if (newAssign.Success)
        {
            var lhs = newAssign.Groups["lhs"].Value;
            var typeName = newAssign.Groups["type"].Value;
            return (options.Target == TargetLanguage.CSharp ? $"{lhs} = new {typeName}();" : line, closeBefore, false);
        }

        var setAssign = SetAssignmentRe.Match(line);
        if (setAssign.Success)
            return (options.Target == TargetLanguage.CSharp ? $"{setAssign.Groups["lhs"].Value} = {ConvertExpr(setAssign.Groups["rhs"].Value)};" : line, closeBefore, false);

        var call = CallRe.Match(line);
        if (call.Success)
            return (options.Target == TargetLanguage.CSharp ? $"{ConvertExpr(call.Groups["call"].Value)};" : line, closeBefore, false);

        var hint = CompatibilityCatalog.GetStatementHint(line);
        if (hint is not null) return (hint, closeBefore, false);

        var assignment = AssignmentRe.Match(line);
        if (assignment.Success && !line.StartsWith("If ", StringComparison.OrdinalIgnoreCase))
            return (options.Target == TargetLanguage.CSharp ? $"{assignment.Groups["lhs"].Value} = {ConvertExpr(assignment.Groups["rhs"].Value)};" : line, closeBefore, false);

        if (options.Target == TargetLanguage.CSharp && options.IncludeTodoComments) return ($"// TODO: verify conversion: {line}", closeBefore, false);
        return (line, closeBefore, false);
    }

    private (string Text, bool CloseBefore, bool OpenAfter) ConvertPropertySignature(Match propertySig, bool closeBefore)
    {
        var access = NormalizeAccess(propertySig.Groups["access"].Value);
        var kind = propertySig.Groups["kind"].Value.ToLowerInvariant();
        var name = propertySig.Groups["name"].Value;
        var pars = ConvertParameters(propertySig.Groups["params"].Value);
        var ret = propertySig.Groups["ret"].Value;

        if (options.Target == TargetLanguage.CSharp)
        {
            if (kind == "get")
            {
                var typeName = string.IsNullOrWhiteSpace(ret) ? "object" : MapType(ret);
                return ($"{access} static {typeName} Get_{name}({pars}) {{", closeBefore, true);
            }
            return ($"{access} static void Set_{name}({pars}) {{", closeBefore, true);
        }

        return ($"{access} Property {char.ToUpper(kind[0]) + kind[1..]} {name}({pars})", closeBefore, true);
    }

    private bool IsMetadataLine(string lower) => lower.StartsWith("version ") || lower == "begin" || lower == "end" || lower.StartsWith("attribute ") || lower.StartsWith("option ");
    private string AsComment(string text) => options.Target == TargetLanguage.CSharp ? $"// {text}" : $"' {text}";

    private string NormalizeMemberAccess(string line)
    {
        var normalized = BracketMemberRe.Replace(line, m => m.Groups["name"].Value);
        normalized = MeMemberRe.Replace(normalized, m => m.Groups["name"].Value);
        return normalized;
    }

    private string NormalizeInputLine(string line)
    {
        var normalized = line.Trim();
        if (!options.RemoveLineNumbers) return normalized;
        var match = LineNumberRe.Match(normalized);
        return match.Success ? match.Groups["body"].Value.TrimStart() : normalized;
    }

    private string ConvertParameters(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return string.Join(", ", input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(param =>
        {
            var original = param.Trim();
            var byRef = Regex.IsMatch(original, "^ByRef\\s+", RegexOptions.IgnoreCase);
            var isParamArray = Regex.IsMatch(original, "^ParamArray\\s+", RegexOptions.IgnoreCase);
            var cleaned = Regex.Replace(original, "^(ByVal|ByRef|Optional|ParamArray)\\s+", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, "\\s*=\\s*.+$", string.Empty);
            var parts = Regex.Split(cleaned, "\\s+As\\s+", RegexOptions.IgnoreCase);
            if (parts.Length != 2) return cleaned;
            if (options.Target != TargetLanguage.CSharp) return $"{parts[0]} As {parts[1]}";
            var prefix = byRef ? "ref " : string.Empty;
            var typeName = MapType(parts[1]) + (isParamArray ? "[]" : string.Empty);
            return $"{prefix}{typeName} {parts[0]}";
        }));
    }

    private string ConvertExpr(string expr)
    {
        var text = expr.Trim();
        if (options.Target == TargetLanguage.CSharp)
        {
            text = Regex.Replace(text, @"\b(?<name>[A-Za-z_]\w*)\s*:=", "${name}: "); // named args
            text = Regex.Replace(text, @"\bNot\s+\((?<body>[^)]+)\)", m => $"!({ConvertExpr(m.Groups["body"].Value)})", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bNot\b", "!", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bAndAlso\b|\bAnd\b", "&&", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bOrElse\b|\bOr\b", "||", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bIs\s+Nothing\b|\bIs\s+null\b", "== null", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bIs\s+Not\s+Nothing\b|\bIs\s+Not\s+null\b", "!= null", RegexOptions.IgnoreCase);
            text = text.Replace("<>", "!=");
            text = Regex.Replace(text, @"(?<![<>=!])=(?!=)", "==");
            text = Regex.Replace(text, @"\s\+\+\s", " && ");
            text = text.Replace("&", "+");
        }
        return text;
    }

    private string ApplyRenames(string line)
    {
        var outLine = line;
        foreach (var (src, dst) in options.Rename.FunctionNames.Concat(options.Rename.VariableNames))
            outLine = Regex.Replace(outLine, $@"\b{Regex.Escape(src)}\b", dst);
        foreach (var (src, dst) in options.Rename.StringLiterals)
            outLine = outLine.Replace($"\"{src}\"", $"\"{dst}\"");
        return outLine;
    }

    private string NormalizeAccess(string access)
    {
        if (options.Target == TargetLanguage.CSharp) return access.Equals("Public", StringComparison.OrdinalIgnoreCase) ? "public" : "private";
        return string.IsNullOrWhiteSpace(access) ? "Private" : access;
    }

    private string MapType(string vbType)
    {
        var normalized = vbType.Trim();
        if (options.Target == TargetLanguage.VbNet) return normalized;
        if (normalized.Contains('.')) return "dynamic";
        return TypeMap.GetValueOrDefault(normalized, normalized);
    }
}
