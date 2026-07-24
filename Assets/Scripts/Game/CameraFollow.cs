using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Follows the rover from behind and above.
    ///
    /// Deliberately slow and slightly lagging. A camera that snaps to the rover would
    /// hide the one thing this game is about: that there is a gap between asking and
    /// happening. Letting the frame drift a beat behind makes the delay legible without
    /// a single line of UI explaining it.
    ///
    /// It also never rotates faster than the rover does, so a turn reads as the rover
    /// turning rather than as the world spinning.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Framing")]
        [Tooltip("Offset in the rover's local space: behind and a little above. Low " +
                 "enough that the bright horizon sits behind the rover, which is what " +
                 "the silhouette look needs, high enough to still see over the walls.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 6.5f, -12f);

        [Tooltip("Look well ahead of the rover and up a touch, out toward the horizon " +
                 "rather than down at the ground right in front.")]
        [SerializeField] private float lookAhead = 18f;

        [SerializeField] private float lookHeight = 3.5f;

        [Header("Lag")]
        [Tooltip("Lower is lazier. This is the drift that makes the signal delay visible.")]
        [SerializeField] private float positionSmoothing = 2.5f;

        [SerializeField] private float rotationSmoothing = 3f;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }

        private void Start()
        {
            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            // Unscaled by game speed on purpose. At 0.25x the world should crawl; the
            // camera following it should not also feel like it is wading through syrup.
            var delta = Time.deltaTime;

            var wantedPosition = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, wantedPosition,
                                              1f - Mathf.Exp(-positionSmoothing * delta));

            var focus = target.position + target.forward * lookAhead + Vector3.up * lookHeight;
            var wantedRotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wantedRotation,
                                                  1f - Mathf.Exp(-rotationSmoothing * delta));
        }

        private void SnapToTarget()
        {
            if (target == null)
                return;

            transform.position = target.TransformPoint(offset);

            var focus = target.position + target.forward * lookAhead + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(focus - transform.position, Vector3.up);
        }
    }
}
