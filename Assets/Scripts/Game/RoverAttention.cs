using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Puts the rover on alert the moment anyone starts giving it an instruction, from
    /// either input, and takes it off alert when the instruction lands.
    ///
    /// <para>This is the whole of the "stop should be instant" problem, solved without
    /// privileging voice or undoing the transport delay. See
    /// <see cref="RoverController.SetAttentive"/> for the argument. This class is only the
    /// wiring: it knows when someone has keyed up and when they are done.</para>
    ///
    /// <para><b>Voice</b> triggers on the microphone's voice detection, which whisper.unity
    /// already runs on a tenth-of-a-second tick off the one microphone it owns. Nothing new
    /// opens the audio device.</para>
    ///
    /// <para>The original plan was a second Windows recogniser listening for four hotwords
    /// in parallel. That was dropped on evidence: Unity's documentation still restricts
    /// KeywordRecognizer to Windows 10, Microsoft deprecated the speech platform underneath
    /// it in 2023, it throws rather than degrades on machines that lack it, and no source
    /// anywhere confirms it can share a microphone with an already-open capture. The
    /// detector already in the project gives the same hundred milliseconds with none of
    /// that.</para>
    ///
    /// <para><b>Keyboard</b> triggers on the key going down, and holds for a fixed moment.
    /// Without this the keyboard would be the only input the rover does not react to, and
    /// the parity this game is built on would quietly become a lie.</para>
    /// </summary>
    public class RoverAttention : MonoBehaviour
    {
        [SerializeField] private RoverController rover;

        [Tooltip("How long a key press holds the rover's attention. Roughly how long a " +
                 "short spoken command takes, so both inputs ease off for a similar beat.")]
        [SerializeField] private float keyboardHoldSeconds = 0.9f;

        [Tooltip("Safety net. However long somebody talks, attention lapses after this, " +
                 "so a microphone stuck open cannot pin the rover at half speed forever.")]
        [SerializeField] private float maximumSeconds = 6f;

        /// <summary>
        /// The one in the current level, if any.
        ///
        /// <para>Static because the two producers live in the Boot scene and survive every
        /// scene load, while this lives on the rover and does not. A serialized reference
        /// across that boundary is a reference that is null in five levels out of six.</para>
        /// </summary>
        public static RoverAttention Instance { get; private set; }

        private bool _voiceActive;
        private float _keyboardUntil;
        private float _voiceStartedAt;

        /// <summary>Called by the keyboard producer. Safe when no rover exists.</summary>
        public static void NoteKeyPressStatic()
        {
            if (Instance != null)
                Instance.NoteKeyPress();
        }

        /// <summary>Called by the voice producer. Safe when no rover exists.</summary>
        public static void SetVoiceDetectedStatic(bool speaking)
        {
            if (Instance != null)
                Instance.SetVoiceDetected(speaking);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandAccepted += OnAccepted;
                CommandBus.Instance.CommandIssued += OnResolved;
                CommandBus.Instance.CommandNotUnderstood += OnResolved;
            }
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandAccepted -= OnAccepted;
                CommandBus.Instance.CommandIssued -= OnResolved;
                CommandBus.Instance.CommandNotUnderstood -= OnResolved;
            }

            if (rover != null)
                rover.SetAttentive(false);
        }

        /// <summary>Called by the voice producer when speech starts and stops.</summary>
        public void SetVoiceDetected(bool speaking)
        {
            if (speaking && !_voiceActive)
                _voiceStartedAt = Time.unscaledTime;

            _voiceActive = speaking;
        }

        /// <summary>Called by the keyboard producer when a bound key goes down.</summary>
        public void NoteKeyPress()
        {
            _keyboardUntil = Time.unscaledTime + keyboardHoldSeconds;
        }

        private void OnAccepted(Intent intent)
        {
            // We have the words. Whatever they were, the sentence is over, so stop easing
            // off and let the command itself decide what the rover does.
            _voiceActive = false;
            _keyboardUntil = 0f;
        }

        private void OnResolved(Intent intent)
        {
            _voiceActive = false;
            _keyboardUntil = 0f;
        }

        private void Update()
        {
            if (rover == null)
                return;

            if (GamePause.IsPaused)
            {
                rover.SetAttentive(false);
                return;
            }

            var voice = _voiceActive &&
                        Time.unscaledTime - _voiceStartedAt < maximumSeconds;

            rover.SetAttentive(voice || Time.unscaledTime < _keyboardUntil);
        }
    }
}
