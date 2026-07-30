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
    /// <para>A spinner would say the software is struggling. A travelling signal says the
    /// rover is a long way away. The wait is identical and only one of them is true.</para>
    /// </summary>
    public class CommsDisplay : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Text heardText;
        [SerializeField] private Text statusText;
        [SerializeField] private Image background;

        [Header("Speaker badge")]
        [Tooltip("Shown only when someone is speaking TO the player: Control, or Salty " +
                 "asking a question. Hidden when the panel is just echoing the player's " +
                 "own words, so a supervisor line never reads as the game awaiting input.")]
        [SerializeField] private GameObject speakerBadge;

        [SerializeField] private Text speakerName;
        [SerializeField] private Image speakerIconHead;
        [SerializeField] private Image speakerIconBody;

        [Tooltip("Grows left to right as the command crosses the gap. Driven by its right " +
                 "anchor, not by fillAmount. See UpdateSignalBar.")]
        [SerializeField] private Image signalBar;

        [Tooltip("The empty track behind the signal bar, so there is visibly somewhere left to go.")]
        [SerializeField] private Image signalTrack;

        [Header("Microphone state")]
        [Tooltip("Its own row, separate from the caption. These used to share one field, " +
                 "which meant the mic state overwrote Control's subtitles during the " +
                 "briefing and the player never saw a word of it.")]
        [SerializeField] private Text micStateText;

        [SerializeField] private Image micDot;

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
                bus.CommandReplaced += OnReplaced;
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
                bus.CommandReplaced -= OnReplaced;
            }

            GameSettings.Changed -= ApplySettings;
        }

        /// <summary>
        /// The supervisor speaking. Their line, held for a few seconds, styled apart from
        /// the rover's own feedback so it reads as a second person on the loop.
        ///
        /// Always shown, whatever the audio does, because the recorded voice is radio-
        /// degraded on purpose and the words live here. This is "subtitles for all speech"
        /// meant literally.
        /// </summary>
        private static readonly Color SupervisorTint = new Color(0.62f, 0.78f, 1f);
        private static readonly Color RoverTint = new Color(0.6f, 1f, 0.75f);

        public void ShowSupervisorLine(string line, bool briefing = false)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            ShowSpeaker("CONTROL", SupervisorTint);

            if (heardText != null)
            {
                heardText.text = line;
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(0.85f, 0.9f, 1f);
            }

            if (statusText != null)
            {
                // During the briefing, tell the player it is a briefing and that they can
                // skip it, rather than leaving them wondering whether to start talking.
                statusText.text = briefing ? "Briefing.  Press Space to skip." : string.Empty;
                statusText.color = GameSettings.HighContrast ? Color.white : SupervisorTint;
            }

            // A briefing line holds until the next one replaces it; the sequence drives
            // the timing. A one-off line clears on its own reading clock.
            _clearAt = briefing ? 0f : Time.time + ReadingTime(line);
        }

        private static readonly Color BroadcastTint = new Color(1f, 0.92f, 0.6f);
        private static readonly Color StationTint = new Color(0.7f, 0.85f, 0.7f);

        /// <summary>
        /// The station's automated system talking, in jargon. Labelled STATION and tinted
        /// apart from the human supervisor, because the contrast between the two is the
        /// whole point of the level: the machine talks like this, the person talks plainly.
        /// </summary>
        public void ShowStationLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            ShowSpeaker("STATION", StationTint);

            if (heardText != null)
            {
                heardText.text = line;
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(0.8f, 0.9f, 0.8f);
            }

            if (statusText != null)
                statusText.text = GameSettings.Subtitles ? "Automated. Wait for the translation." : string.Empty;

            _clearAt = 0f;
        }

        /// <summary>
        /// The player's own words going out on the open loop, at the end.
        ///
        /// Labelled OPEN LOOP rather than CONTROL or SALTY, because this is neither of
        /// them speaking. It is the player, played back. Held until the next line
        /// replaces it; the broadcast drives the timing.
        /// </summary>
        public void ShowBroadcastLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            ShowSpeaker("OPEN LOOP", BroadcastTint);

            if (heardText != null)
            {
                heardText.text = line;
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(1f, 0.95f, 0.8f);
            }

            if (statusText != null)
                statusText.text = string.Empty;

            _clearAt = 0f;
        }

        /// <summary>Hide the speaker badge and return to rest. Called when the briefing ends.</summary>
        public void ClearSupervisor()
        {
            _clearAt = 0f;
            ShowResting();
        }

        private bool _speakerShowing;
        private bool _messageHeld;

        private void ShowSpeaker(string who, Color tint)
        {
            _speakerShowing = true;
            _messageHeld = true;

            if (speakerBadge != null)
                speakerBadge.SetActive(true);

            if (speakerName != null)
            {
                speakerName.text = who;
                speakerName.color = GameSettings.HighContrast ? Color.white : tint;
            }

            var iconColour = GameSettings.HighContrast ? Color.white : tint;
            if (speakerIconHead != null) speakerIconHead.color = iconColour;
            if (speakerIconBody != null) speakerIconBody.color = iconColour;
        }

        private void HideSpeaker()
        {
            _speakerShowing = false;

            if (speakerBadge != null)
                speakerBadge.SetActive(false);
        }

        private static float ReadingTime(string line)
        {
            var words = line.Split(' ').Length;
            return Mathf.Clamp(1.5f + words * 0.35f, 2.5f, 8f);
        }

        /// <summary>
        /// Salty asking something, rather than us reporting on Salty.
        ///
        /// Held on screen with no timeout, because it is a question and questions wait.
        /// The next command the player gives will replace it.
        /// </summary>
        public void ShowRoverQuestion(string question)
        {
            ShowSpeaker("SALTY", RoverTint);

            if (heardText != null)
            {
                heardText.text = question;
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(0.85f, 1f, 0.9f);
            }

            if (statusText != null)
            {
                statusText.text = GameSettings.Subtitles ? "Salty is waiting." : string.Empty;
                statusText.color = GameSettings.HighContrast ? Color.white : RoverTint;
            }

            _clearAt = 0f;
        }

        private static readonly Color PromptTint = new Color(1f, 0.85f, 0.45f);

        /// <summary>
        /// The game asking the player for something and then waiting: end of a sector,
        /// end of the mission. No timeout, because there is nothing to time out to. The
        /// player decides when this moves on, which is the same contract as the rest of
        /// the game.
        /// </summary>
        public void ShowPrompt(string headline, string instruction)
        {
            ShowSpeaker("CONTROL", PromptTint);

            if (heardText != null)
            {
                heardText.text = headline;
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(1f, 0.95f, 0.85f);
            }

            if (statusText != null)
            {
                // Not gated on the subtitles setting. This one is an instruction, not a
                // transcript, and a player who turned subtitles off has not asked to be
                // left on a screen with no way off it.
                statusText.text = instruction;
                statusText.color = GameSettings.HighContrast ? Color.white : PromptTint;
            }

            _clearAt = 0f;
        }

        /// <summary>Take the prompt down once the player has answered it.</summary>
        public void ClearPrompt()
        {
            _clearAt = 0f;
            ShowResting();
        }

        /// <summary>Called by the voice producer when speech starts and stops.</summary>
        public void SetListening(bool listening)
        {
            _listening = listening;

            // Never wipe a line someone is speaking. The microphone's voice detection
            // fires constantly, including on Control's own voice through the speakers,
            // and it used to overwrite the subtitle with "Listening" before the player
            // could read it, which read as a prompt to talk over the briefing. While a
            // speaker badge is up, or a message is being held, the caption stays.
            if (_speakerShowing || _clearAt == 0f && _messageHeld)
                return;

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
            var status = _replaced == null
                ? "Sending."
                : $"Sending.  Replaces: {_replaced}";

            Show(Describe(intent), status, transitColour);
            _clearAt = 0f;   // held open until it lands
        }

        private void OnIssued(Intent intent)
        {
            Show(Describe(intent), $"Salty: {Readable(intent.Id)}", actedColour);
            _clearAt = Time.time + holdSeconds;
            _replaced = null;
        }

        private string _replaced;

        /// <summary>
        /// A command was overtaken by a newer one before it arrived.
        ///
        /// Named out loud rather than letting it disappear. The player chose to talk over
        /// themselves, so this is not an error, but it is a thing that happened to an
        /// instruction they gave, and this game does not let those vanish quietly.
        /// </summary>
        private void OnReplaced(Intent dropped)
        {
            _replaced = Readable(dropped.Id);
        }

        /// <summary>
        /// Say what actually went wrong, and what the player can do about it.
        ///
        /// <para>This used to be one fixed apology for three different failures, which a
        /// playtester summed up better than I can: "no input is wasted but anticipated and
        /// guided, and the user is not just shouting into a void".</para>
        ///
        /// <para>The three are genuinely different and only one of them is a mishearing.
        /// The worst case was the third: in sector 5 they said "open" against a wall, were
        /// told the game did not understand, and quit to the menu. The game understood
        /// perfectly. That level just does not allow it.</para>
        /// </summary>
        private void OnNotUnderstood(Intent intent)
        {
            var bus = CommandBus.Instance;
            var blocked = bus != null ? bus.LastBlockedIntent : IntentId.None;
            bus?.ClearLastBlocked();

            // Understood, but switched off here. Name the word, so the player learns that
            // it is a real word and this is the wrong place for it.
            if (blocked != IntentId.None)
            {
                Show(Describe(intent),
                     $"Salty knows \"{Readable(blocked)}\", but there is nothing to do that to here.",
                     missedColour);

                _clearAt = Time.time + holdSeconds;
                return;
            }

            // Nothing arrived at all. Point at the mic row rather than at the player.
            if (string.IsNullOrWhiteSpace(intent.RawText))
            {
                Show("Nothing came through.",
                     "Check the microphone line on the right. Not your fault.",
                     missedColour);

                _clearAt = Time.time + holdSeconds;
                return;
            }

            // Heard words, no match. This is the one where the player can actually fix it
            // permanently, and the way to do that has been in the settings screen the whole
            // time with nothing pointing at it.
            Show(Describe(intent),
                 "Not one of Salty's words yet.  Press ESC, Controls, to teach it.",
                 missedColour);

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
            // This is the console echoing the player's own words back, not a person
            // speaking to them, so no speaker badge.
            HideSpeaker();
            _messageHeld = true;

            if (heardText != null)
            {
                heardText.text = heard;
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(0.92f, 0.94f, 0.96f);
            }

            if (statusText != null)
            {
                statusText.text = GameSettings.Subtitles ? status : string.Empty;
                statusText.color = GameSettings.HighContrast ? Color.white : tint;
            }
        }

        private void ShowResting()
        {
            HideSpeaker();
            _messageHeld = false;

            if (heardText != null)
            {
                // Blank, not "Listening."
                //
                // This field used to carry the microphone state as well as the captions,
                // and the two fought. During the briefing the mic state kept winning, so
                // the player was told to say something while Control was mid-sentence and
                // never saw a single subtitle. Mic state now lives on its own row and this
                // one shows captions or nothing.
                heardText.text = string.Empty;
                heardText.color = GameSettings.HighContrast ? Color.white : new Color(0.92f, 0.94f, 0.96f);
            }

            if (statusText != null)
                statusText.text = string.Empty;
        }

        /// <summary>
        /// The one place that says whether the microphone is open, and why not when it is
        /// closed.
        ///
        /// <para>Polled from the real state every frame rather than pushed from wherever
        /// somebody remembered to push it. A microphone indicator that can be wrong is
        /// worse than none, because the player will believe it and then blame themselves.</para>
        ///
        /// <para>Order matters: the most specific reason wins. "Mic off" because the player
        /// turned it off outranks "Control is speaking", which outranks "listening".</para>
        /// </summary>
        private void UpdateMicState()
        {
            if (micStateText == null)
                return;

            string label;
            Color tint;

            if (!GameSettings.VoiceEnabled)
            {
                label = "Mic off.  Keyboard only.";
                tint = new Color(0.55f, 0.57f, 0.62f);
            }
            else if (Bootstrap.Instance != null && !Bootstrap.Instance.VoiceAvailable)
            {
                label = "Mic unavailable.  Keyboard works.";
                tint = new Color(0.55f, 0.57f, 0.62f);
            }
            else if (Voice.VoiceArbiter.IsListeningBlocked)
            {
                label = "Mic closed while Control speaks.";
                tint = new Color(0.62f, 0.78f, 1f);
            }
            else if (Producers.VoiceCommandProducer.IsTranscribing)
            {
                // Not "Listening", which is what it used to fall back to here and is not
                // true: the microphone has closed and Whisper is running. Waiting to be
                // heard and waiting to be understood are different things to be told.
                label = "Working out what you said.";
                tint = transitColour;
            }
            else if (CommandBus.Instance != null && CommandBus.Instance.InTransitCount > 0)
            {
                label = "Sending.";
                tint = transitColour;
            }
            else if (_listening)
            {
                label = "Hearing you.";
                tint = new Color(0.5f, 1f, 0.65f);
            }
            else
            {
                label = "Listening.";
                tint = new Color(0.4f, 0.85f, 0.55f);
            }

            micStateText.text = label;
            micStateText.color = GameSettings.HighContrast ? Color.white : tint;

            // The dot is the half you can read without reading. Colour alone carries no
            // meaning here: the words are always next to it.
            if (micDot != null)
                micDot.color = GameSettings.HighContrast ? Color.white : tint;
        }

        private void Update()
        {
            UpdateSignalBar();
            UpdateMicState();

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

            if (signalTrack != null)
                signalTrack.enabled = inTransit;

            if (!inTransit)
                return;

            // The right anchor, not fillAmount. See the comment in BootSceneBuilder: a
            // Filled image with no sprite ignores fillAmount and draws itself full, so
            // this bar spent its whole life at 100%.
            var rect = signalBar.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(bus.TransitProgress), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

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
