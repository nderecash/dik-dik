using System;
using System.Collections;
using System.Linq;
using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>Anything that has to be put back when the sim runs again.</summary>
    public interface IResettable
    {
        void ResetForSimulation();
    }

    /// <summary>
    /// There is no failure in this game. There is only running it again.
    ///
    /// Borrowed from Katana Zero, where dying is not dying: the level is the character
    /// predicting how a fight will go, so a bad outcome is a bad plan being discarded.
    /// No death screen, no counter, no loading, and the fiction absorbs the retry so it
    /// never feels like punishment.
    ///
    /// Mission control gives us that for free and more honestly, because it is real
    /// procedure rather than a fantasy premise. Command sequences are rehearsed in
    /// simulation before anyone uplinks them. So a rover that drives into a crevasse
    /// did not die. It has not happened yet. You abort the run and take it again.
    ///
    /// This matters beyond tone. A game arguing that exclusion is a design failure
    /// cannot then punish a player for not being understood. If speech recognition
    /// mishears you and the rover drives off a ledge, being charged for that would
    /// make the game an example of the thing it is complaining about.
    /// </summary>
    public class SimulationReset : MonoBehaviour
    {
        [Header("Start state")]
        [SerializeField] private RoverController rover;
        [SerializeField] private Transform startMarker;

        [Header("Abort conditions")]
        [Tooltip("Below this height the rover has fallen out of the level")]
        [SerializeField] private float floorY = -5f;

        [Header("Feel")]
        [Tooltip("Beat before the reset lands. Long enough to register, short enough not to punish.")]
        [SerializeField] private float pauseSeconds = 1.2f;

        [Tooltip("Dry, never scolding. The sim failing is not the player failing.")]
        [SerializeField]
        private string[] lines =
        {
            "Sim aborted. Resetting.",
            "That is not how that goes. Take two.",
            "Run it back.",
            "Noted. Again from the top.",
            "Good. Now we know. Again."
        };

        /// <summary>Raised with the line to display and speak. Nothing here plays audio itself.</summary>
        public event Action<string> Aborted;

        /// <summary>Raised once the world is back at the start.</summary>
        public event Action Restarted;

        private Vector3 _startPosition;
        private float _startYaw;
        private bool _resetting;
        private int _lineIndex;

        private void Awake()
        {
            if (startMarker != null)
            {
                _startPosition = startMarker.position;
                _startYaw = startMarker.eulerAngles.y;
            }
            else if (rover != null)
            {
                _startPosition = rover.transform.position;
                _startYaw = rover.transform.eulerAngles.y;
            }
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
            if (intent.Id == IntentId.Restart)
                Abort();
        }

        private void Update()
        {
            if (_resetting || rover == null)
                return;

            if (rover.transform.position.y < floorY)
                Abort();
        }

        /// <summary>Call this from a hazard trigger, or let the player ask for it.</summary>
        public void Abort()
        {
            if (_resetting)
                return;

            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            _resetting = true;

            Aborted?.Invoke(NextLine());

            // Cycle rather than randomise. Random repeats itself in a way players notice
            // and read as the game not paying attention.
            var wait = pauseSeconds / Mathf.Max(0.25f, GameSettings.GameSpeed);
            yield return new WaitForSeconds(wait);

            if (rover != null)
                rover.ResetTo(_startPosition, _startYaw);

            // FindObjectsByType with None sorting: we do not care about order and
            // sorting a scene's worth of objects every reset is wasted work.
            var resettables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IResettable>();

            foreach (var item in resettables)
                item.ResetForSimulation();

            Restarted?.Invoke();
            _resetting = false;
        }

        private string NextLine()
        {
            if (lines == null || lines.Length == 0)
                return "Resetting.";

            var line = lines[_lineIndex % lines.Length];
            _lineIndex++;
            return line;
        }
    }
}
