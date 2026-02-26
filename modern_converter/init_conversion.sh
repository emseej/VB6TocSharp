#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ $# -lt 2 ]]; then
  echo "Usage: ./init_conversion.sh <vb6_source_dir> <output_dir> [csharp|vbnet]"
  exit 1
fi
TARGET="${3:-csharp}"
python -m vb6_converter.cli "$1" "$2" --target "$TARGET" --create-vs-solution --project-name ConvertedProject --namespace Converted.VB6
