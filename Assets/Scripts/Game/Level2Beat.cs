using System.Collections;
using Dikdik.Commands;
using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// One instruction beat on the clear-language level.
    ///
    /// The rover reaches the trigger, the station reads the procedure in dense jargon,
    /// and then the human supervisor translates it into a plain instruction the player
    /// can actually act on. Hearing the two back to back is the level: the machine talks
    /// like a manual, the person talks like a person, and only one of them is usable.
    ///
    /// The station line is synthetic (Windows text-to-speech, radio filtered). The plain
    /// line is one of the supervisor's recorded "plain" lines, played in order.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Level2Beat : MonoBehaviour
    {
        [Tooltip("Resources/Station clip name, e.g. station_01")]
        [SerializeField] private string stationClip = "station_01";

        [Tooltip("The jargon, shown while the station speaks")]
        [TextArea]
        [SerializeField] private string jargon = "";

        private bool _fired;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired || other.GetComponentInParent<RoverController>() == null)
                return;

            _fired = true;
            StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            var arbiter = VoiceArbiter.Instance;
            if (arbiter == null)
                yield break;

            var clip = Resources.Load<AudioClip>($"Station/{stationClip}");
            if (clip == null)
                yield break;

            // Station reads the procedure, then Control translates it. Both go into one
            // queue, so the ordering stops being enforced by a hand-tuned timer and
            // becomes structural: two entries, in order, and nothing can wedge between
            // them. Both are Essential because the translation is the only usable version
            // of the instruction and the player would be stuck without it.
            yield return arbiter.Say(SpeechLine.Make(
                clip, jargon, Speaker.Station, SpeechPriority.Beat, essential: true));

            yield return arbiter.SayGroup("plain", SpeechPriority.Beat,
                                          Speaker.Control, essential: true);
        }
    }
}
