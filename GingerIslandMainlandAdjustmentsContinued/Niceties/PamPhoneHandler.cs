namespace GingerIslandMainlandAdjustments.Niceties;

using GingerIslandMainlandAdjustments.AssetManagers;
using GingerIslandMainlandAdjustments.MultiplayerHandler;

using Microsoft.Xna.Framework.Graphics;

using StardewValley.Objects;

/// <summary>
/// Handles Pam's phone call.
/// </summary>
internal sealed class PamPhoneHandler : IPhoneHandler
{
    /// <inheritdoc />
    public string? CheckForIncomingCall(Random random) => null;

    /// <inheritdoc />
    public bool TryHandleIncomingCall(string callId, out Action? showDialogue)
    {
        showDialogue = null;
        return false;
    }

    /// <inheritdoc />
    public IEnumerable<KeyValuePair<string, string>> GetOutgoingNumbers()
    {
        if (Game1.player.mailReceived.Contains(AssetEditor.PAMMAILKEY) && Game1.getCharacterFromName("Pam") is NPC pam)
        {
            yield return new ("PamBus", pam.displayName);
        }
    }

    /// <inheritdoc />
    public bool TryHandleOutgoingCall(string callId)
    {
        if (callId != "PamBus")
        {
            return false;
        }

        GameLocation location = Game1.currentLocation;
        location.playShopPhoneNumberSounds("PamBus");
        Game1.player.freezePause = 4950;

        DelayedAction.functionAfterDelay(
            func: () =>
            {
                try
                {
                    Game1.playSound(GameLocation.PHONE_PICKUP_SOUND);
                    if (Game1.getCharacterFromName("Pam") is not NPC pam)
                    {
                        Globals.ModMonitor.Log($"Pam cannot be found, ending phone call.", LogLevel.Warn);
                    }
                    else if (Game1.timeOfDay > 2200)
                    {
                        Game1.DrawDialogue(pam, "Strings\\Characters:Pam_Bus_Late");
                    }
                    else if (Game1.timeOfDay < 900)
                    {
                        AnswerPamCall(pam);
                    }
                    else
                    {
                        PlayPamVoicemail(pam);
                    }
                }
                catch (Exception ex)
                {
                    Globals.ModMonitor.LogError("handling Pam's phone call", ex);
                }
            },
            delay: 4950);
        return true;
    }

    /// <summary>
    /// Pam picks up the phone herself (early morning).
    /// </summary>
    /// <param name="pam">Pam.</param>
    private static void AnswerPamCall(NPC pam)
    {
        if (Game1.IsVisitingIslandToday(pam.Name))
        {
            Game1.DrawDialogue(pam, $"Strings\\Characters:Pam_Island_{Random.Shared.Next(1, 4)}");
        }
        else if (Utility.IsHospitalVisitDay(pam.Name))
        {
            Game1.DrawDialogue(pam, "Strings\\Characters:Pam_Doctor");
        }
        else if (MultiplayerSharedState.PamsSchedule is null)
        {
            Globals.ModMonitor.Log("Something very odd has happened. Pam's dayScheduleName is null", LogLevel.Debug);
            Game1.DrawDialogue(pam, "Strings\\Characters:Pam_Other");
        }
        else if (MultiplayerSharedState.PamsSchedule.Contains("BusStop 21 10"))
        {
            Game1.DrawDialogue(pam, $"Strings\\Characters:Pam_Bus_{Random.Shared.Next(1, 4)}");
        }
        else
        {
            Game1.DrawDialogue(pam, "Strings\\Characters:Pam_Other");
        }
    }

    /// <summary>
    /// Plays Pam's answering machine message.
    /// </summary>
    /// <param name="pam">Pam.</param>
    private static void PlayPamVoicemail(NPC pam)
    {
        string key;
        if (Game1.IsVisitingIslandToday(pam.Name))
        {
            key = "Island";
        }
        else if (Utility.IsHospitalVisitDay(pam.Name))
        {
            key = "Doctor";
        }
        else if (MultiplayerSharedState.PamsSchedule is null)
        {
            Globals.ModMonitor.Log("Something very odd has happened. Pam's dayScheduleName is not found?", LogLevel.Debug);
            key = "Other";
        }
        else if (MultiplayerSharedState.PamsSchedule.Contains("BusStop 21 10"))
        {
            key = "Bus";
        }
        else
        {
            key = "Other";
        }
        Game1.DrawDialogue(new Dialogue(pam, $"Strings\\Characters:Pam_Voicemail_{key}")
        {
            overridePortrait = Game1.temporaryContent.Load<Texture2D>("Portraits\\AnsweringMachine"),
        });
    }
}
