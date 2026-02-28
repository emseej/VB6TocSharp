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
- Optional `--remove-line-numbers` flag strips legacy VB6 numeric line labels before conversion.
- Supports rename customizations via config JSON:
  - function names
  - variable names
  - string literals
- Generates Visual Studio-importable project + solution files during conversion.

- Aggressive compatibility layer for VB6-to-.NET migration:
  - normalizes common control methods (`AddItem`, `RemoveItem`, `SetFocus`, `Repaint`, `UndoAction`, `RedoAction`, `Dropdown`, etc.)
  - maps common VB6 control properties (`Caption`, `ListIndex`, `ListCount`, etc.)
  - maps common VB6 constants/keys (`vbNullString`, `vbKeyTab`, `vbYes`, `vbNo`, etc.)
- Enterprise-focused conversion rules for common Access/VB6 class patterns:
  - metadata/header normalization (`VERSION`, `Attribute`, `Option`)
  - `Property Set/Let/Get` conversion to explicit C# methods
  - `Set` assignments and `WithEvents` typed declarations
  - stronger boolean/comparison normalization (`Not/And/Or`, `<>`, `=` in conditions)

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
dotnet run --project ./src/Vb6ModernConverter -- ../ ./converted --target vbnet --config ./conversion.json --remove-line-numbers
```

## Test

```bash
dotnet test ./Vb6ModernConverter.sln
```

## Enterprise conversion coverage

The converter now supports broader VB6 control-flow and class-module conversion:

- `Sub` / `Function` / `Property Set|Let|Get` signatures
- `Dim`/`Const` including `WithEvents` declarations
- `Set` assignments and `Call` statements
- `If/ElseIf/Else/End If`
- `Select Case` / `Case` / `Case Else` / `End Select`
- `For ... To ... Step` / `Next`
- `Do While` / `Do` / `Loop` / `Loop Until` / `While` / `Wend`
- `GoTo`, labels, `Resume`, and `On Error GoTo` (mapped to explicit control flow/comments)
- Metadata normalization for class headers (`VERSION`, `Attribute`, `Option`)

This is a high-coverage migration accelerator designed to reduce manual cleanup significantly for large VB6 codebases, while still leaving explicit markers where semantic review is required.


## Extending enterprise coverage

The converter now uses a centralized `CompatibilityCatalog` for constants, properties, methods, functions, operators, and statement hints. Add additional VB6 entries to this catalog to expand conversion breadth across your codebase without changing parser logic.


## Reference basis

The compatibility and statement maps are curated using VBA language/reference patterns (including TutorialsPoint VBA reference topics) to drive enterprise-scale VB6-to-C#/VB.NET migration rules.
