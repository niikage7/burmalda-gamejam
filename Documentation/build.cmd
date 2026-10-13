@echo off
rem Собирает сайт документации в Documentation\_site. Аргумент --serve открывает его на http://localhost:8080
cd /d "%~dp0.."
dotnet tool restore || exit /b 1
dotnet run Documentation/tools/InspectorParams.cs || exit /b 1
dotnet docfx Documentation/docfx.json %*
