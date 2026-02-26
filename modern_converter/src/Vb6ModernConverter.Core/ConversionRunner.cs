namespace Vb6ModernConverter.Core;

public sealed class ConversionRunner
{
    public void ConvertPath(string source, string destination, ConversionOptions options)
    {
        var src = new DirectoryInfo(source);
        if (!src.Exists) throw new DirectoryNotFoundException(source);

        var dest = new DirectoryInfo(destination);
        if (!dest.Exists) dest.Create();

        var converter = new Vb6Converter(options);
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bas", ".cls", ".frm" };
        var outExt = options.Target == TargetLanguage.CSharp ? ".cs" : ".vb";

        foreach (var file in src.EnumerateFiles("*.*", SearchOption.AllDirectories).Where(f => extensions.Contains(f.Extension)))
        {
            var relative = Path.GetRelativePath(src.FullName, file.FullName);
            var target = Path.Combine(dest.FullName, Path.ChangeExtension(relative, outExt));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var converted = converter.ConvertText(File.ReadAllText(file.FullName), Path.GetFileNameWithoutExtension(file.Name));
            File.WriteAllText(target, converted);
        }

        SolutionScaffolder.Write(destination, options);
    }
}
