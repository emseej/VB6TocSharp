using System.Text.Json;
using Vb6ModernConverter.Core;

if (args.Length < 2)
{
    Console.WriteLine("Usage: Vb6ModernConverter <source_dir> <destination_dir> [--target csharp|vbnet] [--config conversion.json]");
    return 1;
}

var source = args[0];
var destination = args[1];
var target = TargetLanguage.CSharp;
var configPath = string.Empty;

for (var i = 2; i < args.Length; i++)
{
    if (args[i] == "--target" && i + 1 < args.Length)
    {
        target = args[i + 1].Equals("vbnet", StringComparison.OrdinalIgnoreCase) ? TargetLanguage.VbNet : TargetLanguage.CSharp;
        i++;
    }
    else if (args[i] == "--config" && i + 1 < args.Length)
    {
        configPath = args[i + 1];
        i++;
    }
}

ConversionOptions options;
if (!string.IsNullOrWhiteSpace(configPath) && File.Exists(configPath))
{
    var json = File.ReadAllText(configPath);
    options = JsonSerializer.Deserialize<ConversionOptions>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ConversionOptions();
    options.Target = target;
}
else
{
    options = new ConversionOptions { Target = target };
}

new ConversionRunner().ConvertPath(source, destination, options);
Console.WriteLine($"Conversion complete: {source} -> {destination}");
return 0;
