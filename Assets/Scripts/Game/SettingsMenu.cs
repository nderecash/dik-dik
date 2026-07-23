using System.Collections.Generic;
using Dikdik.Commands;
using Dikdik.Producers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dikdik.Game
{
    /// <summary>
    /// The console settings panel. Escape opens it, anywhere, always.
    ///
    /// <para><b>Nothing here is ever unlocked.</b> Every option is present and usable from
    /// the first launch. A level may show you why an option exists; none may make you
    /// earn one. Treating access as a reward inverts the argument of the whole game, and
    /// it is an easy mistake to make by accident, so it is written here as well as in the
    /// design notes.</para>
    ///
    /// <para>Drawn with OnGUI on purpose, and it is not laziness. It gives direct control
    /// over every font size and colour, which is what makes the text-scale and
    /// high-contrast settings actually apply to the settings screen itself. A settings
    /// panel that does not obey its own accessibility options is a joke at the player's
    /// expense. It is also completely reliable to generate in batch mode, and a
    /// utilitarian readout is arguably what a mission control console should look like.</para>
    /// </summary>
    public class SettingsMenu : MonoBehaviour
    {
        private enum Tab { Play, Display, Controls }

        [SerializeField] private KeyCode openKey = KeyCode.Escape;

        private bool _open;
        private Tab _tab = Tab.Play;
        private IntentId _rebinding = IntentId.None;
        private Vector2 _scroll;

        private KeyboardCommandProducer _keyboard;

        /// <summary>True while the panel is up. Gameplay can pause on this.</summary>
        public bool IsOpen => _open;

        private void Update()
        {
            if (Input.GetKeyDown(openKey))
            {
                // Escape cancels a rebind first, then closes. Otherwise someone stuck
                // in "press a key" has no way out except pressing a key.
                if (_rebinding != IntentId.None)
                {
                    _rebinding = IntentId.None;
                }
                else
                {
                    _open = !_open;

                    // Commit to disk when the panel closes rather than on every slider
                    // frame.
                    if (!_open)
                        GameSettings.Flush();
                }
            }

            if (_rebinding != IntentId.None)
                CaptureRebind();
        }

        private void CaptureRebind()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            foreach (var control in keyboard.allKeys)
            {
                if (!control.wasPressedThisFrame)
                    continue;

                if (control.keyCode == Key.Escape)
                {
                    _rebinding = IntentId.None;
                    return;
                }

                Producer?.Rebind(_rebinding, control.keyCode);
                _rebinding = IntentId.None;
                return;
            }
        }

        private KeyboardCommandProducer Producer =>
            _keyboard != null ? _keyboard : _keyboard = FindAnyObjectByType<KeyboardCommandProducer>();

        private void OnGUI()
        {
            if (!_open)
            {
                DrawHint();
                return;
            }

            var scale = GameSettings.TextScale;
            var contrast = GameSettings.HighContrast;

            var body = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(17 * scale),
                wordWrap = true,
                normal = { textColor = contrast ? Color.white : new Color(0.88f, 0.9f, 0.93f) }
            };

            var head = new GUIStyle(body) { fontSize = Mathf.RoundToInt(24 * scale) };
            var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(16 * scale) };
            var toggle = new GUIStyle(GUI.skin.toggle) { fontSize = Mathf.RoundToInt(17 * scale) };

            var width = Mathf.Min(760f, Screen.width - 80f);
            var height = Mathf.Min(620f, Screen.height - 80f);
            var area = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            // Solid, not translucent. A see-through panel over a busy scene is where
            // contrast ratios quietly go to die.
            GUI.color = contrast ? Color.black : new Color(0.03f, 0.04f, 0.06f, 0.97f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(area.x + 28, area.y + 24, area.width - 56, area.height - 48));

            GUILayout.Label("Console settings", head);
            GUILayout.Space(6);
            GUILayout.Label("Everything here is available from the first launch and stays available. " +
                            "Nothing in this game is unlocked by playing well.", body);
            GUILayout.Space(14);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play", button)) _tab = Tab.Play;
            if (GUILayout.Button("Display", button)) _tab = Tab.Display;
            if (GUILayout.Button("Controls", button)) _tab = Tab.Controls;
            GUILayout.EndHorizontal();
            GUILayout.Space(14);

            _scroll = GUILayout.BeginScrollView(_scroll);

            switch (_tab)
            {
                case Tab.Play: DrawPlay(body, toggle); break;
                case Tab.Display: DrawDisplay(body, toggle); break;
                case Tab.Controls: DrawControls(body, button); break;
            }

            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset everything to defaults", button))
                GameSettings.ResetToDefaults();

            if (GUILayout.Button("Close", button))
            {
                _open = false;
                GameSettings.Flush();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawPlay(GUIStyle body, GUIStyle toggle)
        {
            GUILayout.Label($"Game speed: {GameSettings.GameSpeed:0.00}x", body);
            GameSettings.GameSpeed = GUILayout.HorizontalSlider(GameSettings.GameSpeed, 0.25f, 1.5f);
            GUILayout.Label("Slows everything, including the rover and the doors. " +
                            "The delay on your commands stays the same, so at slower speeds " +
                            "it costs you less ground.", body);
            GUILayout.Space(16);

            var voiceAvailable = Bootstrap.Instance == null || Bootstrap.Instance.VoiceAvailable;

            GUI.enabled = voiceAvailable;
            GameSettings.VoiceEnabled = GUILayout.Toggle(
                GameSettings.VoiceEnabled && voiceAvailable, " Listen to my voice", toggle);
            GUI.enabled = true;

            GUILayout.Label(voiceAvailable
                ? "Speech is recognised on this machine. Nothing is sent anywhere."
                : "Voice is not available in this build. Everything is playable on the keyboard, " +
                  "and the keyboard is not a lesser way to play.", body);
        }

        private void DrawDisplay(GUIStyle body, GUIStyle toggle)
        {
            GameSettings.HighContrast = GUILayout.Toggle(GameSettings.HighContrast, " High contrast", toggle);
            GUILayout.Label("Outlines hazards and edges, and makes every panel solid rather than " +
                            "see-through.", body);
            GUILayout.Space(16);

            GUILayout.Label($"Text size: {GameSettings.TextScale:0.0}x", body);
            GameSettings.TextScale = GUILayout.HorizontalSlider(GameSettings.TextScale, 1f, 2.5f);
            GUILayout.Label("Applies to this screen too, so you can see what you are changing.", body);
            GUILayout.Space(16);

            GameSettings.Subtitles = GUILayout.Toggle(GameSettings.Subtitles, " Subtitles", toggle);
            GUILayout.Label("Everything spoken is also written. Salty never speaks in words at all, " +
                            "so nothing it tells you was ever sound-only.", body);
        }

        private void DrawControls(GUIStyle body, GUIStyle button)
        {
            GUILayout.Label("Every command works by voice and by key. Neither is the real one.", body);
            GUILayout.Space(12);

            var producer = Producer;
            if (producer == null)
            {
                GUILayout.Label("No keyboard producer in this scene.", body);
                return;
            }

            foreach (var pair in new List<KeyValuePair<IntentId, Key>>(producer.Bindings))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(Describe(pair.Key), body, GUILayout.Width(240));

                var label = _rebinding == pair.Key ? "press a key..." : pair.Value.ToString();
                if (GUILayout.Button(label, button, GUILayout.Width(160)))
                    _rebinding = pair.Key;

                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Reset controls to defaults", button))
                producer.ResetBindings();

            GUILayout.Space(8);
            GUILayout.Label("Escape cancels a rebind.", body);
        }

        /// <summary>Small persistent hint, so nobody has to guess the panel exists.</summary>
        private void DrawHint()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(14 * GameSettings.TextScale),
                alignment = TextAnchor.UpperRight,
                normal = { textColor = GameSettings.HighContrast
                    ? Color.white
                    : new Color(0.75f, 0.78f, 0.82f, 0.85f) }
            };

            GUI.Label(new Rect(Screen.width - 320, 18, 300, 30), "Esc  settings", style);
        }

        private static string Describe(IntentId id)
        {
            switch (id)
            {
                case IntentId.Go: return "Go forward";
                case IntentId.Back: return "Back up";
                case IntentId.Left: return "Turn left";
                case IntentId.Right: return "Turn right";
                case IntentId.Stop: return "Stop";
                case IntentId.Open: return "Open";
                case IntentId.Light: return "Lamp";
                case IntentId.Wake: return "Transmit";
                case IntentId.Repeat: return "Say again";
                case IntentId.Restart: return "Run it again";
                case IntentId.Help: return "Help";
                default: return id.ToString();
            }
        }
    }
}
