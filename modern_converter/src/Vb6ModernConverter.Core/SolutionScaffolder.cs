namespace Vb6ModernConverter.Core;

public static class SolutionScaffolder
{
    public static void Write(string outputDir, ConversionOptions options)
    {
        var projectFile = Path.Combine(outputDir, options.ProjectName + (options.Target == TargetLanguage.CSharp ? ".csproj" : ".vbproj"));
        var compileExt = options.Target == TargetLanguage.CSharp ? "cs" : "vb";
        var projectText = $"""<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n    <LangVersion>latest</LangVersion>\n    <RootNamespace>{options.Namespace}</RootNamespace>\n  </PropertyGroup>\n  <ItemGroup>\n    <Compile Include=\"**/*.{compileExt}\" Exclude=\"bin/**;obj/**\" />\n  </ItemGroup>\n</Project>\n""";
        File.WriteAllText(projectFile, projectText);

        var slnPath = Path.Combine(outputDir, options.ProjectName + ".sln");
        File.WriteAllText(slnPath, "Microsoft Visual Studio Solution File, Format Version 12.00\n# Visual Studio Version 17\nGlobal\nEndGlobal\n");
    }
}
