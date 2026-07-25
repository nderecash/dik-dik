using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Plays a level's own lines when it starts: which stretch of the relay line this is,
    /// the console fault, whatever belongs to this level and nowhere else.
    ///
    /// <para>This used to poll a flag waiting for the opening briefing to finish, with a
    /// hand-tuned half-second beat afterwards, and it still managed to talk over the
    /// briefing whenever the player skipped it. Now it asks the arbiter and stops. Both
    /// are Critical, so they queue, and "wait for the briefing" stops being a special case
    /// anybody has to implement and becomes what a queue does.</para>
    /// </summary>
    public class LevelIntroVoice : MonoBehaviour
    {
        [Tooltip("One specific clip, e.g. sup_sector_03: which stretch of the relay line " +
                 "this is. Plays first.")]
        [SerializeField] private string clipName = "";

        [Tooltip("Optional. Every line of this group in order, after the sector line. " +
                 "Used where a level has a short scripted exchange of its own.")]
        [SerializeField] private string group = "";

        private void Start()
        {
            var arbiter = VoiceArbiter.Instance;
            if (arbiter == null)
                return;

            // Both Critical, so they queue in the order asked for. Nothing here has to
            // wait on anything: "play the sector line, then the level's own lines, and
            // put both after the briefing" is three sentences that the queue already
            // means. This is why the class is nine lines instead of forty.
            if (!string.IsNullOrWhiteSpace(clipName))
                arbiter.SayClip(clipName, SpeechPriority.Critical, essential: true);

            if (!string.IsNullOrWhiteSpace(group))
                arbiter.SaySequence(group, SpeechPriority.Critical);
        }
    }
}
