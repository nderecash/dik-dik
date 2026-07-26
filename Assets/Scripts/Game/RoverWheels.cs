using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The rover's visible motion: a body that rocks over the ground while it drives, and
    /// wheels that turn if the model has any.
    ///
    /// <para>It earns its place twice. It makes the rover look like a machine crossing
    /// terrain rather than a box sliding along a plane, and it is a second silent channel
    /// for speed. Level 5 is about a rover that keeps rolling after you say stop, and a
    /// body still rocking while the lamp is red says that without a word.</para>
    ///
    /// <para><b>Why the bob does the work.</b> The Kenney rover is a single mesh. Measured
    /// with KenneyInspect: one renderer, 0.30 by 0.39 by 0.35. Its wheels are moulded into
    /// the body, so there is nothing to spin, and the honest options were to kitbash six
    /// separate wheels onto it or to animate the body. The body won: at this camera
    /// distance a pitch of about a degree reads clearly as driving, and six added wheels
    /// would have to line up perfectly with painted ones that are already there.</para>
    ///
    /// <para>The wheel list stays and stays empty. If a level ever kitbashes wheels named
    /// "Wheel...", they spin at the correct circumference with no further work.</para>
    ///
    /// <para>All of it is local to the body transform, never the rover root. The root's
    /// position is the rover's actual location, read by the cable, the checkpoints and the
    /// recovery drive, and none of them should see a decorative wobble.</para>
    /// </summary>
    public class RoverWheels : MonoBehaviour
    {
        [SerializeField] private RoverController rover;

        [Tooltip("The rover model. Bobs and pitches while driving. Leave empty to skip.")]
        [SerializeField] private Transform body;

        [Tooltip("Wheel transforms, if the model has separate ones. Usually empty: the " +
                 "Kenney rover is a single mesh.")]
        [SerializeField] private Transform[] wheels = new Transform[0];

        [Tooltip("Wheel radius in world units, for rolling without skid.")]
        [SerializeField] private float radius = 0.28f;

        [SerializeField] private Vector3 spinAxis = Vector3.right;

        [Header("Body motion")]
        [Tooltip("Vertical travel at full speed, in world units. Small. This is suspension " +
                 "over gravel, not a boat in a swell.")]
        [SerializeField] private float bobHeight = 0.035f;

        [Tooltip("Degrees of pitch at full speed.")]
        [SerializeField] private float pitchDegrees = 1.1f;

        [Tooltip("Bobs per second at full speed.")]
        [SerializeField] private float bobRate = 7f;

        [SerializeField] private float fullSpeed = 6f;

        private Vector3 _restPosition;
        private Quaternion _restRotation;
        private float _phase;

        private void Reset()
        {
            rover = GetComponentInParent<RoverController>();
        }

        private void Start()
        {
            if (body != null)
            {
                _restPosition = body.localPosition;
                _restRotation = body.localRotation;
            }
        }

        private void Update()
        {
            if (rover == null || GamePause.IsPaused)
                return;

            var speed = rover.CurrentSpeed;
            var fraction = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(0.01f, fullSpeed));

            SpinWheels(speed);
            MoveBody(fraction);
        }

        private void SpinWheels(float speed)
        {
            if (wheels == null || wheels.Length == 0 || Mathf.Abs(speed) < 0.001f)
                return;

            // Distance over circumference, in degrees. Uses the same scaled delta as the
            // rover itself, so slowing the game slows the wheels with everything else.
            var travelled = speed * Time.deltaTime * GameSettings.GameSpeed;
            var degrees = travelled / (2f * Mathf.PI * Mathf.Max(0.01f, radius)) * 360f;

            foreach (var wheel in wheels)
            {
                if (wheel != null)
                    wheel.Rotate(spinAxis, degrees, Space.Self);
            }
        }

        private void MoveBody(float fraction)
        {
            if (body == null)
                return;

            // The phase only advances while moving, so the rover settles where it stopped
            // instead of continuing to twitch at a standstill.
            _phase += Time.deltaTime * GameSettings.GameSpeed * bobRate * fraction;

            var bob = Mathf.Sin(_phase) * bobHeight * fraction;

            // Pitch runs at half the bob's rate so the two never lock into one obvious
            // repeating beat.
            var pitch = Mathf.Sin(_phase * 0.5f) * pitchDegrees * fraction;

            body.localPosition = _restPosition + new Vector3(0f, bob, 0f);
            body.localRotation = _restRotation * Quaternion.Euler(pitch, 0f, pitch * 0.4f);
        }
    }
}
