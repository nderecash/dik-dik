using System;
using System.Collections;
using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// A door that opens when someone says so, and only when the rover is close enough
    /// to be plausibly talking to it.
    ///
    /// The proximity check is not a puzzle mechanic, it is disambiguation: with several
    /// doors in a level, "open" has to mean the one in front of you. The rule is
    /// deliberately generous, because a player who says the right word and gets nothing
    /// learns to distrust the interface, and that is a worse failure than an early open.
    /// </summary>
    public class InteractableDoor : MonoBehaviour, IResettable
    {
        [Header("Reach")]
        [Tooltip("How close the rover must be for 'open' to mean this door")]
        [SerializeField] private float reach = 4f;

        [SerializeField] private Transform rover;

        [Header("Motion")]
        [Tooltip("How far the door slides, in local units")]
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 3f, 0f);

        [SerializeField] private float seconds = 1.2f;

        [Header("Feedback")]
        [Tooltip("Light or renderer that changes when the door opens. Sound alone is never enough.")]
        [SerializeField] private Renderer indicator;

        [SerializeField] private Color closedColour = new Color(0.7f, 0.2f, 0.2f);
        [SerializeField] private Color openColour = new Color(0.3f, 0.8f, 0.4f);

        /// <summary>Raised when this door finishes opening. Levels use it for completion.</summary>
        public event Action Opened;

        /// <summary>Raised when someone said open but this door was out of reach.</summary>
        public event Action OutOfReach;

        public bool IsOpen { get; private set; }

        private Vector3 _closedPosition;
        private Coroutine _motion;

        private void Awake()
        {
            _closedPosition = transform.localPosition;
            Paint(closedColour);
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
            if (intent.Id != IntentId.Open || IsOpen)
                return;

            if (!WithinReach())
            {
                OutOfReach?.Invoke();
                return;
            }

            Open();
        }

        private bool WithinReach()
        {
            if (rover == null)
                return true;   // no rover assigned means a test scene; do not silently refuse

            return Vector3.Distance(rover.position, transform.position) <= reach;
        }

        public void Open()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            Paint(openColour);

            if (_motion != null)
                StopCoroutine(_motion);

            _motion = StartCoroutine(Slide(_closedPosition + openOffset, () => Opened?.Invoke()));
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            Paint(closedColour);

            if (_motion != null)
                StopCoroutine(_motion);

            _motion = StartCoroutine(Slide(_closedPosition, null));
        }

        /// <summary>
        /// Snap shut, no animation. A sim reset is a run that never happened,
        /// not a rewind the player has to sit through.
        /// </summary>
        public void ResetForSimulation()
        {
            if (_motion != null)
            {
                StopCoroutine(_motion);
                _motion = null;
            }

            IsOpen = false;
            transform.localPosition = _closedPosition;
            Paint(closedColour);
        }

        private IEnumerator Slide(Vector3 target, Action done)
        {
            var start = transform.localPosition;
            var elapsed = 0f;

            // Scaled by game speed so the whole world slows together. A door that opens
            // at full speed in a slowed game is a door that can still catch you out.
            var duration = Mathf.Max(0.01f, seconds / Mathf.Max(0.01f, GameSettings.GameSpeed));

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localPosition = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                yield return null;
            }

            transform.localPosition = target;
            _motion = null;
            done?.Invoke();
        }

        private void Paint(Color colour)
        {
            if (indicator == null)
                return;

            // Visual state, always. Any sound this door makes is a second channel,
            // never the only one: "no essential information conveyed by sounds alone".
            indicator.material.color = colour;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 0.4f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
