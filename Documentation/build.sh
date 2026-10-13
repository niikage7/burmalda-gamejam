#!/usr/bin/env sh
# Собирает сайт документации в Documentation/_site.
#   ./Documentation/build.sh          — только собрать
#   ./Documentation/build.sh --serve  — собрать и открыть на http://localhost:8080
set -e
cd "$(dirname "$0")/.."
dotnet tool restore
dotnet run Documentation/tools/InspectorParams.cs
dotnet docfx Documentation/docfx.json "$@"
