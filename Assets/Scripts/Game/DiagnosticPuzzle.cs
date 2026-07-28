using System.Collections;
using Dikdik.Commands;
using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// A blockage on the line, and a conversation about what to do with it.
    ///
    /// <para>The rover scans it, reports what it found, and offers two or three ways
    /// through. The player picks one. It works. Then they carry on.</para>
    ///
    /// <para><b>There are no wrong answers, and one of the spoken lines says so out loud:</b>
    /// "Any of them work. It's just a question of how long you want to stand here." That
    /// line is not softening a puzzle, it is describing one accurately. What differs
    /// between the options is a few seconds, and that is the entire stake.</para>
    ///
    /// <para>Which sounds like it makes the puzzle pointless, and would, if the puzzle were
    /// about finding an answer. It is not. It is about being asked. Six sectors of this
    /// game consist of the player instructing a machine that never once has an opinion;
    /// here the machine has looked at something, formed a view, and wants to know what you
    /// think. Being consulted is the content.</para>
    ///
    /// <para>It also quietly answers the thing this game keeps arguing about. A voice
    /// interface that only accepts commands is a worse interface than one that can hold a
    /// two-turn exchange, and this is the only place the project demonstrates the second
    /// kind rather than asserting it.</para>
    ///
    /// <para>The options are deliberately plain words. Cut, dissolve, push. A puzzle whose
    /// difficulty is vocabulary would fight everything Level 2 spends its whole runtime
    /// establishing.</para>
    /// </summary>
    public class DiagnosticPuzzle : MonoBehaviour, IResettable
    {
        private enum Phase { Waiting, Scanning, Offering, Working, Done }

        [Header("Wiring")]
        [SerializeField] private RoverController rover;
        [SerializeField] private RoverLight roverLight;
        [SerializeField] private Transform blockage;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip scanTone;

        [Header("Timing")]
        [Tooltip("Seconds each option takes to carry out. The only difference between " +
                 "them, and small enough that nobody is punished for a preference.")]
        [SerializeField] private float cutSeconds = 3.5f;

        [SerializeField] private float dissolveSeconds = 5f;
        [SerializeField] private float pushSeconds = 2f;

        [SerializeField] private float clearSeconds = 1.2f;

        private Phase _phase = Phase.Waiting;
        private Vector3 _blockageStart;

        private void Start()
        {
            if (blockage != null)
                _blockageStart = blockage.localPosition;
        }

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandIssued += OnCommand;
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandIssued -= OnCommand;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_phase != Phase.Waiting || rover == null)
                return;

            if (other.attachedRigidbody == null ||
                other.attachedRigidbody.gameObject != rover.gameObject)
                return;

            StartCoroutine(Scan());
        }

        private IEnumerator Scan()
        {
            _phase = Phase.Scanning;

            // Stop first, same as a checkpoint. Nobody has to land on a mark.
            rover.BeginScanHold();

            if (roverLight != null)
                roverLight.SignalScanning();

            if (source != null && scanTone != null)
                source.PlayOneShot(scanTone, GameSettings.EffectsVolume);

            var arbiter = VoiceArbiter.Instance;
            if (arbiter == null)
            {
                Release();
                yield break;
            }

            // sup_puzzle_01: "It's scanning the blockage. Give it a second."
            yield return arbiter.SayGroup("puzzle", SpeechPriority.Beat, Speaker.Control,
                                          essential: true);

            // sup_puzzle_02: the three options.
            yield return arbiter.SayGroup("puzzle", SpeechPriority.Beat, Speaker.Control,
                                          essential: true);

            // sup_puzzle_03: "Any of them work."
            yield return arbiter.SayGroup("puzzle", SpeechPriority.Beat, Speaker.Control,
                                          essential: true);

            _phase = Phase.Offering;

            // On screen as well as spoken, because these are three specific words and
            // asking someone to remember them from degraded radio audio would be a
            // memory test wearing a puzzle's clothes.
            if (Bootstrap.Instance != null && Bootstrap.Instance.Comms != null)
                Bootstrap.Instance.Comms.ShowPrompt(
                    "Cut it, dissolve it, or push it.",
                    "Say any one of them. They all work.");
        }

        private void OnCommand(Intent intent)
        {
            if (_phase != Phase.Offering)
                return;

            // Named answers first, then a catch-all.
            //
            // The catch-all is not laziness, it is the promise. This prompt says out loud
            // that any of them work, and an earlier version could not keep that: it read
            // the three answers out of the raw transcript, so the words it named were
            // absent from the vocabulary, resolved to nothing, and never arrived. On the
            // keyboard it was worse. A key press carries "W" or "Space" as its text, which
            // matched no substring, so a keyboard player was stopped in front of a rock
            // with the rover scan-held and no input that could release it.
            //
            // So: anything the game understood at all gets the rover through. If the
            // player has found a word this machine recognises, they have said enough.
            switch (intent.Id)
            {
                case IntentId.Cut:
                    StartCoroutine(Work(cutSeconds));
                    break;

                case IntentId.Dissolve:
                    StartCoroutine(Work(dissolveSeconds));
                    break;

                default:
                    StartCoroutine(Work(pushSeconds));
                    break;
            }
        }

        private IEnumerator Work(float seconds)
        {
            _phase = Phase.Working;

            if (Bootstrap.Instance != null && Bootstrap.Instance.Comms != null)
                Bootstrap.Instance.Comms.ClearPrompt();

            var elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!GamePause.IsPaused)
                    elapsed += Time.unscaledDeltaTime;

                yield return null;
            }

            // The blockage goes down through the floor rather than fading. This project
            // has no transparency in its material vocabulary and a solid that sinks reads
            // cleanly at the camera's angle, which is the same reasoning as the doors.
            if (blockage != null)
            {
                var from = blockage.localPosition;
                var to = from + Vector3.down * 4f;

                var t = 0f;
                while (t < clearSeconds)
                {
                    if (!GamePause.IsPaused)
                        t += Time.unscaledDeltaTime;

                    blockage.localPosition = Vector3.Lerp(from, to, Mathf.Clamp01(t / clearSeconds));
                    yield return null;
                }

                blockage.gameObject.SetActive(false);
            }

            var arbiter = VoiceArbiter.Instance;
            if (arbiter != null)
                // sup_puzzle_04: "That's it through. Carry on."
                yield return arbiter.SayGroup("puzzle", SpeechPriority.Beat, Speaker.Control,
                                              essential: true);

            Release();
        }

        private void Release()
        {
            _phase = Phase.Done;

            if (roverLight != null)
                roverLight.Release();

            if (rover != null)
                rover.EndScanHold();

            if (Bootstrap.Instance != null && Bootstrap.Instance.Comms != null)
                Bootstrap.Instance.Comms.ClearPrompt();
        }

        /// <summary>
        /// Put the blockage back for a fresh rehearsal run.
        ///
        /// <para>This was missing entirely, which is the same bug the checkpoints had: a
        /// reset during the conversation stopped the coroutine partway, leaving the lamp
        /// orange and a prompt on screen asking a question nothing was listening for any
        /// more.</para>
        ///
        /// <para>The rock comes back too. A reset means the run happens again from the top,
        /// and a blockage that stayed dissolved would quietly hand the player a shortcut
        /// for having triggered a safety cutout.</para>
        /// </summary>
        public void ResetForSimulation()
        {
            StopAllCoroutines();

            _phase = Phase.Waiting;

            if (roverLight != null)
                roverLight.Release();

            if (Bootstrap.Instance != null && Bootstrap.Instance.Comms != null)
                Bootstrap.Instance.Comms.ClearPrompt();

            if (blockage != null)
            {
                blockage.gameObject.SetActive(true);
                blockage.localPosition = _blockageStart;
            }
        }
    }
}
