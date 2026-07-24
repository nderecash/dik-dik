using System;
using System.Collections.Generic;
using Dikdik.Commands;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dikdik.Producers
{
    /// <summary>
    /// Keyboard half of the input story.
    ///
    /// This ships in the same commit as the voice producer, always. Every command
    /// the rover accepts by voice it accepts by key, with no extra steps and no
    /// reduced outcome. If you are reading this file to check whether the keyboard
    /// path is a real path or a courtesy, the answer is in
    /// <see cref="Dikdik.Commands.CommandSource"/>: nothing downstream reads it.
    ///
    /// Bindings are rebindable and persist, which is the Game Accessibility
    /// Guidelines basic item "allow controls to be remapped / reconfigured".
    /// </summary>
    public class KeyboardCommandProducer : MonoBehaviour, ICommandProducer
    {
        private const string PrefsPrefix = "dikdik.binding.";

        public event Action<Intent> CommandProduced;

        public bool IsAvailable => Keyboard.current != null;

        public string DisplayName => "Keyboard";

        /// <summary>
        /// Defaults chosen to be reachable one handed on the left of the keyboard,
        /// so the whole game is playable without crossing the board.
        /// </summary>
        private static readonly Dictionary<IntentId, Key> Defaults = new Dictionary<IntentId, Key>
        {
            { IntentId.Go, Key.W },
            { IntentId.Back, Key.S },
            { IntentId.Left, Key.A },
            { IntentId.Right, Key.D },
            { IntentId.Stop, Key.Space },
            { IntentId.Open, Key.E },
            { IntentId.Light, Key.F },
            { IntentId.Wake, Key.Q },
            { IntentId.Repeat, Key.R },
            { IntentId.Help, Key.H }
        };

        private readonly Dictionary<IntentId, Key> _bindings = new Dictionary<IntentId, Key>();

        public IReadOnlyDictionary<IntentId, Key> Bindings => _bindings;

        private void Awake()
        {
            LoadBindings();
        }

        private void OnEnable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (CommandBus.Instance != null)
                CommandBus.Instance.Unregister(this);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            foreach (var pair in _bindings)
            {
                // A jammed key is dead until the command bound to it is remapped
                // elsewhere. This is the whole of the jammed-key level: the console
                // reports a stuck control, the physical key does nothing, and the fix
                // is to bind that command to a different key. The fault is the console's,
                // never the player's.
                if (pair.Value == _jammedKey)
                    continue;

                var control = keyboard[pair.Value];
                if (control != null && control.wasPressedThisFrame)
                {
                    // RawText is the key name so the "I heard" panel can show
                    // keyboard players the same feedback voice players get.
                    //
                    // Stamped with now, which for a key press is genuinely when the
                    // player finished giving the command. The bus then holds it the
                    // full transport delay, exactly as long as a spoken command that
                    // has already spent most of that budget inside whisper. Neither
                    // route reaches the rover first.
                    CommandProduced?.Invoke(
                        new Intent(pair.Key, CommandSource.Keyboard, pair.Value.ToString(), 1f, Time.time));
                }
            }
        }

        private Key _jammedKey = Key.None;

        /// <summary>The stuck key, or None. Shown in the settings so the player knows which.</summary>
        public Key JammedKey => _jammedKey;

        /// <summary>Jam a physical key. It stops registering until nothing is bound to it.</summary>
        public void SetJammedKey(Key key) => _jammedKey = key;

        public void ClearJam() => _jammedKey = Key.None;

        /// <summary>The key currently bound to a command, or None.</summary>
        public Key KeyFor(IntentId intent) =>
            _bindings.TryGetValue(intent, out var key) ? key : Key.None;

        public void Rebind(IntentId intent, Key key)
        {
            _bindings[intent] = key;
            PlayerPrefs.SetString(PrefsPrefix + intent, key.ToString());
            PlayerPrefs.Save();
        }

        public void ResetBindings()
        {
            foreach (var pair in Defaults)
            {
                _bindings[pair.Key] = pair.Value;
                PlayerPrefs.DeleteKey(PrefsPrefix + pair.Key);
            }

            PlayerPrefs.Save();
        }

        private void LoadBindings()
        {
            _bindings.Clear();

            foreach (var pair in Defaults)
            {
                var saved = PlayerPrefs.GetString(PrefsPrefix + pair.Key, string.Empty);

                if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out Key parsed))
                    _bindings[pair.Key] = parsed;
                else
                    _bindings[pair.Key] = pair.Value;
            }
        }
    }
}
