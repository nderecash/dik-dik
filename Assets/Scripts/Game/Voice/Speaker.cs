namespace Dikdik.Game.Voice
{
    /// <summary>
    /// Who is talking. Chooses the badge and tint on the console, so the player can always
    /// see which of them is speaking as well as hear it.
    /// </summary>
    public enum Speaker
    {
        /// <summary>The human on the loop. The recorded voice.</summary>
        Control,

        /// <summary>The station's automated system, reading procedures in jargon. Synthetic.</summary>
        Station,

        /// <summary>The rover itself, asking something. Never words, but it gets a caption.</summary>
        Salty
    }
}
