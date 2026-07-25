using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Plays a level's own supervisor lines when it starts: what this stretch of the relay
    /// line is, the console fault, the station's jargon.
    ///
    /// <para>This used to poll a flag waiting for the opening briefing to finish, with a
    /// hand-tuned half-second beat afterwards, and it still managed to talk over the
    /// briefing whenever the player skipped it. Now it asks the arbiter to say the group
    /// and stops. Both are Critical, so they queue, and "wait for the briefing" stops
    /// being a special case anybody has to implement and becomes what a queue does.</para>
    /// </summary>
    public class LevelIntroVoice : MonoBehaviour
    {
        [Tooltip("Which recorded group to play: sector, console, plain...")]
        [SerializeField] private string group = "sector";

        private void Start()
        {
            VoiceArbiter.Instance?.SaySequence(group, SpeechPriority.Critical);
        }
    }
}
