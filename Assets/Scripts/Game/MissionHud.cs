using Dikdik.Commands;
using Dikdik.Game.Cable;
using UnityEngine;
using UnityEngine.UI;

namespace Dikdik.Game
{
    /// <summary>
    /// The mission, on screen, all the time.
    ///
    /// <para>The playtest note this answers was blunt: there was no stated mission and no
    /// sense of progress. A player could drive well for five minutes without knowing
    /// whether they were nearly finished or had not started. Four numbers fix that, and
    /// this shows exactly those four and nothing else.</para>
    ///
    /// <list type="bullet">
    /// <item><b>Sector.</b> Which of the six, so the game has a shape.</item>
    /// <item><b>Sections scanned.</b> The work done, out of the work there is.</item>
    /// <item><b>Distance remaining.</b> Metres of cable left to check.</item>
    /// <item><b>Speed.</b> A bar, because a rover that is coasting to a stop after "stop"
    /// looks identical to one ignoring you unless something says otherwise.</item>
    /// </list>
    ///
    /// <para>Plus two states that only appear when they are true: idling, and off the
    /// cable. Both are things the player would otherwise have to infer from an absence,
    /// and inferring from an absence is exactly how a voice interface loses somebody.</para>
    ///
    /// <para>Everything here is text or a solid bar. No icons without labels, no colour
    /// carrying meaning on its own, and the whole panel scales with the player's text size
    /// setting. A HUD that explains the game and is itself unreadable is a joke at the
    /// player's expense.</para>
    /// </summary>
    public class MissionHud : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private MissionProgress progress;
        [SerializeField] private RoverController rover;
        [SerializeField] private LevelDirector director;

        [Header("Wiring")]
        [SerializeField] private Text sectorText;
        [SerializeField] private Text scannedText;
        [SerializeField] private Text distanceText;
        [SerializeField] private Text stateText;
        [SerializeField] private Image speedBar;
        [SerializeField] private Image background;

        [Header("Base sizes, before the player's text scale")]
        [SerializeField] private int baseHeadingSize = 20;
        [SerializeField] private int baseBodySize = 16;

        [Header("Speed bar")]
        [Tooltip("Speed that fills the bar completely. The rover's own move speed, so a " +
                 "full bar means flat out rather than an arbitrary fraction.")]
        [SerializeField] private float fullScaleSpeed = 6f;

        [Header("Colours")]
        [SerializeField] private Color normalTint = new Color(0.72f, 0.82f, 0.95f);
        [SerializeField] private Color warnTint = new Color(1f, 0.78f, 0.4f);
        [SerializeField] private Color brakeTint = new Color(1f, 0.45f, 0.4f);

        [Tooltip("How far off the cable counts as wandering rather than driving round a rock.")]
        [SerializeField] private float offCableWarnDistance = 9f;

        [Tooltip("Silence this long reads as idle. Matches the first idle voice reminder, " +
                 "so the panel and the supervisor agree about when you stopped playing.")]
        [SerializeField] private float idleSeconds = 25f;

        private float _lastCommandAt;

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandAccepted += OnCommandAccepted;

            if (progress != null)
                progress.Changed += Refresh;

            GameSettings.Changed += ApplySettings;
            _lastCommandAt = Time.unscaledTime;

            ApplySettings();
            Refresh();
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.CommandAccepted -= OnCommandAccepted;

            if (progress != null)
                progress.Changed -= Refresh;

            GameSettings.Changed -= ApplySettings;
        }

        private void OnCommandAccepted(Intent intent)
        {
            _lastCommandAt = Time.unscaledTime;
        }

        private void Refresh()
        {
            if (sectorText != null && director != null)
                sectorText.text = $"SECTOR {director.LevelNumber} / 6";

            if (scannedText != null && progress != null)
                scannedText.text = progress.TotalCheckpoints > 0
                    ? $"Sections scanned   {progress.ScannedCheckpoints} / {progress.TotalCheckpoints}"
                    : "Sections scanned   —";
        }

        private void Update()
        {
            if (GamePause.IsPaused)
                return;

            UpdateDistance();
            UpdateSpeedBar();
            UpdateState();
        }

        private void UpdateDistance()
        {
            if (distanceText == null || progress == null)
                return;

            var remaining = progress.DistanceRemaining();

            // Rounded to whole metres. A distance that flickers through three decimals as
            // you drive is noise pretending to be precision.
            distanceText.text = remaining > 0.5f
                ? $"Cable remaining    {Mathf.RoundToInt(remaining)} m"
                : "Cable remaining    none";
        }

        private void UpdateSpeedBar()
        {
            if (speedBar == null || rover == null)
                return;

            var speed = Mathf.Abs(rover.CurrentSpeed);
            var fraction = Mathf.Clamp01(speed / Mathf.Max(0.01f, fullScaleSpeed));

            // The right anchor, not fillAmount. See the comment in CableBuilder: a Filled
            // image with no sprite ignores fillAmount entirely and draws itself full.
            var rect = speedBar.rectTransform;
            rect.anchorMax = new Vector2(fraction, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Red while shedding speed it was told to lose. On the slope level this is the
            // difference between "it is ignoring me" and "it is stopping, give it a moment".
            speedBar.color = GameSettings.HighContrast
                ? Color.white
                : rover.IsBraking ? brakeTint : normalTint;
        }

        /// <summary>
        /// The one line that changes. Only ever shows a state the player would otherwise
        /// have to guess at, and stays blank when everything is ordinary.
        /// </summary>
        private void UpdateState()
        {
            if (stateText == null)
                return;

            string message = null;
            var tint = normalTint;

            if (rover != null && rover.IsRemoteControlled)
            {
                message = "Control is driving.";
                tint = warnTint;
            }
            else if (rover != null && rover.IsScanHeld)
            {
                message = rover.HeldCommand.HasValue
                    ? $"Scanning.  Waiting: {Readable(rover.HeldCommand.Value.Id)}"
                    : "Scanning.";
                tint = warnTint;
            }
            else if (progress != null && progress.OffCableDistance() > offCableWarnDistance)
            {
                message = $"Off the line by {Mathf.RoundToInt(progress.OffCableDistance())} m.";
                tint = warnTint;
            }
            else if (Time.unscaledTime - _lastCommandAt > idleSeconds)
            {
                message = "Idle.  Salty is waiting for you.";
                tint = normalTint;
            }

            stateText.text = message ?? string.Empty;
            stateText.color = GameSettings.HighContrast ? Color.white : tint;
        }

        private static string Readable(IntentId id)
        {
            switch (id)
            {
                case IntentId.Go: return "go";
                case IntentId.Stop: return "stop";
                case IntentId.Left: return "left";
                case IntentId.Right: return "right";
                case IntentId.Back: return "back";
                default: return id.ToString().ToLowerInvariant();
            }
        }

        private void ApplySettings()
        {
            var scale = GameSettings.TextScale;
            var body = GameSettings.HighContrast ? Color.white : normalTint;

            if (sectorText != null)
            {
                sectorText.fontSize = Mathf.RoundToInt(baseHeadingSize * scale);
                sectorText.color = GameSettings.HighContrast ? Color.white : new Color(0.92f, 0.94f, 0.98f);
            }

            foreach (var text in new[] { scannedText, distanceText, stateText })
            {
                if (text == null)
                    continue;

                text.fontSize = Mathf.RoundToInt(baseBodySize * scale);
                text.color = body;
            }

            if (background != null)
                background.color = GameSettings.HighContrast
                    ? Color.black
                    : new Color(0f, 0f, 0f, 0.55f);

            Refresh();
        }
    }
}
