# VB6 Modern Converter (Python)

A modernized VB6 converter that is simple to use and practical for real migration projects.

## 1-click style workflow

```bash
cd modern_converter
./install.sh
./init_conversion.sh ../ ./converted csharp
```

- `install.sh` installs the tool in editable mode.
- `init_conversion.sh` runs a full folder conversion and generates Visual Studio solution/project files.

## Conversion highlights

- Converts `.bas`, `.frm`, and `.cls` files.
- Targets **C#** or **VB.NET**.
- Wraps output into namespace/module/class structures for cleaner imports.
- Generates Visual Studio-friendly `.sln` + `.csproj` / `.vbproj` (with `--create-vs-solution`).
- Supports rename customization during conversion:
  - function names
  - variable names
  - string literal values

## Rename config

Create `rename.json`:

```json
{
  "function_names": { "OldSub": "NewSub" },
  "variable_names": { "legacyValue": "modernValue" },
  "string_literals": { "Old Product": "New Product" }
}
```

Run conversion:

```bash
python -m vb6_converter.cli ../ ./converted \
  --target csharp \
  --rename-config ./rename.json \
  --create-vs-solution \
  --project-name MigratedApp \
  --namespace Company.LegacyMigration
```

## Visual Studio 2026 import

Open the generated `.sln` file in Visual Studio. The SDK-style project includes all converted source files with wildcard includes and modern project properties.

## Quality

```bash
python -m pytest -q
```
