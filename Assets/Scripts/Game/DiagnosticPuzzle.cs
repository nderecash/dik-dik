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
    public class DiagnosticPuzzle : MonoBehaviour
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

            // The three options are read out of the raw text rather than given intents of
            // their own. They exist for one beat in one place, and three IntentIds that
            // mean nothing anywhere else in the game would be three more things every
            // level has to know to allow.
            var said = (intent.RawText ?? string.Empty).ToLowerInvariant();

            if (said.Contains("cut") || said.Contains("saw") || said.Contains("slice"))
                StartCoroutine(Work(cutSeconds));
            else if (said.Contains("dissolve") || said.Contains("melt") || said.Contains("acid"))
                StartCoroutine(Work(dissolveSeconds));
            else if (said.Contains("push") || said.Contains("shove") || said.Contains("ram") ||
                     said.Contains("move") || said.Contains("go") || said.Contains("forward"))
                StartCoroutine(Work(pushSeconds));

            // Anything else falls through and the prompt stays up. No scolding, no timer,
            // and the question is still on screen when they are ready.
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
    }
}
