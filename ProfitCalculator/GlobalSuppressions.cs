﻿// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Critical Code Smell", "S2696:Instance members should not write to \"static\" fields", Justification = "<Pending>", Scope = "member", Target = "~M:ProfitCalculator.ui.DropdownOption.Update")]
[assembly: SuppressMessage("Critical Code Smell", "S2696:Instance members should not write to \"static\" fields", Justification = "<Pending>", Scope = "member", Target = "~M:ProfitCalculator.ModEntry.OnButtonPressed(System.Object,StardewModdingAPI.Events.ButtonPressedEventArgs)")]
[assembly: SuppressMessage("Critical Code Smell", "S2696:Instance members should not write to \"static\" fields", Justification = "<Pending>", Scope = "member", Target = "~M:ProfitCalculator.ui.DropdownOption.ReceiveScrollWheelAction(System.Int32)")]
[assembly: SuppressMessage("Critical Code Smell", "S2696:Instance members should not write to \"static\" fields", Justification = "<Pending>", Scope = "member", Target = "~M:ProfitCalculator.ModEntry.OnGameLaunchedAPIs(System.Object,StardewModdingAPI.Events.GameLaunchedEventArgs)")]
[assembly: SuppressMessage("Major Bug", "S1244:Floating point numbers should not be tested for equality", Justification = "In this case, floats will be exact because they are read from txt files and not from calculations", Scope = "module")]