using Dikdik.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace Dikdik.Game
{
    /// <summary>
    /// The console. Shows the player their own words back, always, and shows honestly
    /// where their command has got to.
    ///
    /// This is not a debug overlay that survived to release. It is the game's answer to
    /// the worst thing a voice interface does, which is go quiet. Silence after you
    /// speak is indistinguishable from being ignored, and a player cannot tell a failed
    /// microphone from a failed sentence from a game that does not care.
    ///
    /// <para>With the transport delay in place it has a second job. There are now three
    /// distinct states and collapsing them would be a lie:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Received.</b> We have your words. Shown instantly.</item>
    /// <item><b>In transit.</b> Crossing the gap. Shown as distance, not as a spinner.</item>
    /// <item><b>Acted on.</b> The rover moved.</item>
    /// </list>
    ///
    /// <para>A spinner would say the software is struggling. A travelling signal says
    /// the Moon is a long way away. The wait is identical and only one of them is true.</para>
    /// </summary>
    public class CommsDisplay : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Text heardText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image background;

        [Tooltip("Fills as the command crosses the gap. Image type must be Filled.")]
        [SerializeField] private Image signalBar;

        [Header("Timing")]
        [Tooltip("How long an acted-on message stays before returning to rest")]
        [SerializeField] private float holdSeconds = 3f;

        [Header("Base sizes, before the player's text scale")]
        [SerializeField] private int baseHeardSize = 28;
        [SerializeField] private int baseStatusSize = 18;

        [Header("Colours")]
        [SerializeField] private Color transitColour = new Color(0.55f, 0.75f, 1f);
        [SerializeField] private Color actedColour = new Color(0.55f, 1f, 0.7f);
        [SerializeField] private Color missedColour = new Color(1f, 0.65f, 0.5f);

        private float _clearAt;
        private bool _listening;

        private void OnEnable()
        {
            var bus = CommandBus.Instance;
            if (bus != null)
            {
                bus.CommandAccepted += OnAccepted;
                bus.CommandIssued += OnIssued;
                bus.CommandNotUnderstood += OnNotUnderstood;
            }

            GameSettings.Changed += ApplySettings;
            ApplySettings();
            ShowResting();
        }

        private void OnDisable()
        {
            var bus = CommandBus.Instance;
            if (bus != null)
            {
                bus.CommandAccepted -= OnAccepted;
                bus.CommandIssued -= OnIssued;
                bus.CommandNotUnderstood -= OnNotUnderstood;
            }

            GameSettings.Changed -= ApplySettings;
        }

        /// <summary>
        /// Salty asking something, rather than us reporting on Salty.
        ///
        /// Held on screen with no timeout, because it is a question and questions wait.
        /// The next command the player gives will replace it.
        /// </summary>
        public void ShowRoverQuestion(string question)
        {
            if (heardText != null)
                heardText.text = question;

            if (statusText != null)
            {
                statusText.text = GameSettings.Subtitles ? "Salty is waiting." : string.Empty;
                statusText.color = GameSettings.HighContrast ? Color.white : transitColour;
            }

            _clearAt = 0f;
        }

        /// <summary>Called by the voice producer when speech starts and stops.</summary>
        public void SetListening(bool listening)
        {
            _listening = listening;
            if (Time.time >= _clearAt)
                ShowResting();
        }

        /// <summary>
        /// We have the words. The rover has not moved and will not for a couple of
        /// seconds, and saying otherwise here would be the interface taking credit for
        /// something that has not happened.
        /// </summary>
        private void OnAccepted(Intent intent)
        {
            Show(Describe(intent), "Sending.", transitColour);
            _clearAt = 0f;   // held open until it lands
        }

        private void OnIssued(Intent intent)
        {
            Show(Describe(intent), $"Salty: {Readable(intent.Id)}", actedColour);
            _clearAt = Time.time + holdSeconds;
        }

        private void OnNotUnderstood(Intent intent)
        {
            // Never blame the player. "I did not understand" and not "invalid command".
            var heard = string.IsNullOrWhiteSpace(intent.RawText)
                ? "I did not catch that."
                : Describe(intent);

            Show(heard, "I did not understand that one. Try saying it another way.", missedColour);
            _clearAt = Time.time + holdSeconds;
        }

        private static string Describe(Intent intent)
        {
            return intent.Source == CommandSource.Keyboard
                ? $"You pressed: {intent.RawText}"
                : $"I heard: {intent.RawText}";
        }

        private void Show(string heard, string status, Color tint)
        {
            if (heardText != null)
                heardText.text = heard;

            if (statusText != null)
            {
                statusText.text = GameSettings.Subtitles ? status : string.Empty;
                statusText.color = GameSettings.HighContrast ? Color.white : tint;
            }
        }

        private void ShowResting()
        {
            if (heardText != null)
                heardText.text = _listening ? "Listening." : "Say something to the rover.";

            if (statusText != null)
                statusText.text = string.Empty;
        }

        private void Update()
        {
            UpdateSignalBar();

            if (_clearAt > 0f && Time.time >= _clearAt)
            {
                _clearAt = 0f;
                ShowResting();
            }
        }

        private void UpdateSignalBar()
        {
            if (signalBar == null)
                return;

            var bus = CommandBus.Instance;
            var inTransit = bus != null && bus.InTransitCount > 0;

            signalBar.enabled = inTransit;

            if (!inTransit)
                return;

            signalBar.fillAmount = bus.TransitProgress;
            signalBar.color = GameSettings.HighContrast ? Color.white : transitColour;
        }

        private void ApplySettings()
        {
            var scale = GameSettings.TextScale;

            if (heardText != null)
            {
                heardText.fontSize = Mathf.RoundToInt(baseHeardSize * scale);
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(0.92f, 0.94f, 0.96f);
            }

            if (statusText != null)
                statusText.fontSize = Mathf.RoundToInt(baseStatusSize * scale);

            if (background != null)
            {
                // Solid black behind text in high contrast. A translucent panel over a
                // busy scene is where contrast ratios quietly go to die.
                background.color = GameSettings.HighContrast
                    ? Color.black
                    : new Color(0f, 0f, 0f, 0.65f);
            }
        }

        /// <summary>Turn an enum name into something a person would say.</summary>
        private static string Readable(IntentId id)
        {
            switch (id)
            {
                case IntentId.Go: return "moving";
                case IntentId.Stop: return "holding";
                case IntentId.Left: return "turning left";
                case IntentId.Right: return "turning right";
                case IntentId.Back: return "backing up";
                case IntentId.Open: return "opening";
                case IntentId.Light: return "lamp";
                case IntentId.Wake: return "transmitting";
                case IntentId.Repeat: return "repeating";
                case IntentId.Restart: return "resetting";
                case IntentId.Help: return "help";
                default: return id.ToString().ToLowerInvariant();
            }
        }
    }
}
