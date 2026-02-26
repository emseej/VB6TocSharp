#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ $# -lt 2 ]]; then
  echo "Usage: ./init_conversion.sh <vb6_source_dir> <output_dir> [csharp|vbnet] [config.json]"
  exit 1
fi
TARGET="${3:-csharp}"
CONFIG_ARG=""
if [[ ${4:-} != "" ]]; then
  CONFIG_ARG="--config $4"
fi
dotnet run --project ./src/Vb6ModernConverter -- "$1" "$2" --target "$TARGET" ${CONFIG_ARG}
