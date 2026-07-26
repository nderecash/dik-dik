using System.Collections;
using Dikdik.Commands;
using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game.Cable
{
    /// <summary>
    /// What happens when the player has got the rover wedged and cannot get it out.
    ///
    /// <para>Open ground made this load-bearing. A corridor level cannot really be failed:
    /// the walls do the navigating. Once the levels opened up, a player who turned the
    /// wrong way twice could genuinely be lost, and a game with no failure state that
    /// leaves you stranded has a failure state after all, just an unadmitted one.</para>
    ///
    /// <para><b>Three strikes, then Control takes over.</b> The escalation is the point.
    /// Rescuing immediately would remove a problem the player has not noticed having;
    /// never rescuing would leave them stuck. So it says something, then says something
    /// with a bit more edge, then acts.</para>
    ///
    /// <list type="bullet">
    /// <item>Strike one: "I think it's stuck. It'll need a new bearing."</item>
    /// <item>Strike two: "Still wedged. Are you giving it directions, or are we waiting
    /// for the automated override?"</item>
    /// <item>Strike three: Control drives it back to the nearest checkpoint.</item>
    /// </list>
    ///
    /// <para><b>Any command cancels the drive instantly.</b> The cancel fires on
    /// CommandAccepted rather than CommandIssued, which is the moment the player finished
    /// speaking rather than the moment the rover would act. That hands them back control a
    /// full transport delay before their instruction lands, so taking over never feels like
    /// wrestling the game for the wheel.</para>
    ///
    /// <para>The drive follows cable waypoints and never pathfinds. It does not have to:
    /// the cable is laid along the same route the terrain was scattered clear of, so
    /// following it is guaranteed to be following open ground. That guarantee is the only
    /// reason a navmesh-free recovery is safe, and it stops being true the moment someone
    /// moves a cable without moving the rocks.</para>
    /// </summary>
    public class CableRecovery : MonoBehaviour, IResettable
    {
        [Header("Wiring")]
        [SerializeField] private CablePath path;
        [SerializeField] private RoverController rover;
        [SerializeField] private RoverLight roverLight;
        [SerializeField] private MissionProgress progress;

        [Header("Detecting stuck")]
        [Tooltip("Blocks within the window that count as one strike's worth of trouble.")]
        [SerializeField] private int blocksPerStrike = 3;

        [SerializeField] private float blockWindowSeconds = 20f;

        [Tooltip("Or: this long asked to move while making no headway along the cable.")]
        [SerializeField] private float noProgressSeconds = 14f;

        [Tooltip("Headway smaller than this does not count. Stops a rover grinding along " +
                 "a wall at a crawl from looking like progress.")]
        [SerializeField] private float progressEpsilon = 1.5f;

        [Header("The drive back")]
        [Tooltip("Fraction of normal speed. Slower on purpose: this is not the player " +
                 "driving, and it should not look like it is.")]
        [SerializeField] private float recoverySpeed = 3.2f;

        [SerializeField] private float recoveryTurnSpeed = 110f;

        [Tooltip("Give up if the drive somehow cannot finish, rather than holding the " +
                 "wheel forever.")]
        [SerializeField] private float driveTimeout = 45f;

        private int _strikes;
        private int _blocksInWindow;
        private float _windowEndsAt;
        private float _bestDistanceAlong;
        private float _noProgressSince;
        private bool _driving;
        private bool _cancelRequested;
        private Coroutine _drive;

        private void OnEnable()
        {
            if (rover != null)
                rover.Blocked += OnBlocked;

            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandAccepted += OnCommandAccepted;
                CommandBus.Instance.CommandIssued += OnCommandIssued;
            }
        }

        private void OnDisable()
        {
            if (rover != null)
                rover.Blocked -= OnBlocked;

            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandAccepted -= OnCommandAccepted;
                CommandBus.Instance.CommandIssued -= OnCommandIssued;
            }
        }

        private void Start()
        {
            ResetProgressWatch();
        }

        private void Update()
        {
            if (GamePause.IsPaused || _driving || rover == null || path == null)
                return;

            if (Time.unscaledTime > _windowEndsAt)
                _blocksInWindow = 0;

            WatchProgress();
        }

        /// <summary>
        /// Has the rover got anywhere lately, measured along the cable rather than through
        /// the air. Driving in a wide circle covers ground and gets nowhere, and the cable
        /// distance is the only measure that knows the difference.
        /// </summary>
        private void WatchProgress()
        {
            // Only counts while it is trying. A rover parked because nobody has said
            // anything is not stuck, it is waiting, and nagging it would be the idle
            // reminder's job and not this one's.
            if (!rover.IsMoving)
            {
                _noProgressSince = Time.unscaledTime;
                return;
            }

            var here = path.DistanceAlong(rover.transform.position);

            if (here > _bestDistanceAlong + progressEpsilon)
            {
                _bestDistanceAlong = here;
                _noProgressSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _noProgressSince >= noProgressSeconds)
                Strike();
        }

        private void OnBlocked()
        {
            if (_driving)
                return;

            if (Time.unscaledTime > _windowEndsAt)
            {
                _blocksInWindow = 0;
                _windowEndsAt = Time.unscaledTime + blockWindowSeconds;
            }

            _blocksInWindow++;

            if (_blocksInWindow < blocksPerStrike)
                return;

            _blocksInWindow = 0;
            Strike();
        }

        private void Strike()
        {
            ResetProgressWatch();
            _strikes++;

            var arbiter = VoiceArbiter.Instance;

            if (_strikes < 3)
            {
                // sup_stuck_01 then sup_stuck_02. Reactive, so a briefing or a scan report
                // outranks it: being told you are stuck is less urgent than the thing
                // already being said.
                if (arbiter != null)
                    arbiter.SayGroup("stuck", SpeechPriority.Reactive, Speaker.Control);

                return;
            }

            _drive = StartCoroutine(DriveBack());
        }

        private void ResetProgressWatch()
        {
            _noProgressSince = Time.unscaledTime;
            _bestDistanceAlong = path != null && rover != null
                ? path.DistanceAlong(rover.transform.position)
                : 0f;
        }

        private void OnCommandAccepted(Intent intent)
        {
            // The moment they spoke, not the moment it lands. See the class comment.
            if (_driving)
                _cancelRequested = true;
        }

        private void OnCommandIssued(Intent intent)
        {
            // Any instruction that actually arrives is evidence the player is engaged and
            // steering, so the strike count starts again. Without this, three strikes
            // accumulated over ten minutes of ordinary play would trigger a rescue nobody
            // needed.
            if (!_driving)
                _strikes = 0;
        }

        /// <summary>
        /// Control drives it to the nearest unscanned checkpoint, or to the nearest point
        /// on the cable if there is none left.
        /// </summary>
        private IEnumerator DriveBack()
        {
            _driving = true;
            _cancelRequested = false;
            _strikes = 0;

            var arbiter = VoiceArbiter.Instance;

            // sup_stuck_03: "It's recalculated to the nearest checkpoint. I'm taking it
            // there myself." Said before anything moves, because the rover moving on its
            // own with no explanation is exactly the thing this game promises never happens.
            if (arbiter != null)
                yield return arbiter.SayGroup("stuck", SpeechPriority.Beat, Speaker.Control,
                                              essential: true);

            if (_cancelRequested)
            {
                Finish();
                yield break;
            }

            rover.BeginRemoteControl();

            if (roverLight != null)
                roverLight.SignalRemoteControl();

            var target = progress != null && progress.NextCheckpoint != null
                ? progress.NextCheckpoint.DistanceAlongCable
                : path.DistanceAlong(rover.transform.position);

            var from = path.DistanceAlong(rover.transform.position);
            var waypoints = path.WaypointsBetween(from, target);

            var deadline = Time.unscaledTime + driveTimeout;

            foreach (var waypoint in waypoints)
            {
                while (!_cancelRequested && Time.unscaledTime < deadline)
                {
                    if (GamePause.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    var here = rover.transform.position;
                    var flat = new Vector3(waypoint.x, here.y, waypoint.z);

                    if (Vector3.Distance(here, flat) < 0.6f)
                        break;

                    var toward = (flat - here).normalized;
                    var wanted = Quaternion.LookRotation(toward, Vector3.up);

                    rover.transform.rotation = Quaternion.RotateTowards(
                        rover.transform.rotation, wanted, recoveryTurnSpeed * Time.deltaTime);

                    // Only drive forward once roughly pointing the right way, so it turns
                    // then goes rather than describing an arc into whatever it was stuck on.
                    if (Quaternion.Angle(rover.transform.rotation, wanted) < 25f)
                        rover.transform.position += rover.transform.forward *
                                                    (recoverySpeed * Time.deltaTime);

                    yield return null;
                }

                if (_cancelRequested || Time.unscaledTime >= deadline)
                    break;
            }

            rover.EndRemoteControl();

            if (roverLight != null)
                roverLight.Release();

            // sup_stuck_04: "It's back on the line. All yours." Said even on a cancel,
            // because the player who spoke over the rescue still needs to know the wheel
            // is theirs again.
            if (arbiter != null)
                arbiter.SayGroup("stuck", SpeechPriority.Reactive, Speaker.Control);

            Finish();
        }

        private void Finish()
        {
            _driving = false;
            _cancelRequested = false;
            _drive = null;
            ResetProgressWatch();
        }

        /// <summary>Put it back for a fresh rehearsal run.</summary>
        public void ResetForSimulation()
        {
            if (_drive != null)
            {
                StopCoroutine(_drive);
                _drive = null;
            }

            if (_driving && rover != null)
                rover.EndRemoteControl();

            if (_driving && roverLight != null)
                roverLight.Release();

            _driving = false;
            _cancelRequested = false;
            _strikes = 0;
            _blocksInWindow = 0;
            ResetProgressWatch();
        }
    }
}
