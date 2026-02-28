#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
dotnet restore Vb6ModernConverter.sln
dotnet build Vb6ModernConverter.sln -c Release
echo "Vb6ModernConverter build complete."
