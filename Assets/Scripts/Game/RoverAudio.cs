using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The sound the rover makes by existing: tires while it rolls, a tick when it brakes.
    ///
    /// <para>Separate from the voice, and on its own AudioSource, because these two must
    /// never fight for one channel. Control talking and the rover driving happen at the
    /// same time constantly, and the voice is radio-degraded on purpose and already hard
    /// to make out.</para>
    ///
    /// <para>Every cue here has a visual twin, which is the rule this project holds itself
    /// to: tires pair with the wheels turning and the speed bar, the brake tick pairs with
    /// the lamp going red. Nothing in this file is the only way to know something. If you
    /// add a sound here and cannot name its visual counterpart, the cue is not finished.</para>
    ///
    /// <para>Tire volume follows speed rather than switching on and off. A rover coasting
    /// to a halt after "stop" is the whole of Level 5, and a sound that cuts out abruptly
    /// would say it had stopped while it was visibly still moving.</para>
    /// </summary>
    public class RoverAudio : MonoBehaviour
    {
        [SerializeField] private RoverController rover;
        [SerializeField] private AudioSource tireSource;
        [SerializeField] private AudioSource effectSource;

        [SerializeField] private AudioClip tireLoop;
        [SerializeField] private AudioClip brakeTick;

        [Header("Tires")]
        [Tooltip("Volume at full speed. Low: this plays constantly and anything louder " +
                 "becomes the thing you hear instead of the voice.")]
        [SerializeField] private float tireVolume = 0.28f;

        [Tooltip("Speed treated as flat out, for both volume and pitch.")]
        [SerializeField] private float fullSpeed = 6f;

        [Tooltip("Pitch at a standstill and at full speed. A narrow range: the rover is " +
                 "heavy and should not sound like it is revving.")]
        [SerializeField] private float minPitch = 0.75f;

        [SerializeField] private float maxPitch = 1.08f;

        [Tooltip("How fast the volume follows the speed. Slow enough to avoid clicking " +
                 "on every small change, fast enough to feel connected.")]
        [SerializeField] private float follow = 6f;

        private bool _brakingShown;

        private void Awake()
        {
            if (tireSource == null || tireLoop == null)
                return;

            tireSource.clip = tireLoop;
            tireSource.loop = true;
            tireSource.volume = 0f;
            tireSource.playOnAwake = false;
        }

        private void Start()
        {
            // Started once and left running, with volume doing the work. Starting and
            // stopping a looping source produces an audible click at both ends, twenty
            // times a minute in a game made of stopping and starting.
            if (tireSource != null && tireLoop != null)
                tireSource.Play();
        }

        private void Update()
        {
            if (rover == null || tireSource == null)
                return;

            if (GamePause.IsPaused)
            {
                // Silence, not pause. The source keeps its position so it does not restart
                // from the top of the loop when play resumes.
                tireSource.volume = 0f;
                return;
            }

            var speed = Mathf.Abs(rover.CurrentSpeed);
            var fraction = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullSpeed));

            var wanted = fraction * tireVolume * GameSettings.EffectsVolume;
            tireSource.volume = Mathf.MoveTowards(tireSource.volume, wanted,
                                                  follow * Time.unscaledDeltaTime);

            tireSource.pitch = Mathf.Lerp(minPitch, maxPitch, fraction);

            WatchBraking();
        }

        /// <summary>
        /// One tick when the rover starts shedding speed it was told to lose. Polled from
        /// the rover's own state, matching how the brake light is driven, so the sound and
        /// the light cannot disagree about when braking started.
        /// </summary>
        private void WatchBraking()
        {
            var braking = rover.IsBraking;

            if (braking == _brakingShown)
                return;

            _brakingShown = braking;

            if (!braking || effectSource == null || brakeTick == null)
                return;

            effectSource.PlayOneShot(brakeTick, GameSettings.EffectsVolume);
        }
    }
}
