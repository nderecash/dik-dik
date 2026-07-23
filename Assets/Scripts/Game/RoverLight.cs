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

        private Light _light;
        private bool _on = true;
        private Color _target;
        private float _pulseUntil;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _target = idleColour;
            _light.color = idleColour;
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
            Pulse(heardColour);
        }

        private void OnNotUnderstood(Intent intent)
        {
            // Being misheard also gets a signal. Silence is the one response that
            // feels like being ignored, and this game is about not being ignored.
            Pulse(blockedColour);
        }

        private void OnMovingChanged(bool moving)
        {
            _target = moving ? movingColour : idleColour;
        }

        private void OnBlocked()
        {
            Pulse(blockedColour);
        }

        private void Pulse(Color colour)
        {
            _light.color = colour;
            _pulseUntil = Time.time + pulseSeconds * Mathf.Max(0.25f, GameSettings.GameSpeed);
        }

        private void Update()
        {
            if (Time.time >= _pulseUntil)
                _light.color = Color.Lerp(_light.color, _target, Time.deltaTime * fadeSpeed);

            var wanted = _on ? onIntensity : offIntensity;
            _light.intensity = Mathf.Lerp(_light.intensity, wanted, Time.deltaTime * fadeSpeed);
        }
    }
}
