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
                var control = keyboard[pair.Value];
                if (control != null && control.wasPressedThisFrame)
                {
                    // RawText is the key name so the "I heard" panel can show
                    // keyboard players the same feedback voice players get.
                    CommandProduced?.Invoke(
                        new Intent(pair.Key, CommandSource.Keyboard, pair.Value.ToString()));
                }
            }
        }

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
