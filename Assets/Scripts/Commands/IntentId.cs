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
        Help
    }
}
