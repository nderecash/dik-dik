using System.Collections;
using Dikdik.Commands;
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

        [SerializeField] private AudioSource stationSource;

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
            var clip = Resources.Load<AudioClip>($"Station/{stationClip}");
            var comms = Bootstrap.Instance != null ? Bootstrap.Instance.Comms : null;
            var length = clip != null ? clip.length : 2.5f;

            // Station reads the jargon. Stop listening so the mic does not transcribe it.
            SupervisorVoice.BlockListeningFor(length + 0.6f);

            if (stationSource != null && clip != null)
                stationSource.PlayOneShot(clip);

            if (comms != null)
                comms.ShowStationLine(jargon);

            var until = Time.realtimeSinceStartup + length + 0.6f;
            while (Time.realtimeSinceStartup < until)
                yield return null;

            // The supervisor translates. This plays the next recorded "plain" line in
            // order and blocks listening for its own length.
            var supervisor = FindAnyObjectByType<SupervisorVoice>();
            if (supervisor != null)
                supervisor.PlayOne("plain");
        }
    }
}
