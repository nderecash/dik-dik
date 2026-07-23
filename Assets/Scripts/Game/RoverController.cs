using System;
using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The rover. Fully capable, completely still until somebody speaks to it.
    ///
    /// It has no idle wander, no autopilot and no ambient behaviour on purpose.
    /// A rover that drifts about on its own would quietly undo the premise: the
    /// point is that all this capability sits waiting on being addressed.
    ///
    /// It listens to <see cref="CommandBus"/> and never to a producer, so it cannot
    /// tell whether the instruction arrived by voice or by key, and cannot behave
    /// differently depending on the answer.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoverController : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Units per second at game speed 1.0")]
        [SerializeField] private float moveSpeed = 2.5f;

        [Tooltip("Degrees per second while turning")]
        [SerializeField] private float turnSpeed = 180f;

        [Tooltip("Degrees per Left or Right command")]
        [SerializeField] private float turnStep = 90f;

        [Header("Collision")]
        [Tooltip("How far ahead to look for walls")]
        [SerializeField] private float probeDistance = 0.6f;

        [SerializeField] private LayerMask obstacleMask = ~0;

        /// <summary>Raised when the rover starts or stops moving. Drives lights and sound.</summary>
        public event Action<bool> MovingChanged;

        /// <summary>Raised when the rover is asked to move but something is in the way.</summary>
        public event Action Blocked;

        /// <summary>Raised for any command the rover accepted, so levels can react.</summary>
        public event Action<Intent> Acted;

        public bool IsMoving { get; private set; }

        private float _targetYaw;
        private int _direction;   // 1 forward, -1 back, 0 still

        private void Awake()
        {
            _targetYaw = transform.eulerAngles.y;
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
            switch (intent.Id)
            {
                case IntentId.Go:
                    SetDirection(1);
                    break;

                case IntentId.Back:
                    SetDirection(-1);
                    break;

                case IntentId.Stop:
                    SetDirection(0);
                    break;

                case IntentId.Left:
                    _targetYaw -= turnStep;
                    break;

                case IntentId.Right:
                    _targetYaw += turnStep;
                    break;

                default:
                    // Open, Light and the rest belong to objects in the world,
                    // not to the rover. They listen to the bus themselves.
                    return;
            }

            Acted?.Invoke(intent);
        }

        private void Update()
        {
            // One multiplier, applied to everything the rover does. At 0.25 the whole
            // game waits for you, which is the point of the setting.
            var speedScale = GameSettings.GameSpeed;
            var delta = Time.deltaTime;

            TurnTowardTarget(delta, speedScale);
            MoveIfAsked(delta, speedScale);
        }

        private void TurnTowardTarget(float delta, float speedScale)
        {
            var current = transform.eulerAngles.y;
            var next = Mathf.MoveTowardsAngle(current, _targetYaw, turnSpeed * speedScale * delta);
            transform.rotation = Quaternion.Euler(0f, next, 0f);
        }

        private void MoveIfAsked(float delta, float speedScale)
        {
            if (_direction == 0)
                return;

            var step = transform.forward * _direction;

            if (Physics.Raycast(transform.position, step, probeDistance, obstacleMask))
            {
                // Stop rather than grind against the wall, and say so. Silent failure
                // is indistinguishable from not being heard, which is the one feeling
                // this game must never produce by accident.
                SetDirection(0);
                Blocked?.Invoke();
                return;
            }

            transform.position += step * (moveSpeed * speedScale * delta);
        }

        private void SetDirection(int direction)
        {
            _direction = direction;

            var moving = direction != 0;
            if (moving == IsMoving)
                return;

            IsMoving = moving;
            MovingChanged?.Invoke(moving);
        }

        /// <summary>Put the rover back where a level started. Used by level reset.</summary>
        public void ResetTo(Vector3 position, float yaw)
        {
            SetDirection(0);
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _targetYaw = yaw;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * probeDistance);
        }
    }
}
