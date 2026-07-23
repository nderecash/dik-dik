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
        [Tooltip("Offset in the rover's local space: behind and above. High enough to " +
                 "see over the corridor walls, because the first playtest spent half " +
                 "of every turn looking at a wall face.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -12f);

        [Tooltip("Look well ahead of the rover, not at it. Down the corridor rather " +
                 "than at the thing in front of the camera.")]
        [SerializeField] private float lookAhead = 7f;

        [SerializeField] private float lookHeight = 0.5f;

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
