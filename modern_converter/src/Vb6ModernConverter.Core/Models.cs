namespace Vb6ModernConverter.Core;

public enum TargetLanguage
{
    CSharp,
    VbNet
}

public sealed class RenameOptions
{
    public Dictionary<string, string> FunctionNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> VariableNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> StringLiterals { get; set; } = new(StringComparer.Ordinal);
}

public sealed class ConversionOptions
{
    public TargetLanguage Target { get; set; } = TargetLanguage.CSharp;
    public string Namespace { get; set; } = "Converted.VB6";
    public string ProjectName { get; set; } = "ConvertedProject";
    public string Indent { get; set; } = "    ";
    public bool IncludeTodoComments { get; set; } = true;
    public bool RemoveLineNumbers { get; set; }
    public bool AggressiveCompatibilityMode { get; set; } = true;
    public RenameOptions Rename { get; set; } = new();
}
