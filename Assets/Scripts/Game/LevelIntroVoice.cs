using System.Collections;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Plays a level's own supervisor lines when it starts: the console fault on the
    /// jammed-key level, the station translations on the clear-language level, the rim
    /// speech at the end.
    ///
    /// Waits out the boot briefing first. If someone jumps straight to this level, they
    /// get the tutorial, and only once it finishes does the level's own intro play, so
    /// the two never talk over each other.
    /// </summary>
    public class LevelIntroVoice : MonoBehaviour
    {
        [Tooltip("Which recorded group to play: console, plain, final, ...")]
        [SerializeField] private string group = "console";

        private IEnumerator Start()
        {
            var supervisor = FindAnyObjectByType<SupervisorVoice>();
            if (supervisor == null)
                yield break;

            while (supervisor.IsBriefing)
                yield return null;

            // A short beat after any briefing, so it does not run straight into this.
            yield return new WaitForSecondsRealtime(0.5f);

            supervisor.PlaySequence(group);
        }
    }
}
