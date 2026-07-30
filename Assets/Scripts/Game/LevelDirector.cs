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

            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandIssued += OnCommandForContinue;
        }

        private void OnDisable()
        {
            if (simulation != null)
                simulation.Restarted -= OnSimulationRestarted;

            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandIssued -= OnCommandForContinue;

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
                // The last sector. Nothing to move on to.
                Bootstrap.Instance?.Comms?.ShowPrompt("Relay line complete. That's all of it.", "");
                yield break;
            }

            // Load on a short timer, cancellable, rather than waiting to be told.
            //
            // It used to wait for "go" and nothing else. The reasoning was that being moved
            // somewhere without agreeing to it is a small version of what this game argues
            // against, which sounded right and played badly. A playtester's note: the end of
            // a sector usually faces open ground, so a prompt asking you to say "go" arrives
            // while you are looking at nothing in particular and reads as a chore.
            //
            // So the default flips. It advances, and staying is the thing you ask for. That
            // is the same respect pointed the other way: the mission continues unless you
            // want to look around, and if you do want to look around nothing hurries you.
            for (var attempt = 0; ; attempt++)
            {
                // Clear both flags before counting. Escape doubles as the settings key, so
                // it gets pressed during the browse period for reasons that have nothing to
                // do with this countdown, and a stale flag would cancel the next offer the
                // instant it appeared.
                _stayRequested = false;
                _continueRequested = false;

                var cancelled = false;
                var remaining = autoAdvanceSeconds;

                while (remaining > 0f)
                {
                    if (!GamePause.IsPaused)
                        remaining -= Time.unscaledDeltaTime;

                    Bootstrap.Instance?.Comms?.ShowPrompt(
                        attempt == 0 ? "Sector clear." : "Ready when you are.",
                        $"Next sector in {Mathf.CeilToInt(remaining)}.  Press ESC to stay.");

                    if (_stayRequested)
                    {
                        cancelled = true;
                        break;
                    }

                    yield return null;
                }

                if (!cancelled)
                    break;

                // They want to keep exploring. Get out of the way completely, then ask again
                // later rather than never, so nobody is stranded in a finished sector with
                // no route onward.
                Bootstrap.Instance?.Comms?.ClearPrompt();

                var idle = 0f;
                while (idle < reofferSeconds)
                {
                    if (!GamePause.IsPaused)
                        idle += Time.unscaledDeltaTime;

                    // A forward command during the browse period means they are done looking.
                    if (_continueRequested)
                    {
                        _continueRequested = false;
                        break;
                    }

                    yield return null;
                }
            }

            Bootstrap.Instance?.Comms?.ClearPrompt();
            SceneManager.LoadScene(nextSceneName);
        }

        [Header("Sector transition")]
        [Tooltip("Seconds before the next sector loads by itself. ESC cancels and leaves the " +
                 "player to explore.")]
        [SerializeField] private float autoAdvanceSeconds = 3f;

        [Tooltip("After cancelling, how long to leave them alone before offering again. " +
                 "Never offering would strand somebody in a finished sector.")]
        [SerializeField] private float reofferSeconds = 120f;

        private bool _stayRequested;
        private bool _continueRequested;

        /// <summary>
        /// A forward command during the browse period means they have finished looking
        /// around. Generous on purpose: go, continue, carry on and keep going all resolve
        /// to Go already.
        /// </summary>
        private void OnCommandForContinue(Intent intent)
        {
            if (intent.Id == IntentId.Go || intent.Id == IntentId.Wake)
                _continueRequested = true;
        }

        private void Update()
        {
            if (GamePause.IsPaused || !IsComplete)
                return;

            // ESC cancels the countdown. It is also the settings key, and SettingsMenu reads
            // it too, so this fires alongside the menu opening rather than instead of it.
            // That is acceptable: both mean "wait, I want to do something else."
            if (Input.GetKeyDown(KeyCode.Escape))
                _stayRequested = true;

            // Keyboard parity for continuing, same as the voice path.
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                _continueRequested = true;
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
