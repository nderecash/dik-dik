#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Diagnostics;
using Dikdik.Commands;
using Dikdik.Game;
using Dikdik.Game.Voice;
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

        // There used to be an UtteranceCaptured event here, carrying the raw microphone
        // samples, and a VoiceJournal that kept every one of them for the whole session.
        // The ending played them back: your own voice, every command, unedited.
        //
        // It was built, it worked, and playing it felt like surveillance. The microphone
        // does not record commands, it records a room, and hearing that room played back
        // is not the same experience as hearing yourself give instructions.
        //
        // So the retention is gone, not just the ending. Audio is transcribed and dropped
        // inside OnRecordStop and never leaves this method. That is a stronger claim than
        // "nothing leaves your machine", and it came from playing the thing rather than
        // from a policy.

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

            // Own the microphone's on/off state, rather than letting Bootstrap read the
            // setting once at launch and walk away.
            //
            // That is what it did, and it made the setting a one-way door. Bootstrap
            // checks GameSettings.VoiceEnabled inside Start and calls StartListening if it
            // is true. Nothing anywhere reacted to it changing. So turning voice off and
            // back on left the microphone off for the rest of the session, and unticking
            // the box did not stop the microphone at all: it kept recording and
            // transcribing after the player had asked it not to. Of the two, that second
            // one is the serious one.
            GameSettings.Changed += ApplyVoiceSetting;

            // And re-evaluate when a briefing ends, which is when the microphone is
            // allowed to open. Without this the gate would close and never reopen.
            if (VoiceArbiter.Instance != null)
                VoiceArbiter.Instance.SequenceFinished += ApplyVoiceSetting;
        }

        /// <summary>
        /// Start or stop listening to match the player's setting and the state of the game,
        /// right now.
        ///
        /// <para>The briefing gate is the second half of this. The microphone used to open
        /// in Bootstrap.Start, before the opening had said a word, so the game was talking
        /// and listening at the same time and never told the player which. Somebody spoke
        /// during the cinematic and it jammed.</para>
        ///
        /// <para>A critical sequence is a briefing. While one is running the microphone
        /// stays shut, the panel says so, and it opens the moment the sequence ends.</para>
        /// </summary>
        public void ApplyVoiceSetting()
        {
            var briefing = VoiceArbiter.Instance != null && VoiceArbiter.Instance.IsSequenceRunning;

            if (GameSettings.VoiceEnabled && IsAvailable && !briefing)
            {
                if (microphone != null && !microphone.IsRecording)
                    StartListening();

                return;
            }

            if (microphone != null && microphone.IsRecording)
                StopListening();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= ApplyVoiceSetting;

            if (VoiceArbiter.Instance != null)
                VoiceArbiter.Instance.SequenceFinished -= ApplyVoiceSetting;

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

        /// <summary>
        /// When voice detection last fired, on the bus clock. Negative when not yet set.
        /// The onset timestamp latency compensation runs on. See <see cref="Intent.StartedAt"/>.
        /// </summary>
        private float _speechStartedAt = -1f;

        private void OnVadChanged(bool speaking)
        {
            VoiceDetectedChanged?.Invoke(speaking);

            // Two jobs on the same event, worth telling apart.
            //
            // The first is the attention reflex: put the rover on alert the moment somebody
            // starts talking, before anyone knows what they are saying. It eases off rather
            // than acting, so the transport delay is untouched, and the keyboard producer
            // does the same on key down so neither input is privileged.
            //
            // The second is timestamping. This instant, not the one when they stop talking,
            // is when the player decided. Recorded here, applied in OnRecordStop, and it is
            // what latency compensation runs on. See Intent.StartedAt.
            if (speaking && _speechStartedAt < 0f)
                _speechStartedAt = CommandBus.Clock;

            Dikdik.Game.RoverAttention.SetVoiceDetectedStatic(speaking);
        }

        private async void OnRecordStop(AudioChunk chunk)
        {
            // The moment the player stopped talking. Captured here, before whisper runs,
            // because this is when they finished giving the command. Everything after
            // this line is us catching up, and the transport delay is measured from the
            // person rather than from our own processing.
            var spokenAt = CommandBus.Clock;

            // And when they started, from the voice-detection onset. Falls back to the end
            // stamp if no onset was seen, which keeps the old behaviour rather than
            // inventing a compensation out of a missing measurement.
            //
            // Consumed and reset here, because the next utterance needs its own onset and
            // listening has already restarted below.
            var startedAt = _speechStartedAt >= 0f ? _speechStartedAt : spokenAt;
            _speechStartedAt = -1f;

            // Restart straight away so we are listening again while whisper thinks.
            // Otherwise the player learns to wait for us, which is backwards.
            if (continuousListening && isActiveAndEnabled)
                StartListening();

            // Drop anything captured while Control was speaking. It is most likely the
            // game's own voice arriving back through the microphone, or the player
            // talking over the briefing. Transcribing it would be the game hearing
            // itself, which is where the chaos came from.
            if (VoiceArbiter.IsListeningBlocked || GamePause.IsPaused)
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

                // Both stamps, not now. By this point whisper has eaten most of the
                // transport budget, so the bus holds this for whatever is left rather than
                // starting the clock again.
                //
                // startedAt is what the bus schedules from when compensation is on, so a
                // player is not charged for the time it took them to say the word.
                var intent = FuzzyIntentMatcher.Match(text, CommandSource.Voice)
                                               .At(startedAt, spokenAt);

                // chunk.Data goes out of scope here and is never copied, stored or
                // written to disk. The transcript survives; the audio does not.
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
