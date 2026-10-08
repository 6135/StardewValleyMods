# SMAPI Mod Templates

Updated replacement for Platonymous' SMAPI Templates, targeting Stardew Valley 1.6 / SMAPI 4 (.NET 6, ModBuildConfig 4.x).

Visual Studio 2026 lists installed `dotnet new` templates in **Create a new project**, so no VSIX is needed.

## Install
```
dotnet pack -c Release
dotnet new install bin/Release/SmapiTemplates.2.0.0.nupkg
```
Restart Visual Studio, then search "SMAPI". CLI: `dotnet new smapimod -n MyMod --Author Me`.

## Templates
| Short name | Description |
|---|---|
| `smapimod` | C# SMAPI mod |
| `smapicp` | Content Patcher pack (adds an example item) |
| `smapiie` | Content Patcher pack using Item Extensions |

Content pack projects have no code: building copies the pack to `$(GamePath)\Mods\[CP] <Name>` (default Steam path, override `GamePath` if needed).
