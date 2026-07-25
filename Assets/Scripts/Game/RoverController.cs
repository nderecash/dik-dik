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

        [Header("Momentum (opt-in, off by default)")]
        [Tooltip("Units per second squared while speeding up. 0 means instant, which is " +
                 "how the rover behaves everywhere except the slope level.")]
        [SerializeField] private float acceleration = 0f;

        [Tooltip("Units per second squared while slowing down. 0 means instant. A low " +
                 "value here is the whole slope level: the rover coasts after you say " +
                 "stop, so you overshoot, and the game-speed setting is what shrinks it.")]
        [SerializeField] private float deceleration = 0f;

        [Header("Collision")]
        [Tooltip("Distance from the rover's centre to the front of its body. The Kenney " +
                 "rover measures 0.35 long and is attached at 4.2 scale, so the nose sits " +
                 "0.74 out. Probing shorter than this looks for walls inside the rover.")]
        [SerializeField] private float noseOffset = 0.74f;

        [Tooltip("How far past the nose to stop. Small: the rover should come to rest " +
                 "close enough to a rock that stopping reads as deliberate.")]
        [SerializeField] private float stopMargin = 0.35f;

        [Tooltip("Half the rover's width, for the two outer probes. The body is 1.26 " +
                 "across at 4.2 scale.")]
        [SerializeField] private float halfWidth = 0.55f;

        [SerializeField] private LayerMask obstacleMask = ~0;

        /// <summary>Raised when the rover starts or stops moving. Drives lights and sound.</summary>
        public event Action<bool> MovingChanged;

        /// <summary>Raised when the rover is asked to move but something is in the way.</summary>
        public event Action Blocked;

        /// <summary>Raised for any command the rover accepted, so levels can react.</summary>
        public event Action<Intent> Acted;

        public bool IsMoving { get; private set; }

        /// <summary>True while stopped at a junction, waiting to be told which way.</summary>
        public bool IsWaitingAtJunction { get; private set; }

        /// <summary>Signed speed along forward, in units per second. Zero when at rest.</summary>
        public float CurrentSpeed => _currentSpeed;

        private float _targetYaw;
        private int _direction;   // target: 1 forward, -1 back, 0 still
        private float _currentSpeed;
        private bool _resumeAfterTurn;

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
                    Turn(-turnStep);
                    break;

                case IntentId.Right:
                    Turn(turnStep);
                    break;

                default:
                    // Open, Light and the rest belong to objects in the world,
                    // not to the rover. They listen to the bus themselves.
                    return;
            }

            Acted?.Invoke(intent);
        }

        /// <summary>
        /// Stop and wait to be told which way. Called by a junction on arrival.
        ///
        /// This exists because the first playtest found the real problem: with a 2.6
        /// second delay, a corner that needs "forward a little, then turn" is a corner
        /// you overshoot every time. The answer was never a shorter delay. Precision
        /// under latency is what Level 5 is for, and putting it in the level that
        /// teaches the loop was my mistake.
        ///
        /// It is also more in character. A rover that rolls past a decision point
        /// without being asked is doing something on its own, and this one does not.
        /// </summary>
        public void HoldAtJunction()
        {
            if (IsWaitingAtJunction)
                return;

            SetDirection(0);
            IsWaitingAtJunction = true;
        }

        private void Turn(float degrees)
        {
            _targetYaw += degrees;

            // Turning while stopped at a junction means go that way, not pivot on the
            // spot and wait for a second instruction. Making someone say "left" and
            // then "go" at every corner would be the interface asking to be obeyed
            // twice for one decision.
            if (IsWaitingAtJunction)
            {
                _resumeAfterTurn = true;
                IsWaitingAtJunction = false;
            }
        }

        private void Update()
        {
            if (GamePause.IsPaused)
                return;

            // The rover lives in game time, which is real time scaled by the speed
            // setting. Everything it does uses this one delta, so turning, accelerating,
            // coasting and moving all slow together. The transport delay in CommandBus
            // is deliberately NOT scaled: it is 2.6 real seconds whatever the setting.
            // That is why slowing the game shrinks the overshoot on the slope, because
            // the rover covers less ground during those fixed 2.6 seconds.
            var gdelta = Time.deltaTime * GameSettings.GameSpeed;

            TurnTowardTarget(gdelta);
            ResumeIfTurnFinished();
            UpdateMovement(gdelta);
        }

        private void ResumeIfTurnFinished()
        {
            if (!_resumeAfterTurn)
                return;

            var remaining = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, _targetYaw));
            if (remaining > 1.5f)
                return;

            _resumeAfterTurn = false;
            SetDirection(1);
        }

        private void TurnTowardTarget(float gdelta)
        {
            var current = transform.eulerAngles.y;
            var next = Mathf.MoveTowardsAngle(current, _targetYaw, turnSpeed * gdelta);
            transform.rotation = Quaternion.Euler(0f, next, 0f);
        }

        private void UpdateMovement(float gdelta)
        {
            // Ease the actual speed toward the asked-for speed. When acceleration and
            // deceleration are zero, which is the default, this snaps and the rover
            // starts and stops on a dime exactly as before. When they are set, as on the
            // slope, the rover takes time to reach speed and, more to the point, time to
            // shed it, so a late "stop" carries you past the mark.
            var target = _direction * moveSpeed;
            var rate = Mathf.Abs(target) < Mathf.Abs(_currentSpeed) ? deceleration : acceleration;

            _currentSpeed = rate <= 0f
                ? target
                : Mathf.MoveTowards(_currentSpeed, target, rate * gdelta);

            if (Mathf.Abs(_currentSpeed) > 0.001f)
            {
                // Probe in the direction we are actually moving, which after a "stop"
                // on a slope is still forward even though the asked-for direction is nil.
                var sign = Mathf.Sign(_currentSpeed);

                if (IsBlockedAhead(sign, Mathf.Abs(_currentSpeed) * gdelta))
                {
                    // Hit something. Come to rest against it rather than grinding, and
                    // say so: silent failure is indistinguishable from not being heard.
                    _currentSpeed = 0f;
                    _direction = 0;
                    Blocked?.Invoke();
                }
                else
                {
                    transform.position += transform.forward * (_currentSpeed * gdelta);
                }
            }

            UpdateMovingFlag();
        }

        /// <summary>
        /// Three parallel rays down the rover's centreline and both flanks.
        ///
        /// <para>One centre ray used to be enough when every obstacle was a wall spanning
        /// the corridor. Now that rocks are solid and scattered, a single ray lets the
        /// rover shoulder a boulder that its own body plainly overlaps, which looks like
        /// the collision is broken rather than generous.</para>
        ///
        /// <para><paramref name="travelThisFrame"/> extends the reach so a fast rover on a
        /// long frame cannot step over a thin obstacle between one probe and the next. At
        /// this speed it almost never matters; the one time it does, it is a rover through
        /// a wall and no way to explain it.</para>
        ///
        /// <para>Rays start inside the rover's own capsule, which is deliberate: Unity does
        /// not report a convex collider a ray begins inside, so the rover cannot block
        /// itself. Triggers are ignored, because stop pads, junctions and exits are all
        /// volumes the rover is meant to roll straight through.</para>
        /// </summary>
        private bool IsBlockedAhead(float sign, float travelThisFrame)
        {
            var forward = transform.forward * sign;
            var reach = noseOffset + stopMargin + travelThisFrame;

            // Level with the capsule's centre rather than the pivot, so the probe looks
            // along the rover's body and not along the ground it is standing on.
            var origin = transform.position + Vector3.up * 0.2f;
            var side = transform.right * halfWidth;

            return Physics.Raycast(origin, forward, reach, obstacleMask, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(origin + side, forward, reach, obstacleMask, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(origin - side, forward, reach, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void SetDirection(int direction)
        {
            _direction = direction;

            if (direction != 0)
                IsWaitingAtJunction = false;
        }

        private void UpdateMovingFlag()
        {
            var moving = Mathf.Abs(_currentSpeed) > 0.01f;
            if (moving == IsMoving)
                return;

            IsMoving = moving;
            MovingChanged?.Invoke(moving);
        }

        /// <summary>Put the rover back where a level started. Used by level reset.</summary>
        public void ResetTo(Vector3 position, float yaw)
        {
            _direction = 0;
            _currentSpeed = 0f;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _targetYaw = yaw;
            UpdateMovingFlag();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;

            var origin = transform.position + Vector3.up * 0.2f;
            var side = transform.right * halfWidth;
            var reach = transform.forward * (noseOffset + stopMargin);

            Gizmos.DrawLine(origin, origin + reach);
            Gizmos.DrawLine(origin + side, origin + side + reach);
            Gizmos.DrawLine(origin - side, origin - side + reach);
        }
    }
}
