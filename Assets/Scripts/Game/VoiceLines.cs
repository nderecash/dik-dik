using System.Collections.Generic;

namespace Dikdik.Game
{
    /// <summary>
    /// The caption for every recorded line, keyed by clip name.
    ///
    /// <para>The audio is radio-filtered and deliberately hard to make out, so the text
    /// carries the meaning while the audio carries the mood. Subtitles are not a courtesy
    /// here, they are half the delivery.</para>
    ///
    /// <para>Copied verbatim from <c>docs/voice-script-v2.md</c>. If a line is re-recorded
    /// with different words, change it here too or the caption and the audio will
    /// disagree — which is worse than having no caption at all, because the player trusts
    /// it.</para>
    /// </summary>
    public static class VoiceLines
    {
        public static readonly IReadOnlyDictionary<string, string> Captions =
            new Dictionary<string, string>
            {
                // ---------------------------------------------------------------
                // Opening. Plays once over the cinematic. Nothing is listening yet.
                // ---------------------------------------------------------------
                ["sup_open_01"] = "Oh — there you are. Line's up.",
                ["sup_open_02"] = "Give me a second, I've been at that a while.",
                ["sup_open_03"] = "Right. We've run into a problem, and you're the only one who can help with it.",
                ["sup_open_04"] = "We're on the surface. The shuttle's in cooldown and we can't lift until it charges.",
                ["sup_open_05"] = "It isn't charging. Something's gone wrong with the relay line between us and the ground station.",
                ["sup_open_06"] = "We've got a rover out there. Our automated control went down with everything else.",
                ["sup_open_07"] = "Your uplink is the only one still live. So it has to be you.",
                ["sup_open_08"] = "Take it along the relay line and scan each section until we find the break.",
                ["sup_open_09"] = "Console's open — just talk to it. Plain speech is fine. Go left, stop, whatever comes out. It's not fussy. Try something.",

                // ---------------------------------------------------------------
                // Scan reports. The most repeated lines in the game, roughly twenty
                // times across a playthrough. Kept short on purpose.
                // ---------------------------------------------------------------
                ["sup_scan_01"] = "Section reads clean. Move it on.",
                ["sup_scan_02"] = "Nothing wrong there. Next one.",
                ["sup_scan_03"] = "That length's fine. Keep going.",
                ["sup_scan_04"] = "Clean. Logging it.",
                ["sup_scan_05"] = "No fault there either.",

                // ---------------------------------------------------------------
                // Finding the fault. Last checkpoint of the last level.
                // ---------------------------------------------------------------
                ["sup_fault_01"] = "Hold on. That's not clean.",
                ["sup_fault_02"] = "That's it. That's our break, right there.",
                ["sup_fault_03"] = "Get me a closer look at it.",

                // ---------------------------------------------------------------
                // The repair, and the end.
                // ---------------------------------------------------------------
                ["sup_fix_01"] = "Go on then. Patch it.",
                ["sup_fix_02"] = "Come on. Come on.",
                ["sup_fix_03"] = "That's power. We've got power.",
                ["sup_fix_04"] = "You did it. We're going home.",

                // ---------------------------------------------------------------
                // Safety cutout. The rover backed itself away from something.
                // Nothing here may imply the player did badly.
                // ---------------------------------------------------------------
                ["sup_cut_01"] = "Safety cutout. It's backing off.",
                ["sup_cut_02"] = "Caught the edge there. Pulling it back.",
                ["sup_cut_03"] = "Cutout again. It's fine, it does that.",
                ["sup_cut_04"] = "Backed up and holding. New heading when you're ready.",

                // ---------------------------------------------------------------
                // Stuck, escalating over three attempts, then Control takes over.
                // ---------------------------------------------------------------
                ["sup_stuck_01"] = "I think it's stuck. It'll need a new bearing.",
                ["sup_stuck_02"] = "Still wedged. Are you giving it directions, or are we waiting for the automated override?",
                ["sup_stuck_03"] = "Right. It's recalculated to the nearest checkpoint. I'm taking it there myself.",
                ["sup_stuck_04"] = "It's back on the line. All yours.",

                // ---------------------------------------------------------------
                // Idle. Each one pushes the next further out. The last drops the line.
                // ---------------------------------------------------------------
                ["sup_idle_01"] = "Still there?",
                ["sup_idle_02"] = "No rush. You off making coffee?",
                ["sup_idle_03"] = "Take your time. It's not going anywhere. Neither are we, which is rather the problem.",
                ["sup_idle_04"] = "The day here is nineteen hours. Nobody's slept properly in weeks.",
                ["sup_idle_05"] = "I'm not rushing you. I'm just… aware of you not being there.",
                ["sup_idle_06"] = "Rover's fine, by the way. Bored, if it could be.",
                ["sup_idle_07"] = "If you've gone, that's alright. I'll be here. Obviously.",
                ["sup_idle_08"] = "I'm going to drop the line and save some power. Say anything when you're back and I'll pick up.",

                // ---------------------------------------------------------------
                // The obstacle puzzle. No wrong answers, and line three says so.
                // ---------------------------------------------------------------
                ["sup_puzzle_01"] = "It's scanning the blockage. Give it a second.",
                ["sup_puzzle_02"] = "Right — it reckons it can cut through it, dissolve it, or just shove it. Your call.",
                ["sup_puzzle_03"] = "Any of them work. It's just a question of how long you want to stand here.",
                ["sup_puzzle_04"] = "That's it through. Carry on.",

                // ---------------------------------------------------------------
                // Sector framing, one per level on arrival.
                // ---------------------------------------------------------------
                ["sup_sector_01"] = "This is the first stretch, out past the old survey post. Should be straightforward.",
                ["sup_sector_02"] = "Next section runs through the station yard. The automated system will be talking. Ignore most of it.",
                ["sup_sector_03"] = "This length is on the night side. You'll want the contrast setting — it's in your console, always has been.",
                ["sup_sector_04"] = "Careful here, we've had a fault on your console reported. Not your doing.",
                ["sup_sector_05"] = "This part runs downhill. It'll carry further than you expect when you stop it.",
                ["sup_sector_06"] = "Last stretch. If the break isn't here, I'm out of ideas.",

                // ---------------------------------------------------------------
                // Delight. Never required, never hinted at. The reward for playing
                // with a game that says it will listen to anything.
                // ---------------------------------------------------------------
                ["sup_fun_01"] = "…It doesn't jump. Six wheels, no legs. It appreciated the thought though.",
                ["sup_fun_02"] = "Now that it can actually do. After a fashion.",
                ["sup_fun_03"] = "Hello to you too. It can't hear tone, but I can.",
                ["sup_fun_04"] = "Please don't encourage it.",
                ["sup_fun_05"] = "Nobody important. Just the one who couldn't fix this on their own.",

                // ---------------------------------------------------------------
                // Kept from the first recording session. Narrative-neutral, so the
                // rework did not touch them.
                // ---------------------------------------------------------------
                ["sup_ack_01"] = "Copy.",
                ["sup_ack_02"] = "Salty has it.",
                ["sup_ack_03"] = "Good.",
                ["sup_ack_04"] = "That worked.",
                ["sup_ack_05"] = "It heard you.",

                ["sup_miss_01"] = "It did not catch that. Try it another way.",
                ["sup_miss_02"] = "Nothing came through. Say it however you like. It is not fussy.",
                ["sup_miss_03"] = "That did not land. Not your fault.",
                ["sup_miss_04"] = "Signal is fine. The understanding is the hard part.",
                ["sup_miss_05"] = "Again, when you are ready. No rush on this end.",

                ["sup_block_01"] = "It has stopped. Something ahead of it.",
                ["sup_block_02"] = "That is a wall. It will wait.",
                ["sup_block_03"] = "Held position. Give it another heading.",

                ["sup_done_01"] = "That is the one. Logging it.",
                ["sup_done_02"] = "Clean run. Uplinking.",
                ["sup_done_03"] = "Salty is through. Next sector.",

                ["sup_plain_01"] = "Open the door.",
                ["sup_plain_02"] = "It means turn left.",
                ["sup_plain_03"] = "Ignore that. Just keep going forward.",
                ["sup_plain_04"] = "I have no idea who writes these. Head right.",

                ["sup_console_01"] = "Fault on your side, not Salty's. One of your controls has stuck.",
                ["sup_console_02"] = "Remap it. Settings, controls. Takes a second.",
                ["sup_console_03"] = "If you are on voice you are fine. Or teach it a new word for it, same screen."
            };

        public static string Caption(string clipName)
        {
            return Captions.TryGetValue(clipName, out var text) ? text : string.Empty;
        }
    }
}
