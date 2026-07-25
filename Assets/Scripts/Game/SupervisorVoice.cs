using Dikdik.Commands;
using Dikdik.Game.Voice;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dikdik.Game
{
    /// <summary>
    /// Decides <em>when</em> Control speaks. It no longer decides how.
    ///
    /// <para>This used to own an AudioSource and play clips directly, which is how four
    /// unrelated components ended up talking over each other. All of that moved to
    /// <see cref="VoiceArbiter"/>. What is left is the part that was always the
    /// interesting bit: the policy about how often a voice should interrupt someone who is
    /// trying to play.</para>
    ///
    /// <para>The policy is restraint. The console prints what it heard and the rover
    /// pulses its light on every single command. If Control also spoke every time, the
    /// voice would become noise and the player would stop hearing it. So an
    /// acknowledgement happens once per level, a misheard line has a cooldown, and idle
    /// chatter pushes itself further away each time it fires.</para>
    ///
    /// <para>Lives in the persistent Boot scene and re-finds the per-level rover, director
    /// and safety system on every scene load, because those are rebuilt per level and this
    /// is not.</para>
    /// </summary>
    public class SupervisorVoice : MonoBehaviour
    {
        [Header("Restraint")]
        [Tooltip("Seconds between spoken not-understood lines. The console still shows " +
                 "every one; the voice chimes in less often so it does not lecture.")]
        [SerializeField] private float missCooldown = 9f;

        [Tooltip("Seconds of stillness before the first idle line.")]
        [SerializeField] private float idleAfter = 40f;

        [Tooltip("How much further out each idle line pushes the next.")]
        [SerializeField] private float idleBackoff = 1.6f;

        [SerializeField] private float idleMaxInterval = 180f;

        [Tooltip("Total idle seconds before Control drops the line to save power. The " +
                 "console must show a visible way back when this happens.")]
        [SerializeField] private float powerSaveAfter = 600f;

        private float _nextMissAllowed;
        private float _idleAt;
        private float _idleInterval;
        private float _idleSince;
        private bool _poweredDown;
        private bool _bootPlayed;
        private bool _ackThisLevel;

        private RoverController _rover;
        private SimulationReset _simulation;
        private LevelDirector _director;

        private static VoiceArbiter Arbiter => VoiceArbiter.Instance;

        /// <summary>True while Control has dropped the line. The console shows the way back.</summary>
        public bool IsPoweredDown => _poweredDown;

        private void OnEnable()
        {
            var bus = CommandBus.Instance;
            if (bus != null)
            {
                bus.CommandIssued += OnCommandIssued;
                bus.CommandNotUnderstood += OnNotUnderstood;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            var bus = CommandBus.Instance;
            if (bus != null)
            {
                bus.CommandIssued -= OnCommandIssued;
                bus.CommandNotUnderstood -= OnNotUnderstood;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookLevel();
        }

        // ------------------------------------------------------------------
        // Per-level wiring
        // ------------------------------------------------------------------

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnhookLevel();

            _rover = FindAnyObjectByType<RoverController>();
            _simulation = FindAnyObjectByType<SimulationReset>();
            _director = FindAnyObjectByType<LevelDirector>();

            _ackThisLevel = false;
            ResetIdle();

            if (_rover != null) _rover.Blocked += OnBlocked;
            if (_simulation != null) _simulation.Aborted += OnAborted;

            if (_director == null)
                return;

            _director.Completed += OnLevelComplete;

            // The opening plays on the first level that has a director, once per session,
            // whichever level that happens to be. Someone jumping straight to level four
            // still gets told what they are doing.
            if (_bootPlayed)
                return;

            _bootPlayed = true;
            SaySequenceWithFallback("open", "boot", SpeechPriority.Critical);
        }

        private void UnhookLevel()
        {
            if (_rover != null) _rover.Blocked -= OnBlocked;
            if (_simulation != null) _simulation.Aborted -= OnAborted;
            if (_director != null) _director.Completed -= OnLevelComplete;

            _rover = null;
            _simulation = null;
            _director = null;
        }

        // ------------------------------------------------------------------
        // Reactions
        // ------------------------------------------------------------------

        private void OnCommandIssued(Intent intent)
        {
            WakeFromPowerSave();
            ResetIdle();

            // Once per level, on the first command that worked. After that the console's
            // own feedback carries it and Control stays out of the way.
            if (_ackThisLevel)
                return;

            _ackThisLevel = true;
            Arbiter?.SayGroup("ack", SpeechPriority.Reactive);
        }

        private void OnNotUnderstood(Intent intent)
        {
            WakeFromPowerSave();
            ResetIdle();

            if (Time.unscaledTime < _nextMissAllowed)
                return;

            _nextMissAllowed = Time.unscaledTime + missCooldown;
            Arbiter?.SayGroup("miss", SpeechPriority.Reactive);
        }

        private void OnBlocked() => Arbiter?.SayGroup("block", SpeechPriority.Reactive);

        private void OnAborted(string _)
        {
            // The rover's safety cutout backed it away from something. Essential: the
            // player needs to know why it moved without being told to.
            SayGroupWithFallback("cut", "reset", SpeechPriority.Beat, essential: true);
        }

        private void OnLevelComplete() =>
            Arbiter?.SayGroup("done", SpeechPriority.Beat, Speaker.Control, essential: true);

        // ------------------------------------------------------------------
        // Idle
        // ------------------------------------------------------------------

        private void Update()
        {
            if (GamePause.IsPaused || _poweredDown || Arbiter == null)
                return;

            if (Time.unscaledTime < _idleAt)
                return;

            var bus = CommandBus.Instance;
            var quiet = (_rover == null || !_rover.IsMoving) &&
                        (bus == null || bus.InTransitCount == 0) &&
                        !Arbiter.IsSpeaking;

            if (quiet)
            {
                // Alone long enough that Control drops the line rather than keep talking
                // into an empty room. The last idle line is the one that says so.
                if (Time.unscaledTime - _idleSince > powerSaveAfter)
                    _poweredDown = true;

                Arbiter.SayGroup("idle", SpeechPriority.Idle);

                // Each one pushes the next further out, so standing still for a long
                // stretch does not mean being told every forty seconds that the rover
                // does not mind waiting.
                _idleInterval = Mathf.Min(_idleInterval * idleBackoff, idleMaxInterval);
            }

            _idleAt = Time.unscaledTime + _idleInterval;
        }

        private void ResetIdle()
        {
            _idleInterval = idleAfter;
            _idleAt = Time.unscaledTime + _idleInterval;
            _idleSince = Time.unscaledTime;
        }

        private void WakeFromPowerSave() => _poweredDown = false;

        // ------------------------------------------------------------------
        // Group fallbacks
        //
        // The rework renames two groups: boot becomes open, reset becomes cut. Until the
        // new lines are recorded the old ones still play, so the game stays voiced through
        // the transition instead of going silent in the middle of a rewrite.
        // ------------------------------------------------------------------

        private void SayGroupWithFallback(string primary, string fallback,
                                          SpeechPriority priority, bool essential = false)
        {
            if (Arbiter == null)
                return;

            var handle = Arbiter.SayGroup(primary, priority, Speaker.Control, essential);
            if (handle.IsDone && !handle.WasDropped)
                Arbiter.SayGroup(fallback, priority, Speaker.Control, essential);
        }

        private void SaySequenceWithFallback(string primary, string fallback, SpeechPriority priority)
        {
            if (Arbiter == null)
                return;

            var handle = Arbiter.SaySequence(primary, priority);
            if (handle.IsDone && !handle.WasDropped)
                Arbiter.SaySequence(fallback, priority);
        }
    }
}
