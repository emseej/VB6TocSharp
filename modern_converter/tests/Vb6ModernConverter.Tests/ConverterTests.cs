using Vb6ModernConverter.Core;
using Xunit;

namespace Vb6ModernConverter.Tests;

public class ConverterTests
{
    [Fact]
    public void Converts_Function_And_Assignment_To_CSharp()
    {
        var source = "Private Function Add(ByVal x As Integer, ByVal y As Integer) As Integer\nDim total As Integer\ntotal = x + y\nEnd Function";
        var converter = new Vb6Converter(new ConversionOptions { Target = TargetLanguage.CSharp });

        var result = converter.ConvertText(source, "MathModule");

        Assert.Contains("public static class MathModule", result);
        Assert.Contains("private static int Add(int x, int y)", result);
        Assert.Contains("int total;", result);
        Assert.Contains("total = x + y;", result);
    }

    [Fact]
    public void Applies_Rename_Options()
    {
        var source = "Public Sub OldSub()\nDim oldValue As String\noldValue = \"OLD\"\nEnd Sub";
        var options = new ConversionOptions
        {
            Target = TargetLanguage.CSharp,
            Rename = new RenameOptions
            {
                FunctionNames = new() { ["OldSub"] = "NewSub" },
                VariableNames = new() { ["oldValue"] = "newValue" },
                StringLiterals = new() { ["OLD"] = "NEW" }
            }
        };

        var result = new Vb6Converter(options).ConvertText(source, "Module1");

        Assert.Contains("NewSub", result);
        Assert.Contains("newValue", result);
        Assert.Contains("\"NEW\"", result);
    }
}
