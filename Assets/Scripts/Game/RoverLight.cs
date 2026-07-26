using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The rover's lamp, and its face.
    ///
    /// Two jobs. It is a command the player can give, and it is the visible half of
    /// every sound the rover makes. Any time this game plays a noise to tell you
    /// something, this light says the same thing at the same moment. That is the
    /// guideline "ensure no essential information is conveyed by sounds alone",
    /// implemented as a rule rather than remembered per level.
    ///
    /// If you add an audio cue anywhere in this project and it has no counterpart
    /// here or on screen, the cue is not finished.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class RoverLight : MonoBehaviour
    {
        [Header("Lamp")]
        [SerializeField] private float onIntensity = 3f;
        [SerializeField] private float offIntensity = 0.15f;
        [SerializeField] private float fadeSpeed = 6f;

        [Header("Signal colours")]
        [SerializeField] private Color idleColour = new Color(0.85f, 0.88f, 1f);
        [SerializeField] private Color movingColour = new Color(0.6f, 0.9f, 1f);
        [SerializeField] private Color blockedColour = new Color(1f, 0.45f, 0.35f);
        [SerializeField] private Color heardColour = new Color(0.6f, 1f, 0.7f);

        [Header("Pulse")]
        [SerializeField] private float pulseSeconds = 0.45f;

        [SerializeField] private RoverController rover;

        [Tooltip("Emissive band on the shell. In a silhouette world where every solid " +
                 "thing renders black, this is how the rover is visible at all.")]
        [SerializeField] private Renderer shell;

        private Light _light;
        private Material _shellMaterial;
        private bool _on = true;
        private Color _target;
        private float _pulseUntil;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _target = idleColour;
            _light.color = idleColour;

            // Instanced so one rover changing colour does not repaint every object
            // sharing the material.
            if (shell != null)
                _shellMaterial = shell.material;
        }

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandIssued += OnCommand;
                CommandBus.Instance.CommandNotUnderstood += OnNotUnderstood;
            }

            if (rover != null)
            {
                rover.MovingChanged += OnMovingChanged;
                rover.Blocked += OnBlocked;
            }
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandIssued -= OnCommand;
                CommandBus.Instance.CommandNotUnderstood -= OnNotUnderstood;
            }

            if (rover != null)
            {
                rover.MovingChanged -= OnMovingChanged;
                rover.Blocked -= OnBlocked;
            }
        }

        private void OnCommand(Intent intent)
        {
            if (intent.Id == IntentId.Light)
                _on = !_on;

            // Every understood command gets a visible acknowledgement, whatever it was.
            // The player should never have to guess whether the rover registered them.
            SignalUnderstood();
        }

        private void OnNotUnderstood(Intent intent)
        {
            // Being misheard also gets a signal. Silence is the one response that
            // feels like being ignored, and this game is about not being ignored.
            SignalNotUnderstood();
        }

        private void OnMovingChanged(bool moving)
        {
            // A scan result stays up until the rover drives away from it, so a player who
            // looks up a second after the report still sees whether the section was clean.
            if (_held && moving && _releaseOnMove)
                Release();

            // Any other held signal outranks this. Scanning is orange until the scan says
            // otherwise, and the rover stopping to do it must not quietly repaint that
            // orange back to idle halfway through.
            if (_held)
                return;

            _target = moving ? movingColour : idleColour;
        }

        private void OnBlocked()
        {
            Pulse(blockedColour);
        }

        /// <summary>
        /// The visual half of a junction ping. Called at the same instant as the tone,
        /// never instead of it and never after it.
        /// </summary>
        public void SignalJunction()
        {
            Pulse(heardColour);
        }

        // ------------------------------------------------------------------
        // The light language
        //
        // The rover has no face and does not speak. Everything it can express, it
        // expresses here, so the vocabulary is deliberately small and each entry means
        // exactly one thing:
        //
        //   green, twice     understood you
        //   red, once        did not understand you
        //   red, held        stopping, or stopped against something
        //   orange, held     working, do not expect a response yet
        //   green, held      section clear
        //   red, slow pulse  found the fault
        //
        // Held states set the resting colour so they persist. Pulses are momentary and
        // fall back to whatever the rover is doing. Nothing in this list is the only
        // channel for its meaning; every one of them has a caption, a sound, or both.
        // ------------------------------------------------------------------

        [Header("Scanning")]
        [SerializeField] private Color scanningColour = new Color(1f, 0.62f, 0.18f);
        [SerializeField] private Color clearColour = new Color(0.4f, 0.95f, 0.6f);
        [SerializeField] private Color faultColour = new Color(1f, 0.3f, 0.28f);
        [SerializeField] private Color brakingColour = new Color(1f, 0.28f, 0.25f);
        [SerializeField] private Color remoteColour = new Color(0.75f, 0.55f, 1f);

        private bool _held;
        private bool _releaseOnMove;
        private bool _brakingShown;

        /// <summary>Understood. Two green blinks, because one is indistinguishable from a flicker.</summary>
        public void SignalUnderstood()
        {
            StartCoroutine(DoubleBlink(heardColour));
        }

        /// <summary>Did not understand. One red, deliberately unlike the double green.</summary>
        public void SignalNotUnderstood()
        {
            Pulse(blockedColour);
        }

        /// <summary>Braking. Held red for as long as the rover is shedding speed.</summary>
        public void SignalBraking(bool braking)
        {
            if (braking)
            {
                _held = true;
                _target = brakingColour;
                _light.color = brakingColour;
                return;
            }

            if (_held && _target == brakingColour)
                Release();
        }

        /// <summary>Working on a section. Held orange: nothing you say lands until this ends.</summary>
        public void SignalScanning()
        {
            _held = true;
            _target = scanningColour;
            _light.color = scanningColour;
        }

        /// <summary>Section done. Green for clear, red for the break in the line.</summary>
        public void SignalScanComplete(bool fault)
        {
            _held = true;
            _releaseOnMove = true;
            _target = fault ? faultColour : clearColour;
            _light.color = _target;
        }

        /// <summary>
        /// Control is driving. A slow deliberate pulse in a colour the rover never shows
        /// on its own, so the one moment the machine moves without being spoken to is also
        /// the one moment it does not look like itself.
        /// </summary>
        public void SignalRemoteControl()
        {
            _held = true;
            _releaseOnMove = false;
            _target = remoteColour;
            _light.color = remoteColour;
        }

        /// <summary>Back to reporting whatever the rover is actually doing.</summary>
        public void Release()
        {
            _held = false;
            _releaseOnMove = false;
            _target = rover != null && rover.IsMoving ? movingColour : idleColour;
        }

        private System.Collections.IEnumerator DoubleBlink(Color colour)
        {
            for (var i = 0; i < 2; i++)
            {
                Pulse(colour);
                yield return new WaitForSecondsRealtime(0.12f);
                _light.color = Color.black;
                yield return new WaitForSecondsRealtime(0.08f);
            }

            Pulse(colour);
        }

        private void Pulse(Color colour)
        {
            _light.color = colour;
            _pulseUntil = Time.time + pulseSeconds * Mathf.Max(0.25f, GameSettings.GameSpeed);
        }

        private void Update()
        {
            // Polled rather than pushed. Braking is a continuous condition, not an event,
            // and deriving it from the rover's own state every frame means the brake light
            // cannot get stuck on after a reset or a scene change the way a subscribed
            // flag can.
            if (rover != null)
            {
                var braking = rover.IsBraking;
                if (braking != _brakingShown)
                {
                    _brakingShown = braking;
                    SignalBraking(braking);
                }
            }

            if (Time.time >= _pulseUntil)
                _light.color = Color.Lerp(_light.color, _target, Time.deltaTime * fadeSpeed);

            var wanted = _on ? onIntensity : offIntensity;
            _light.intensity = Mathf.Lerp(_light.intensity, wanted, Time.deltaTime * fadeSpeed);

            // The shell carries the same signal as the lamp. Someone looking at the
            // rover and someone looking at the pool of light it casts get told the
            // same thing at the same moment.
            if (_shellMaterial != null)
            {
                var dim = _on ? 1f : 0.25f;
                _shellMaterial.color = _light.color * dim;
            }
        }
    }
}
