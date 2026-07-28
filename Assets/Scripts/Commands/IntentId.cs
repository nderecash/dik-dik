namespace Dikdik.Commands
{
    /// <summary>
    /// Every action the rover can be asked to take.
    /// Voice and keyboard both resolve to one of these. Neither is privileged.
    /// </summary>
    public enum IntentId
    {
        None = 0,

        // Movement
        Go,
        Stop,
        Left,
        Right,
        Back,

        // World
        Open,
        Light,

        // Meta
        Wake,
        Repeat,     // say your last message again
        Restart,    // run the simulation again from the top
        Help,

        // Delight.
        //
        // Recognised, never required, never hinted at, and never part of any objective.
        // They exist because a game that tells you it will listen to anything invites you
        // to test that, and the worst possible answer to someone trying is silence. Every
        // one of these gets a real reply from Control.
        //
        // Only Spin does anything physical. The rest are answered in words, including
        // Jump, which the rover cannot do and says so.
        Jump,
        Spin,
        Dance,
        Greet,
        Who,

        // The three answers to the blockage. Real intents rather than substrings matched
        // against raw text, which is what they were, and which meant none of them worked:
        // they were absent from the vocabulary so they resolved to None, and a keyboard
        // press arrives as "W" or "Space" and matched nothing at all.
        Cut,
        Dissolve,
        Push,

        // The last command in the game, and it did not exist.
        //
        // The ending asks you to patch the break in the line and then waits. There was no
        // patch, fix, repair or mend anywhere in the vocabulary, so every word a player
        // could reasonably try resolved to None, was reported as not understood, and never
        // reached RepairFinale. The game could not be finished by anybody.
        Repair
    }
}
