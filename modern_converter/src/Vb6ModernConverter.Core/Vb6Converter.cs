using System.Text;
using System.Text.RegularExpressions;

namespace Vb6ModernConverter.Core;

public sealed class Vb6Converter(ConversionOptions options)
{
    private static readonly Regex SigRe = new(@"^(?<access>Public|Private|Friend)?\s*(?<kind>Sub|Function)\s+(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?:As\s+(?<ret>\w+))?$", RegexOptions.IgnoreCase);
    private static readonly Regex DimRe = new(@"^(Dim|Private|Public)\s+(?<name>\w+)\s+As\s+(?<type>\w+)$", RegexOptions.IgnoreCase);
    private static readonly Regex ConstRe = new(@"^(Public|Private)?\s*Const\s+(?<name>\w+)\s+As\s+(?<type>\w+)\s*=\s*(?<value>.+)$", RegexOptions.IgnoreCase);

    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Integer"] = "int", ["Long"] = "int", ["Double"] = "double", ["Single"] = "float",
        ["String"] = "string", ["Boolean"] = "bool", ["Date"] = "DateTime", ["Object"] = "object", ["Variant"] = "dynamic"
    };

    public string ConvertText(string code, string moduleName)
    {
        var sb = new StringBuilder();
        var indent = 1;

        if (options.Target == TargetLanguage.CSharp)
        {
            sb.AppendLine("using System;");
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

            var converted = ConvertLine(line.Trim());
            if (converted.CloseBefore) indent = Math.Max(1, indent - 1);
            sb.AppendLine($"{string.Concat(Enumerable.Repeat(options.Indent, indent))}{converted.Text}");
            if (converted.OpenAfter) indent++;
        }

        if (options.Target == TargetLanguage.CSharp) sb.AppendLine("}");
        else { sb.AppendLine("End Module"); sb.AppendLine("End Namespace"); }
        return sb.ToString();
    }

    private (string Text, bool CloseBefore, bool OpenAfter) ConvertLine(string line)
    {
        line = ApplyRenames(line);
        var lower = line.ToLowerInvariant();
        var closeBefore = lower.StartsWith("end if") || lower.StartsWith("end sub") || lower.StartsWith("end function") || lower == "loop" || lower.StartsWith("next") || lower.StartsWith("end select");

        var sig = SigRe.Match(line);
        if (sig.Success)
        {
            var access = (sig.Groups["access"].Value.Length == 0 ? "Private" : sig.Groups["access"].Value).ToLowerInvariant();
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

        if (lower.StartsWith("end sub") || lower.StartsWith("end function")) return (options.Target == TargetLanguage.CSharp ? "}" : line, closeBefore, false);
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

        var dim = DimRe.Match(line);
        if (dim.Success)
        {
            var n = dim.Groups["name"].Value;
            var t = dim.Groups["type"].Value;
            return (options.Target == TargetLanguage.CSharp ? $"{MapType(t)} {n};" : $"Dim {n} As {t}", closeBefore, false);
        }

        var cons = ConstRe.Match(line);
        if (cons.Success)
        {
            var n = cons.Groups["name"].Value;
            var t = cons.Groups["type"].Value;
            var v = ConvertExpr(cons.Groups["value"].Value);
            return (options.Target == TargetLanguage.CSharp ? $"const {MapType(t)} {n} = {v};" : $"Const {n} As {t} = {v}", closeBefore, false);
        }

        if (line.Contains('=') && !line.StartsWith("If ", StringComparison.OrdinalIgnoreCase))
        {
            var ix = line.IndexOf('=');
            var lhs = line[..ix].Trim();
            var rhs = ConvertExpr(line[(ix + 1)..]);
            return (options.Target == TargetLanguage.CSharp ? $"{lhs} = {rhs};" : $"{lhs} = {rhs}", closeBefore, false);
        }

        if (options.Target == TargetLanguage.CSharp && options.IncludeTodoComments)
            return ($"// TODO: verify conversion: {line}", closeBefore, false);

        return (line, closeBefore, false);
    }

    private string ConvertParameters(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return string.Join(", ", input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(param =>
        {
            var cleaned = Regex.Replace(param, "^(ByVal|ByRef|Optional)\\s+", string.Empty, RegexOptions.IgnoreCase);
            var parts = Regex.Split(cleaned, "\\s+As\\s+", RegexOptions.IgnoreCase);
            if (parts.Length != 2) return cleaned;
            return options.Target == TargetLanguage.CSharp ? $"{MapType(parts[1])} {parts[0]}" : $"{parts[0]} As {parts[1]}";
        }));
    }

    private string ConvertExpr(string expr)
    {
        var text = expr.Trim();
        if (options.Target == TargetLanguage.CSharp)
        {
            text = text.Replace("<>", "!=").Replace(" AndAlso ", " && ").Replace(" And ", " && ")
                .Replace(" OrElse ", " || ").Replace(" Or ", " || ").Replace("True", "true").Replace("False", "false").Replace("Nothing", "null").Replace("&", "+");
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

    private string MapType(string vbType) => options.Target == TargetLanguage.VbNet ? vbType : TypeMap.GetValueOrDefault(vbType.Trim(), vbType.Trim());
}
