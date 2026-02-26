# VB6 Modern Converter (C#)

This is now a **C# application** (not Python) for configuring and performing VB6 to C# / VB.NET conversion.

## Quick start

```bash
cd modern_converter
./install.sh
./init_conversion.sh ../ ./converted csharp
```

## Features

- C# console converter app built with .NET.
- Converts `.bas`, `.frm`, `.cls` files.
- Targets `csharp` or `vbnet`.
- Supports rename customizations via config JSON:
  - function names
  - variable names
  - string literals
- Generates Visual Studio-importable project + solution files during conversion.

## Configuration example

Create `conversion.json`:

```json
{
  "namespace": "Company.LegacyMigration",
  "projectName": "MigratedApp",
  "includeTodoComments": true,
  "rename": {
    "functionNames": { "OldSub": "NewSub" },
    "variableNames": { "legacyValue": "modernValue" },
    "stringLiterals": { "OLD": "NEW" }
  }
}
```

Run:

```bash
./init_conversion.sh ../ ./converted csharp ./conversion.json
```

## Manual run

```bash
dotnet run --project ./src/Vb6ModernConverter -- ../ ./converted --target vbnet --config ./conversion.json
```

## Test

```bash
dotnet test ./Vb6ModernConverter.sln
```
