# 6135's Stardew Valley Mods Repository

[![Codacy Badge](https://api.codacy.com/project/badge/Grade/ddb41dbbeac1420284ee60bb5dddf436)](https://app.codacy.com/gh/6135/StardewValleyMods?utm_source=github.com&utm_medium=referral&utm_content=6135/StardewValleyMods&utm_campaign=Badge_Grade)
[![CodeFactor](https://www.codefactor.io/repository/github/6135/stardewvalleymods/badge)](https://www.codefactor.io/repository/github/6135/stardewvalleymods)

This is my collection of asorted SDV Mods.

Mods included in this repository:

| Name                                                                        | Version | Working Version | Main         | Docs                                                                                                           | Notes                            |
|-----------------------------------------------------------------------------|---------|-----------------|--------------|-------------------------------------------------------------------------------------------------------------------------|----------------------------------|
| [Profit Calculator](https://www.nexusmods.com/stardewvalley/mods/19931)     | 1.0.4   | 1.6.*            | Yes          | [Docs](https://github.com/6135/StardewValleyMods/blob/master/ProfitCalculator/README.md)                                |                                  |
| [Profit Calculator DGA](https://www.nexusmods.com/stardewvalley/mods/19931) | 1.0.4   | 1.5.*            | Addon for PC | [Docs](https://github.com/6135/StardewValleyMods/blob/master/ProfitCalculatorDGA/README.md)                             | Not Needed in 1.6 and above      |
| [Various Coal Ores](https://www.nexusmods.com/stardewvalley/mods/21507)     | 1.2.3   | 1.6.*           | Yes          | [Docs](https://github.com/6135/StardewValleyMods/blob/master/%5BCP%5D%20Various%20Coal%20Ores%20%2B%20Extras/README.MD) | Updated from Dojando's mod       |
| [Pizza Nodes](https://www.nexusmods.com/stardewvalley/mods/21625)           | 1.0.0   | 1.6.*           | Yes          | [Docs](https://github.com/6135/StardewValleyMods/blob/master/%5BCP%5D%20Pizza%20Nodes/README.MD)                        |                                  |
| [UI Framework](StardewUIFramework)                                          | 1.0.0   | 1.6.*           | Yes          | [Docs](https://github.com/6135/StardewValleyMods/blob/master/StardewUIFramework/README.md)                              | Library mod; see architecture.md |
| [UI Framework Example](UIFrameworkExample)                                  | 1.0.0   | 1.6.*           | No           | [Docs](https://github.com/6135/StardewValleyMods/blob/master/StardewUIFramework/README.md)                              | Sample consumer, not for players |

## Building

The C# projects are built with `dotnet build` (or `Stardew Mods.sln` in Visual Studio) and use
[Pathoschild.Stardew.ModBuildConfig](https://github.com/Pathoschild/SMAPI/blob/develop/docs/technical/mod-package.md).
The `STARDEW_GAME_DIR` environment variable must point at the Stardew Valley game folder (the one containing
`Stardew Valley.dll` and `Mods/`) or the game and SMAPI references cannot be resolved and the build fails; CI needs
it set as well. After a successful build the mod is copied into the game's `Mods` folder and a release zip is
created next to the build output.
