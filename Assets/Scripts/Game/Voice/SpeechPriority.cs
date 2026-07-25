namespace Dikdik.Game.Voice
{
    /// <summary>
    /// How much a spoken line matters. Higher preempts lower; equal queues in order;
    /// lower is dropped rather than made to wait.
    ///
    /// <para>Dropping a spoken acknowledgement is safe, and it is worth saying why once
    /// here rather than arguing it at every call site. The console already prints what it
    /// heard and the rover already pulses its light, both immediately. The supervisor's
    /// voice is a third channel on information that is already dual-coded, so losing it
    /// costs colour and never costs meaning. That is exactly what makes a priority system
    /// compatible with "no essential information by sound alone".</para>
    ///
    /// <para>Anything that would genuinely lose meaning if dropped is marked
    /// <see cref="SpeechLine.Essential"/> instead, which delays it rather than discarding
    /// it.</para>
    /// </summary>
    public enum SpeechPriority
    {
        /// <summary>Filling silence. First thing to go.</summary>
        Idle = 0,

        /// <summary>Reacting to the player: acknowledgements, misheard, blocked.</summary>
        Reactive = 1,

        /// <summary>Level content: scan reports, station jargon, the rover's questions.</summary>
        Beat = 2,

        /// <summary>Briefings, the fault, the repair. Never interrupted by anything.</summary>
        Critical = 3
    }
}
