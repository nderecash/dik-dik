#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Diagnostics;
using Dikdik.Commands;
using Dikdik.Game;
using Dikdik.Matching;
using UnityEngine;
using Whisper;
using Whisper.Utils;
using Debug = UnityEngine.Debug;

namespace Dikdik.Producers
{
    /// <summary>
    /// Voice half of the input story. Speech to text runs on this machine,
    /// with no network call and no account, which is both a privacy position
    /// and a practical one: the game works on a train and in a lab with no wifi.
    ///
    /// Flow, once per utterance:
    ///   microphone records, voice activity detection notices you stopped talking,
    ///   the clip goes to whisper, the transcript goes to the fuzzy matcher,
    ///   and an <see cref="Intent"/> comes out the far end. From the bus onward
    ///   it is indistinguishable from someone pressing a key.
    ///
    /// The whole file is compiled out of WebGL builds. whisper.cpp has no working
    /// WebGL target, so rather than ship a voice button that quietly does nothing,
    /// the browser build has no voice producer at all and says so in settings.
    /// </summary>
    public class VoiceCommandProducer : MonoBehaviour, ICommandProducer
    {
        [Header("Wiring")]
        [SerializeField] private WhisperManager whisper;
        [SerializeField] private MicrophoneRecord microphone;

        [Header("Behaviour")]
        [Tooltip("Keep listening after each utterance. Off means push to talk.")]
        [SerializeField] private bool continuousListening = true;

        [Tooltip("Ignore clips shorter than this. Filters out coughs and door slams.")]
        [SerializeField] private float minimumClipSeconds = 0.3f;

        public event Action<Intent> CommandProduced;

        /// <summary>
        /// Raised for every transcript, matched or not, with how long whisper took.
        /// The feedback panel and the spike log both listen to this.
        /// </summary>
        public event Action<string, long> TranscriptReady;

        /// <summary>Raised when the player starts and stops speaking. Drives the listening indicator.</summary>
        public event Action<bool> VoiceDetectedChanged;

        /// <summary>
        /// Raised with the whole utterance: the audio, not just what we made of it.
        ///
        /// Every other voice interface converts speech to a command and drops the sound
        /// on the floor. <see cref="Dikdik.Game.VoiceJournal"/> listens here and keeps it,
        /// because the ending of this game is the player's own recorded voice going out
        /// on the open loop to wake the other rovers. That only works if we never threw
        /// it away.
        /// </summary>
        public event Action<Utterance> UtteranceCaptured;

        public bool IsAvailable =>
            whisper != null &&
            microphone != null &&
            whisper.IsLoaded &&
            HasMicrophone();

        public string DisplayName => "Voice";

        public bool IsListening => microphone != null && microphone.IsRecording;

        public string LastTranscript { get; private set; } = string.Empty;

        private bool _busy;

        private void Reset()
        {
            whisper = FindAnyObjectByType<WhisperManager>();
            microphone = FindAnyObjectByType<MicrophoneRecord>();
        }

        private void OnEnable()
        {
            if (microphone != null)
            {
                // Playing the player's own voice back at them is a novelty in a demo
                // scene and an irritation in a game. Off.
                microphone.echo = false;

                // End the utterance on silence rather than on a fixed timer.
                // A fixed timer punishes anyone who speaks slowly, which is exactly
                // the kind of assumption this project is arguing with.
                microphone.useVad = true;
                microphone.vadStop = true;

                microphone.OnRecordStop += OnRecordStop;
                microphone.OnVadChanged += OnVadChanged;
            }

            if (CommandBus.Instance != null)
                CommandBus.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (microphone != null)
            {
                microphone.OnRecordStop -= OnRecordStop;
                microphone.OnVadChanged -= OnVadChanged;
            }

            if (CommandBus.Instance != null)
                CommandBus.Instance.Unregister(this);
        }

        public void StartListening()
        {
            if (microphone == null || microphone.IsRecording)
                return;

            if (!HasMicrophone())
            {
                Debug.LogWarning("[Voice] No microphone available. Keyboard still works.");
                return;
            }

            microphone.StartRecord();
        }

        public void StopListening()
        {
            if (microphone != null && microphone.IsRecording)
                microphone.StopRecord();
        }

        private void OnVadChanged(bool speaking)
        {
            VoiceDetectedChanged?.Invoke(speaking);
        }

        private async void OnRecordStop(AudioChunk chunk)
        {
            // The moment the player stopped talking. Captured here, before whisper runs,
            // because this is when they finished giving the command. Everything after
            // this line is us catching up, and the transport delay is measured from the
            // person rather than from our own processing.
            var spokenAt = Time.time;

            // Restart straight away so we are listening again while whisper thinks.
            // Otherwise the player learns to wait for us, which is backwards.
            if (continuousListening && isActiveAndEnabled)
                StartListening();

            // Drop anything captured while Control was speaking. It is most likely the
            // game's own voice arriving back through the microphone, or the player
            // talking over the briefing. Transcribing it would be the game hearing
            // itself, which is where the chaos came from.
            if (SupervisorVoice.IsListeningBlocked)
                return;

            if (_busy)
                return;

            if (chunk.Length < minimumClipSeconds || chunk.Data == null || chunk.Data.Length == 0)
                return;

            _busy = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await whisper.GetTextAsync(chunk.Data, chunk.Frequency, chunk.Channels);
                stopwatch.Stop();

                var text = result != null ? result.Result : string.Empty;

                // Whisper narrates absence: [BLANK_AUDIO], [Music], [ Silence ], [ Grunts ].
                // That is the model describing a room, not a person speaking. Say nothing
                // at all: no transcript, no failed command, no feedback. Someone who has
                // not spoken has not failed, and telling them otherwise teaches them to
                // distrust the microphone.
                if (FuzzyIntentMatcher.IsNonSpeech(text))
                    return;

                LastTranscript = text;

                TranscriptReady?.Invoke(text, stopwatch.ElapsedMilliseconds);

                // Stamped with when they stopped speaking, not with now. By this point
                // whisper has eaten most of the transport budget, so the bus holds this
                // for whatever is left rather than starting the clock again.
                var intent = FuzzyIntentMatcher.Match(text, CommandSource.Voice).At(spokenAt);

                // Keep the sound before anything reacts to the meaning. Unrecognised
                // utterances are kept too: the player still spoke, and a broadcast made
                // only of the sentences a machine happened to understand would be a
                // strange thing for this game of all games to assemble.
                UtteranceCaptured?.Invoke(new Utterance
                {
                    Samples = chunk.Data,
                    Frequency = chunk.Frequency,
                    Channels = chunk.Channels,
                    Transcript = text,
                    Intent = intent.Id,
                    CapturedAt = spokenAt
                });

                CommandProduced?.Invoke(intent);
            }
            catch (Exception e)
            {
                stopwatch.Stop();
                Debug.LogError($"[Voice] Transcription failed: {e}");
            }
            finally
            {
                _busy = false;
            }
        }

        private static bool HasMicrophone()
        {
            return Microphone.devices != null && Microphone.devices.Length > 0;
        }
    }
}
#endif
