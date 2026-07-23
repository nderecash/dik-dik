namespace Dikdik.Commands
{
    /// <summary>
    /// Where a command came from.
    ///
    /// This is recorded for player feedback and for the spike log, and for nothing else.
    /// No gameplay code may branch on this value to decide whether a command counts.
    /// If you ever find yourself writing "if (source == CommandSource.Voice)" in a rule,
    /// you have just made one way of playing the real one and the other a courtesy.
    /// </summary>
    public enum CommandSource
    {
        Keyboard,
        Voice,
        Script
    }
}
