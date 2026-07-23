using Dikdik.Commands;
using UnityEngine;
using UnityEngine.UI;

namespace Dikdik.Game
{
    /// <summary>
    /// Shows the player their own words back, always, whether we understood them or not.
    ///
    /// This panel is not a debug overlay that survived to release. It is the game's
    /// answer to the worst thing a voice interface does, which is go quiet. Silence
    /// after you speak is indistinguishable from being ignored, and a player cannot
    /// tell a failed microphone from a failed sentence from a game that does not care.
    ///
    /// So: we print what we heard, we say plainly when we did not understand, and the
    /// keyboard gets the same treatment so neither way of playing feels less answered.
    /// </summary>
    public class HeardDisplay : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Text heardText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image background;

        [Header("Timing")]
        [Tooltip("Seconds a message stays before fading back to the resting state")]
        [SerializeField] private float holdSeconds = 4f;

        [Header("Base sizes, before the player's text scale is applied")]
        [SerializeField] private int baseHeardSize = 28;
        [SerializeField] private int baseStatusSize = 18;

        private float _clearAt;
        private bool _listening;

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandIssued += OnUnderstood;
                CommandBus.Instance.CommandNotUnderstood += OnNotUnderstood;
            }

            GameSettings.Changed += ApplySettings;
            ApplySettings();
            ShowResting();
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
            {
                CommandBus.Instance.CommandIssued -= OnUnderstood;
                CommandBus.Instance.CommandNotUnderstood -= OnNotUnderstood;
            }

            GameSettings.Changed -= ApplySettings;
        }

        /// <summary>Called by the voice producer whenever speech starts or stops.</summary>
        public void SetListening(bool listening)
        {
            _listening = listening;
            if (Time.time >= _clearAt)
                ShowResting();
        }

        private void OnUnderstood(Intent intent)
        {
            Show(Describe(intent), $"Understood: {Readable(intent.Id)}");
        }

        private void OnNotUnderstood(Intent intent)
        {
            // Never blame the player. "I did not understand" and not "invalid command".
            var heard = string.IsNullOrWhiteSpace(intent.RawText)
                ? "I did not catch that."
                : Describe(intent);

            Show(heard, "I did not understand that one. Try saying it another way.");
        }

        private static string Describe(Intent intent)
        {
            return intent.Source == CommandSource.Keyboard
                ? $"You pressed: {intent.RawText}"
                : $"I heard: {intent.RawText}";
        }

        private void Show(string heard, string status)
        {
            if (heardText != null)
                heardText.text = heard;

            if (statusText != null)
                statusText.text = GameSettings.Subtitles ? status : string.Empty;

            _clearAt = Time.time + holdSeconds;
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
            if (_clearAt > 0f && Time.time >= _clearAt)
            {
                _clearAt = 0f;
                ShowResting();
            }
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
            {
                statusText.fontSize = Mathf.RoundToInt(baseStatusSize * scale);
                statusText.color = GameSettings.HighContrast ? Color.white : new Color(0.72f, 0.76f, 0.80f);
            }

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
                case IntentId.Go: return "go";
                case IntentId.Stop: return "stop";
                case IntentId.Left: return "turn left";
                case IntentId.Right: return "turn right";
                case IntentId.Back: return "back up";
                case IntentId.Open: return "open";
                case IntentId.Light: return "light";
                case IntentId.Wake: return "wake";
                case IntentId.Repeat: return "repeat";
                case IntentId.Help: return "help";
                default: return id.ToString().ToLowerInvariant();
            }
        }
    }
}
