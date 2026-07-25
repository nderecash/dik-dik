using System;

namespace Dikdik.Game
{
    /// <summary>
    /// Whether the game is paused, and one event to say so.
    ///
    /// <para><b>Deliberately not Time.timeScale.</b> This project scales its own delta by
    /// <see cref="GameSettings.GameSpeed"/> while keeping the command transport delay in
    /// real seconds. That split is what makes the slope level work: slowing the game
    /// shrinks how far the rover travels during a fixed 2.6 second delay. Setting
    /// timeScale to zero would flatten the two back together and quietly break it.</para>
    ///
    /// <para>So pause is cooperative. Everything that moves, counts down or makes noise
    /// checks this and stops itself. The list of consumers is short and each one is
    /// deliberate: the rover, the command bus transport clock, the voice arbiter, and the
    /// microphone.</para>
    ///
    /// <para>The command bus one matters more than it looks. Its transport delay is
    /// measured against a clock, so a pause that did not freeze that clock would let
    /// every queued command land at once the instant you unpaused.</para>
    /// </summary>
    public static class GamePause
    {
        private static bool _paused;

        /// <summary>Raised whenever the paused state changes.</summary>
        public static event Action<bool> Changed;

        public static bool IsPaused
        {
            get => _paused;
            set
            {
                if (_paused == value)
                    return;

                _paused = value;
                Changed?.Invoke(value);
            }
        }

        /// <summary>
        /// Reset on a fresh session. Statics survive scene loads, and a pause left set by
        /// a previous run would soft-lock the next one.
        /// </summary>
        public static void Reset()
        {
            _paused = false;
        }
    }
}
