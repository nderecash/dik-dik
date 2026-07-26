using System;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Every accessibility option in one place, persisted, and available from the
    /// main menu on first launch.
    ///
    /// Nothing here is ever unlocked, earned, or hidden behind progress. A level may
    /// show you why an option exists. It must never make you win something to get it.
    /// Treating access as a reward would invert the whole argument of this game.
    ///
    /// Defaults are chosen so the game is already reasonable before anyone opens
    /// this screen, because most people never will.
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "dikdik.settings.";

        /// <summary>Raised whenever anything changes, so live UI can follow along.</summary>
        public static event Action Changed;

        private static float _gameSpeed = 1f;
        private static bool _highContrast;
        private static float _textScale = 1f;
        private static bool _subtitles = true;
        private static bool _voiceEnabled = true;
        private static float _effectsVolume = 1f;

        /// <summary>
        /// Guideline: "Include an option to adjust the game speed."
        /// Also the honest fix for slow speech: at 0.5 nothing outruns you.
        /// </summary>
        public static float GameSpeed
        {
            get => _gameSpeed;
            set => Set(ref _gameSpeed, Mathf.Clamp(value, 0.25f, 1.5f), "gameSpeed");
        }

        /// <summary>Guideline: "Provide high contrast between text/UI and background."</summary>
        public static bool HighContrast
        {
            get => _highContrast;
            set => Set(ref _highContrast, value, "highContrast");
        }

        /// <summary>Guideline: "Use an easily readable default font size." This scales it further.</summary>
        public static float TextScale
        {
            get => _textScale;
            set => Set(ref _textScale, Mathf.Clamp(value, 1f, 2.5f), "textScale");
        }

        /// <summary>Guideline: "Provide subtitles for all important speech." On by default.</summary>
        public static bool Subtitles
        {
            get => _subtitles;
            set => Set(ref _subtitles, value, "subtitles");
        }

        /// <summary>
        /// Whether the microphone is listening. Turning this off is not a lesser mode,
        /// it is the mode the whole browser build runs in.
        /// </summary>
        public static bool VoiceEnabled
        {
            get => _voiceEnabled;
            set => Set(ref _voiceEnabled, value, "voiceEnabled");
        }

        /// <summary>
        /// How loud the world is: tires, wind, tones. Not the voice, which has its own
        /// level and must stay audible even with the world turned all the way down.
        ///
        /// <para>Separating them is the point. Someone who finds the constant tire roll
        /// tiring, or who is running a screen reader over the captions, can silence the
        /// world without losing the half of the game that carries the meaning. Guideline:
        /// "Provide separate volume controls or mutes for effects, speech and background."</para>
        /// </summary>
        public static float EffectsVolume
        {
            get => _effectsVolume;
            set => Set(ref _effectsVolume, Mathf.Clamp01(value), "effectsVolume");
        }

        public static void Load()
        {
            _gameSpeed = PlayerPrefs.GetFloat(Prefix + "gameSpeed", 1f);
            _effectsVolume = PlayerPrefs.GetFloat(Prefix + "effectsVolume", 1f);
            _highContrast = PlayerPrefs.GetInt(Prefix + "highContrast", 0) == 1;
            _textScale = PlayerPrefs.GetFloat(Prefix + "textScale", 1f);
            _subtitles = PlayerPrefs.GetInt(Prefix + "subtitles", 1) == 1;
            _voiceEnabled = PlayerPrefs.GetInt(Prefix + "voiceEnabled", 1) == 1;
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            _gameSpeed = 1f;
            _effectsVolume = 1f;
            _highContrast = false;
            _textScale = 1f;
            _subtitles = true;
            _voiceEnabled = true;
            Save();
        }

        /// <summary>
        /// Writes the values but does not flush to disk.
        ///
        /// The settings screen has sliders, and a slider being dragged produces a new
        /// value every frame. PlayerPrefs.Save writes the file, so flushing on every
        /// change would hit the disk sixty times a second for as long as someone holds
        /// the mouse down. Setting the value is cheap; committing it is not.
        /// </summary>
        private static void Save()
        {
            PlayerPrefs.SetFloat(Prefix + "gameSpeed", _gameSpeed);
            PlayerPrefs.SetFloat(Prefix + "effectsVolume", _effectsVolume);
            PlayerPrefs.SetInt(Prefix + "highContrast", _highContrast ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "textScale", _textScale);
            PlayerPrefs.SetInt(Prefix + "subtitles", _subtitles ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "voiceEnabled", _voiceEnabled ? 1 : 0);
            Changed?.Invoke();
        }

        /// <summary>
        /// Commit to disk. Called when the settings screen closes and on quit, which
        /// covers every way a player can stop changing things.
        /// </summary>
        public static void Flush()
        {
            PlayerPrefs.Save();
        }

        private static void Set<T>(ref T field, T value, string _)
        {
            if (Equals(field, value))
                return;

            field = value;
            Save();
        }
    }
}
