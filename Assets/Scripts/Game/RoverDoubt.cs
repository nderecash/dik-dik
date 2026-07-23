using System;
using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// After a long stretch with no instruction, Salty stops and asks whether it should
    /// keep going.
    ///
    /// <para><b>Why this is not a feature, but a correction.</b> The rover acts only when
    /// spoken to. A long unbroken drive is therefore the rover acting unbidden: it is
    /// making the decision to continue, over and over, with nobody asking it to. Checking
    /// in restores the premise rather than decorating it.</para>
    ///
    /// <para>Stopping at a junction works because a junction is a question. Distance is
    /// also a question, just a quieter one.</para>
    ///
    /// <para>What it buys, beyond consistency: it punctuates the long silences, it hands
    /// the player a beat in which to plan the next command instead of composing it while
    /// the rover rolls away from them, and it gives the machine an inner life without a
    /// line of dialogue. Three rising beeps and a light are enough to read as doubt.</para>
    ///
    /// <para>It can never cost anything. There is no failure state, no timer and no
    /// penalty for leaving it waiting. It will sit there until spoken to, which is the
    /// most in-character thing it could possibly do.</para>
    /// </summary>
    public class RoverDoubt : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private RoverController rover;
        [SerializeField] private RoverLight roverLight;
        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip queryClip;

        [Header("When to ask")]
        [Tooltip("Metres of unbroken travel before it starts to wonder")]
        [SerializeField] private float distanceBeforeDoubt = 20f;

        [Tooltip("Grace period after any command. Stops it querying the instruction " +
                 "it was just given, which would read as not listening.")]
        [SerializeField] private float quietAfterCommand = 3f;

        [Header("Voice")]
        [Tooltip("Cycled in order. Random repeats itself in a way players read as " +
                 "the game not paying attention.")]
        [SerializeField]
        private string[] questions =
        {
            "Still going. Do you want me to keep going?",
            "Long way out here. Continue?",
            "No instruction for a while. Keep going?",
            "Holding. Say the word."
        };

        /// <summary>Raised with the question, for subtitles. Never sound alone.</summary>
        public event Action<string> Asked;

        private float _distanceSinceCommand;
        private float _quietUntil;
        private Vector3 _lastPosition;
        private int _questionIndex;
        private bool _asking;

        private void Awake()
        {
            _lastPosition = transform.position;
        }

        private void Start()
        {
            // Wired at runtime rather than in the inspector: the console lives in the
            // persistent Boot scene and the rover lives in a level scene, and Unity
            // cannot serialize a reference across that boundary.
            var comms = Bootstrap.Instance != null ? Bootstrap.Instance.Comms : null;
            if (comms != null)
                Asked += comms.ShowRoverQuestion;
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

        private void OnCommand(Intent intent)
        {
            // Any instruction at all resets the wondering, including one that stops it.
            // Being told to hold is still being spoken to.
            _distanceSinceCommand = 0f;
            _quietUntil = Time.time + quietAfterCommand;
            _asking = false;
        }

        private void Update()
        {
            var moved = (transform.position - _lastPosition).magnitude;
            _lastPosition = transform.position;

            if (rover == null || !rover.IsMoving || _asking)
                return;

            if (Time.time < _quietUntil)
                return;

            _distanceSinceCommand += moved;

            if (_distanceSinceCommand < distanceBeforeDoubt)
                return;

            Ask();
        }

        private void Ask()
        {
            _asking = true;
            _distanceSinceCommand = 0f;

            // Stop first. The question is only honest if it is actually waiting for
            // the answer rather than asking over its shoulder while driving off.
            if (rover != null)
                rover.HoldAtJunction();

            if (source != null && queryClip != null)
                source.PlayOneShot(queryClip);

            // Sound, light and text. The query is essential information, so it does not
            // get to live in one channel. Engine noise is ambience and may; this may not.
            if (roverLight != null)
                roverLight.SignalJunction();

            Asked?.Invoke(NextQuestion());
        }

        private string NextQuestion()
        {
            if (questions == null || questions.Length == 0)
                return "Keep going?";

            var question = questions[_questionIndex % questions.Length];
            _questionIndex++;
            return question;
        }

        /// <summary>Put it back for a fresh rehearsal run.</summary>
        public void ResetDoubt()
        {
            _distanceSinceCommand = 0f;
            _asking = false;
            _lastPosition = transform.position;
        }
    }
}
