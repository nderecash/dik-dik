using System;
using System.Collections.Generic;
using Dikdik.Commands;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Keeps every sound the player made, so the game can give it back to them.
    ///
    /// This is the ending. Through the whole game the player speaks and the rover acts,
    /// and it looks like the usual bargain where your voice is converted into commands
    /// and thrown away. It is not being thrown away. At the final level the player stops
    /// commanding and starts broadcasting, and what goes out is this: their own voice,
    /// in order, unedited, waking every dormant rover on the surface.
    ///
    /// The argument does not need stating after that. The thing that reaches everyone
    /// else was never translated into anything.
    ///
    /// A side effect worth not preventing: a player who maps musical sounds during
    /// remapping gets music out of the broadcast. We do not build that. We just do not
    /// stand in its way.
    /// </summary>
    public class VoiceJournal : MonoBehaviour
    {
        [Tooltip("Ceiling on retained audio. At 16 kHz mono this is about 4 MB per minute.")]
        [SerializeField] private float maximumSeconds = 300f;

        [Tooltip("Silence inserted between clips in the final broadcast")]
        [SerializeField] private float gapSeconds = 0.35f;

        [Tooltip("Keep utterances we could not understand. They were still the player speaking.")]
        [SerializeField] private bool keepUnrecognised = true;

        private readonly List<Utterance> _utterances = new List<Utterance>();
        private float _retainedSeconds;

        public IReadOnlyList<Utterance> Utterances => _utterances;
        public float RetainedSeconds => _retainedSeconds;

        /// <summary>Raised whenever something new is kept. The end-of-game screen counts these.</summary>
        public event Action<Utterance> Captured;

        public void Capture(Utterance utterance)
        {
            if (utterance.Samples == null || utterance.Samples.Length == 0)
                return;

            if (!keepUnrecognised && utterance.Intent == IntentId.None)
                return;

            // Drop the oldest rather than refuse the newest. A player near the end of a
            // long session should still hear the things they just said.
            while (_retainedSeconds + utterance.Seconds > maximumSeconds && _utterances.Count > 0)
            {
                _retainedSeconds -= _utterances[0].Seconds;
                _utterances.RemoveAt(0);
            }

            _utterances.Add(utterance);
            _retainedSeconds += utterance.Seconds;

            Captured?.Invoke(utterance);
        }

        /// <summary>
        /// Stitch everything the player said into one clip, in the order they said it.
        ///
        /// Deliberately not normalised, cleaned or trimmed. The hesitations, the false
        /// starts and the sentence where they told the rover it was going the wrong way
        /// are the point. A tidied version would be a different voice.
        /// </summary>
        public AudioClip BuildBroadcast(string clipName = "broadcast")
        {
            if (_utterances.Count == 0)
                return null;

            var frequency = _utterances[0].Frequency;
            var channels = _utterances[0].Channels;
            var gap = Mathf.Max(0, Mathf.RoundToInt(gapSeconds * frequency * channels));

            var total = 0;
            foreach (var utterance in _utterances)
                total += utterance.Samples.Length + gap;

            var buffer = new float[total];
            var cursor = 0;

            foreach (var utterance in _utterances)
            {
                // Mismatched rates would play back at the wrong pitch. The microphone is
                // fixed at 16 kHz mono so this should not happen, but a silent chipmunk
                // ending would be a miserable way to find out otherwise.
                if (utterance.Frequency != frequency || utterance.Channels != channels)
                {
                    Debug.LogWarning($"[VoiceJournal] Skipped a clip recorded at " +
                                     $"{utterance.Frequency} Hz / {utterance.Channels} ch, " +
                                     $"expected {frequency} / {channels}.");
                    continue;
                }

                Array.Copy(utterance.Samples, 0, buffer, cursor, utterance.Samples.Length);
                cursor += utterance.Samples.Length + gap;
            }

            var clip = AudioClip.Create(clipName, buffer.Length / channels, channels, frequency, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        /// <summary>Transcripts in order, for the subtitles that run under the broadcast.</summary>
        public List<string> BroadcastTranscript()
        {
            var lines = new List<string>(_utterances.Count);

            foreach (var utterance in _utterances)
                if (!string.IsNullOrWhiteSpace(utterance.Transcript))
                    lines.Add(utterance.Transcript.Trim());

            return lines;
        }

        public void Clear()
        {
            _utterances.Clear();
            _retainedSeconds = 0f;
        }
    }
}
