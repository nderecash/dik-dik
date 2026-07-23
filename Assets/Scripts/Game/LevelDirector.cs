using System;
using System.Collections;
using Dikdik.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dikdik.Game
{
    /// <summary>
    /// Runs one level: what it is about, what the rover will answer to here, when it
    /// is finished, and what happens next.
    ///
    /// One of these per level scene. Everything level-specific lives in the scene;
    /// this only handles the lifecycle, so a new level is a new scene and not a new
    /// system.
    /// </summary>
    public class LevelDirector : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private int levelNumber = 1;
        [SerializeField] private string levelName = "Dust corridor";

        [Tooltip("The Game Accessibility Guidelines item this level embodies, quoted. " +
                 "Shown on the pause screen, because a claim nobody can check is not a claim.")]
        [TextArea]
        [SerializeField] private string guideline =
            "Ensure no essential information is conveyed by sounds alone";

        [Header("Vocabulary")]
        [Tooltip("What the rover answers to here. Empty means everything. " +
                 "Help, Repeat and Restart are always available regardless.")]
        [SerializeField]
        private IntentId[] allowedIntents =
        {
            IntentId.Go, IntentId.Stop, IntentId.Left, IntentId.Right
        };

        [Header("Flow")]
        [SerializeField] private string nextSceneName = "";

        [Tooltip("Beat between finishing and moving on, so the last thing that happened can land")]
        [SerializeField] private float completionPause = 3f;

        [Header("Wiring")]
        [SerializeField] private SimulationReset simulation;

        public int LevelNumber => levelNumber;
        public string LevelName => levelName;
        public string Guideline => guideline;
        public bool IsComplete { get; private set; }

        /// <summary>Raised once the level is up and the player may start giving commands.</summary>
        public event Action Started;

        /// <summary>Raised the moment the objective is met, before the pause.</summary>
        public event Action Completed;

        private void OnEnable()
        {
            if (simulation != null)
                simulation.Restarted += OnSimulationRestarted;
        }

        private void OnDisable()
        {
            if (simulation != null)
                simulation.Restarted -= OnSimulationRestarted;

            // Never leave our vocabulary restriction behind for the next scene.
            if (CommandBus.Instance != null)
                CommandBus.Instance.AllowedIntents = null;
        }

        private void Start()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.AllowedIntents =
                    allowedIntents != null && allowedIntents.Length > 0 ? allowedIntents : null;

                CommandBus.Instance.ClearInTransit();
            }

            Started?.Invoke();
        }

        /// <summary>
        /// Called by whatever this level treats as finishing: a trigger volume, a door
        /// opening, a rover waking. Levels decide; this only reacts.
        /// </summary>
        public void Complete()
        {
            if (IsComplete)
                return;

            IsComplete = true;
            Completed?.Invoke();

            StartCoroutine(MoveOn());
        }

        private IEnumerator MoveOn()
        {
            // Real seconds, not scaled. Someone playing at 0.25 speed to give themselves
            // room to think should not also have to sit through four times the wait
            // after they have already won.
            yield return new WaitForSecondsRealtime(completionPause);

            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.Log($"[LevelDirector] Level {levelNumber} complete, no next scene set.");
                yield break;
            }

            SceneManager.LoadScene(nextSceneName);
        }

        private void OnSimulationRestarted()
        {
            // A rehearsal being discarded means anything still crossing the gap is
            // from a run that no longer happened. Delivering it afterwards would have
            // the rover obeying a sentence from a timeline the player just abandoned.
            if (CommandBus.Instance != null)
                CommandBus.Instance.ClearInTransit();
        }

        /// <summary>Restart this level from the top. Wired to the pause menu.</summary>
        public void Replay()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
