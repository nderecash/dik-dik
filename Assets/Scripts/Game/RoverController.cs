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

        /// <summary>True while a checkpoint scan has the wheel. See BeginScanHold.</summary>
        public bool IsScanHeld { get; private set; }

        /// <summary>True while Control is driving it back to the line. See BeginRemoteControl.</summary>
        public bool IsRemoteControlled { get; private set; }

        /// <summary>
        /// The rover has heard someone key up and is easing off until it knows what they
        /// want. See <see cref="SetAttentive"/>.
        /// </summary>
        public bool IsAttentive { get; private set; }

        /// <summary>
        /// A steering command given during a scan, waiting to be obeyed. Null when there
        /// is none. The HUD shows this so a held command never looks like an ignored one.
        /// </summary>
        public Intent? HeldCommand { get; private set; }

        /// <summary>Raised when the held command appears or is released.</summary>
        public event Action<Intent?> HeldCommandChanged;

        /// <summary>Signed speed along forward, in units per second. Zero when at rest.</summary>
        public float CurrentSpeed => _currentSpeed;

        /// <summary>
        /// Still rolling, but told to stop. This is the whole of Level 5 in one boolean:
        /// on the slope the rover coasts after "stop", and the brake light is what makes
        /// that visible rather than merely frustrating.
        /// </summary>
        public bool IsBraking => _direction == 0 && Mathf.Abs(_currentSpeed) > 0.01f;

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
            // Held, not dropped. See BeginScanHold.
            if (IsScanHeld && IsDrivingIntent(intent.Id))
            {
                HeldCommand = intent;
                HeldCommandChanged?.Invoke(HeldCommand);
                return;
            }

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

        /// <summary>
        /// Take the wheel for a scan, and keep anything the player says while we have it.
        ///
        /// <para>The rover stops itself at a checkpoint because landing a stop on a mark
        /// under a 2.6 second delay is not a skill, it is a coin toss. But taking control
        /// away creates the risk this whole game exists to avoid: a player speaks, and
        /// nothing happens, and they cannot tell whether they were heard.</para>
        ///
        /// <para>So the command is held rather than ignored, shown on the panel as waiting,
        /// and obeyed the moment the scan ends. The hold is one deep and newest wins, which
        /// is the same rule as everywhere else: the rover does the most recent thing it was
        /// told, not a backlog of everything it was ever told.</para>
        /// </summary>
        public void BeginScanHold()
        {
            if (IsScanHeld)
                return;

            SetDirection(0);
            IsScanHeld = true;
            HeldCommand = null;
            HeldCommandChanged?.Invoke(null);
        }

        /// <summary>Give the wheel back, and obey whatever was said while we had it.</summary>
        public void EndScanHold()
        {
            if (!IsScanHeld)
                return;

            IsScanHeld = false;

            var pending = HeldCommand;
            HeldCommand = null;
            HeldCommandChanged?.Invoke(null);

            if (pending.HasValue)
                OnCommand(pending.Value);
        }

        [Header("Attention")]
        [Tooltip("Fraction of normal speed while someone is mid-sentence. Not a stop: the " +
                 "rover eases off, it does not decide for itself to halt.")]
        [SerializeField] private float attentiveSpeedFactor = 0.45f;

        /// <summary>
        /// Someone has started talking. Ease off until we know what they want.
        ///
        /// <para>This is the answer to "stop should be instant" that does not break
        /// anything else. The naive version, acting on a hotword the moment it is heard,
        /// privileges voice over the keyboard and undoes the transport delay, which is the
        /// mechanism holding those two inputs level with each other.</para>
        ///
        /// <para>So the rover does not act early. It slows early. A machine that eases off
        /// when the radio keys up, before it knows what is coming, is a local reflex and
        /// not a remote command, so the delay survives intact and the fiction gets better
        /// rather than worse. Saying "stop" now feels immediate because the rover is
        /// already shedding speed by the time the word arrives.</para>
        ///
        /// <para>Both inputs get it. Voice triggers on the microphone's voice detection at
        /// about a tenth of a second; the keyboard triggers on the key going down. Neither
        /// reaches the rover first.</para>
        /// </summary>
        public void SetAttentive(bool attentive)
        {
            IsAttentive = attentive;
        }

        /// <summary>
        /// Control takes the wheel remotely. Used only by the recovery drive.
        ///
        /// <para>While this is on, the rover neither steers nor moves itself: something
        /// else is writing its transform. The framing matters and is not decoration. A
        /// rover that unstuck itself would be a rover that acts without being asked, and
        /// the entire premise is that it does not. So the fiction is that Control is
        /// driving it, the voice line says exactly that, and the light shows a state the
        /// player has never seen the rover produce on its own.</para>
        /// </summary>
        public void BeginRemoteControl()
        {
            SetDirection(0);
            _currentSpeed = 0f;
            IsRemoteControlled = true;
        }

        /// <summary>
        /// Adopt whatever direction the transform is currently facing as the intended one.
        ///
        /// For anything that moves the rover's rotation directly, like the spin easter egg,
        /// and then wants the controller to carry on from there rather than snapping back
        /// to a heading it was aiming at before.
        /// </summary>
        public void SnapHeadingToTransform()
        {
            _targetYaw = transform.eulerAngles.y;
            _resumeAfterTurn = false;
        }

        /// <summary>Hand the wheel back, pointing wherever the recovery left it.</summary>
        public void EndRemoteControl()
        {
            IsRemoteControlled = false;

            // Adopt the heading the recovery drive finished on. Without this the rover
            // snaps back to whatever it was aiming at before it got stuck, which after a
            // recovery is almost always into the thing it was stuck on.
            _targetYaw = transform.eulerAngles.y;
            _resumeAfterTurn = false;
        }

        /// <summary>Commands that steer. Everything else belongs to objects in the world.</summary>
        private static bool IsDrivingIntent(IntentId id)
        {
            return id == IntentId.Go || id == IntentId.Back || id == IntentId.Stop
                || id == IntentId.Left || id == IntentId.Right;
        }

        [Tooltip("Off. A turn given while stopped turns and stays stopped, and going again " +
                 "takes a separate word. On, a turn at a junction also sets off. See Turn().")]
        [SerializeField] private bool driveOnAfterTurn = false;

        private void Turn(float degrees)
        {
            _targetYaw += degrees;

            if (!driveOnAfterTurn || !IsWaitingAtJunction)
                return;

            // The old behaviour, now off by default.
            //
            // The argument for it was that making somebody say "left" and then "go" at
            // every corner is the interface asking to be obeyed twice for one decision.
            // That reasoning was fine and the playtest still killed it, for a reason no
            // amount of reasoning would have found: a turn that also sets off is
            // unrecoverable under a 2.6 second delay. You turn to escape a wall, the rover
            // drives before you have judged the angle, and by the time "stop" arrives you
            // are in whatever you were turning away from.
            //
            // Separated, a turn is a free action. Line the rover up, look at it, then go.
            // It costs a second word per corner and buys back the ability to be careful,
            // which is the thing latency takes away and the thing this game is about.
            _resumeAfterTurn = true;
            IsWaitingAtJunction = false;
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

            // Something else owns the transform while Control is driving. Steering and
            // moving here as well would have two things writing one position.
            if (IsRemoteControlled)
                return;

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
            var ceiling = IsAttentive ? moveSpeed * attentiveSpeedFactor : moveSpeed;
            var target = _direction * ceiling;
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

        /// <summary>
        /// Put the rover back where the level started, listening.
        ///
        /// <para><b>Every hold is released here, unconditionally.</b> That is the whole
        /// point of this method and it was missing, which cost a playtest.</para>
        ///
        /// <para>What happened: a safety cutout fired while a checkpoint scan was running.
        /// The scan had called BeginScanHold, which makes the rover keep commands instead
        /// of obeying them. The reset stopped the scan coroutine, so the matching
        /// EndScanHold never ran, and the rover sat there silently swallowing every
        /// instruction for the rest of the level. Nothing on screen said why, because from
        /// the rover's point of view nothing was wrong: it was waiting for a scan that no
        /// longer existed.</para>
        ///
        /// <para>Clearing the flags at each call site would fix that one path and leave the
        /// next one to be discovered the same way. A reset means "the rover is as it was at
        /// the start of the level", and at the start of the level it listens. So this is
        /// the one place that has to be right, and anything that grabs the wheel can be
        /// interrupted without having to clean up after itself.</para>
        /// </summary>
        public void ResetTo(Vector3 position, float yaw)
        {
            _direction = 0;
            _currentSpeed = 0f;

            IsScanHeld = false;
            IsRemoteControlled = false;
            IsWaitingAtJunction = false;
            IsAttentive = false;
            _resumeAfterTurn = false;

            if (HeldCommand.HasValue)
            {
                // Dropped rather than obeyed. It was given during a run that has just been
                // discarded, and delivering it now would have the rover acting on a
                // sentence from a timeline the player already abandoned.
                HeldCommand = null;
                HeldCommandChanged?.Invoke(null);
            }

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
