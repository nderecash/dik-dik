using System.Collections.Generic;

namespace Dikdik.Game
{
    /// <summary>
    /// The caption for every recorded supervisor line, keyed by clip name.
    ///
    /// The audio is radio-filtered and hard to make out on purpose, so every line needs
    /// its exact words on screen. "Provide subtitles for all important speech" is not
    /// optional here, it is the point: the one human voice in the game is deliberately
    /// degraded, so the text carries the meaning and the audio carries the feeling.
    ///
    /// These are copied verbatim from the recording script. If a line is re-recorded with
    /// different words, change it here too or the caption and the audio will disagree.
    /// </summary>
    public static class VoiceLines
    {
        public static readonly IReadOnlyDictionary<string, string> Captions =
            new Dictionary<string, string>
            {
                // Boot, played once at first launch. The whole tutorial.
                ["sup_boot_01"] = "Console is yours. Salty is on the surface and waiting.",
                ["sup_boot_02"] = "It will not move on its own. It moves when you tell it to.",
                ["sup_boot_03"] = "Talk to it, or type to it. Same loop either way. It cannot tell the difference.",
                ["sup_boot_04"] = "Round trip is about two and a half seconds. That is the Moon, not you.",
                ["sup_boot_05"] = "Everything you need is in settings. It is all switched on already. Change what helps.",

                // Acknowledgements, used sparingly.
                ["sup_ack_01"] = "Copy.",
                ["sup_ack_02"] = "Salty has it.",
                ["sup_ack_03"] = "Good.",
                ["sup_ack_04"] = "That worked.",
                ["sup_ack_05"] = "It heard you.",

                // Not understood. Never blame the player.
                ["sup_miss_01"] = "It did not catch that. Try it another way.",
                ["sup_miss_02"] = "Nothing came through. Say it however you like. It is not fussy.",
                ["sup_miss_03"] = "That did not land. Not your fault.",
                ["sup_miss_04"] = "Signal is fine. The understanding is the hard part.",
                ["sup_miss_05"] = "Again, when you are ready. No rush on this end.",

                // Something in the way.
                ["sup_block_01"] = "It has stopped. Something ahead of it.",
                ["sup_block_02"] = "That is a wall. It will wait.",
                ["sup_block_03"] = "Held position. Give it another heading.",

                // Run it again. A rehearsal discarded, never a failure.
                ["sup_reset_01"] = "Sim aborted. Resetting.",
                ["sup_reset_02"] = "That is not how that goes. Take two.",
                ["sup_reset_03"] = "Run it back.",
                ["sup_reset_04"] = "Noted. Again from the top.",
                ["sup_reset_05"] = "Good. Now we know. Again.",
                ["sup_reset_06"] = "This is why we rehearse.",

                // Level finished.
                ["sup_done_01"] = "That is the one. Logging it.",
                ["sup_done_02"] = "Clean run. Uplinking.",
                ["sup_done_03"] = "Salty is through. Next sector.",

                // Long silence.
                ["sup_idle_01"] = "Still here.",
                ["sup_idle_02"] = "Salty is holding. It does not mind waiting.",
                ["sup_idle_03"] = "Take your time. It has nowhere to be.",

                // Level 2, translating the station.
                ["sup_plain_01"] = "Open the door.",
                ["sup_plain_02"] = "It means turn left.",
                ["sup_plain_03"] = "Ignore that. Just keep going forward.",
                ["sup_plain_04"] = "I have no idea who writes these. Head right.",

                // Level 4, console fault.
                ["sup_console_01"] = "Fault on your side, not Salty's. One of your controls has stuck.",
                ["sup_console_02"] = "Remap it. Settings, controls. Takes a second.",
                ["sup_console_03"] = "If you are on voice you are fine. Or teach it a new word for it, same screen.",

                // The rim.
                ["sup_final_01"] = "There are others out there. Dormant. Same build, same problem.",
                ["sup_final_02"] = "You are on the open loop now. Everything hears you.",
                ["sup_final_03"] = "Say it once.",
                ["sup_final_04"] = "All of them. One transmission."
            };

        public static string Caption(string clipName)
        {
            return Captions.TryGetValue(clipName, out var text) ? text : string.Empty;
        }
    }
}
