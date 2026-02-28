using Vb6ModernConverter.Core;
using Xunit;

namespace Vb6ModernConverter.Tests;

public class ConverterTests
{
    [Fact]
    public void Converts_Core_Constructs_To_CSharp()
    {
        var source = "Private Function Add(ByVal x As Integer, ByVal y As Integer) As Integer\nDim total As Integer\ntotal = x + y\nEnd Function";
        var result = new Vb6Converter(new ConversionOptions { Target = TargetLanguage.CSharp }).ConvertText(source, "MathModule");
        Assert.Contains("private static int Add(int x, int y)", result);
        Assert.Contains("private static int total;", result);
    }

    [Fact]
    public void Converts_Compatibility_Items_And_Functions()
    {
        var source = "txtName.Caption = vbNullString\nlstPeople.AddItem \"A\"\nIf KeyCode = vbKeyTab Then\nx = Left(name, 3)\nBeep";
        var result = new Vb6Converter(new ConversionOptions { Target = TargetLanguage.CSharp, AggressiveCompatibilityMode = true }).ConvertText(source, "CompatModule");
        Assert.Contains("txtName.Text = string.Empty;", result);
        Assert.Contains("lstPeople.Add \"A\"", result);
        Assert.Contains("if (KeyCode == Keys.Tab)", result);
        Assert.Contains("Microsoft.VisualBasic.Strings.Left", result);
        Assert.Contains("Console.Beep();", result);
    }

    [Fact]
    public void Converts_Named_And_Optional_And_ParamArray_Arguments()
    {
        var source = "Public Sub DoWork(Optional ByVal a As Integer = 1, ByRef b As String, ParamArray args As Variant)\nCall Save(Name:=\"A\", Count:=2)\nEnd Sub";
        var result = new Vb6Converter(new ConversionOptions { Target = TargetLanguage.CSharp }).ConvertText(source, "ArgsModule");
        Assert.Contains("private static void DoWork(int a, ref string b, dynamic[] args)", result);
        Assert.Contains("Save(Name: \"A\", Count: 2);", result);
    }

    [Fact]
    public void Converts_Conditional_Compilation_And_LineNumbers()
    {
        var source = "10 #If DEBUG Then\n20 MsgBox \"x\"\n30 #End If";
        var result = new Vb6Converter(new ConversionOptions { Target = TargetLanguage.CSharp, RemoveLineNumbers = true }).ConvertText(source, "CondModule");
        Assert.Contains("// conditional compilation: #If DEBUG Then", result);
        Assert.Contains("// conditional compilation: #End If", result);
        Assert.DoesNotContain("10 #If", result);
    }

    [Fact]
    public void Converts_ForEach_Array_And_OnErrorResumeNext()
    {
        var source = "Dim items As String()\nFor Each item In items\nNext item\nOn Error Resume Next";
        var result = new Vb6Converter(new ConversionOptions { Target = TargetLanguage.CSharp }).ConvertText(source, "LoopModule");
        Assert.Contains("private static string[] items;", result);
        Assert.Contains("foreach (var item in items)", result);
        Assert.Contains("// exception routing: resume next", result);
    }
}
