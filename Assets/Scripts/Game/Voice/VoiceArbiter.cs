using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dikdik.Game.Voice
{
    /// <summary>
    /// Everything spoken in the game goes through here, and only one thing is ever
    /// speaking.
    ///
    /// <para><b>Why this exists.</b> Speech used to be played with
    /// <c>AudioSource.PlayOneShot</c> from four unrelated components. PlayOneShot mixes a
    /// new voice over whatever is already sounding and cannot be stopped individually, so
    /// the supervisor, the station's automated system and the rover could all talk at
    /// once, over each other, with captions from whichever spoke last. It was not one bug;
    /// it was the absence of anybody being in charge.</para>
    ///
    /// <para><b>The rules.</b> One queue, one AudioSource, <c>Play</c> and never
    /// <c>PlayOneShot</c>. A higher priority line interrupts a lower one and clears
    /// anything lesser waiting behind it. Equal priorities queue in order. A lower
    /// priority line is dropped rather than made to wait, unless it is marked
    /// <see cref="SpeechLine.Essential"/>, in which case it waits its turn.</para>
    ///
    /// <para><b>Captions cannot drift</b> because the same method that starts the clip
    /// writes the caption, and the same method that stops it clears the caption. There is
    /// no path through this class that does one without the other.</para>
    ///
    /// <para><b>The clock is local and unscaled.</b> Not <c>Time.time</c>, which a pause
    /// would freeze and leave the microphone shut forever, and not
    /// <c>Time.deltaTime</c>, which the game speed setting scales. Speech runs at the
    /// speed people talk regardless of how slowly the player has asked the world to
    /// move.</para>
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class VoiceArbiter : MonoBehaviour
    {
        public static VoiceArbiter Instance { get; private set; }

        [SerializeField] private AudioSource source;
        [SerializeField] private CommsDisplay comms;

        [Tooltip("Resources folder holding the recorded supervisor lines")]
        [SerializeField] private string voiceFolder = "Voice";

        private VoiceClipBank _bank;

        private readonly List<QueuedLine> _queue = new List<QueuedLine>();
        private QueuedLine _current;

        private float _clock;          // local, unscaled, frozen while paused
        private float _lineEndsAt;
        private float _gapUntil;

        private static float _listenBlockedUntil;
        private static float _staticClock;

        private int _sequenceRemaining;
        private bool _sequenceSkippable;

        private class QueuedLine
        {
            public SpeechLine Line;
            public SpeechHandle Handle;
            public bool PartOfSequence;
        }

        /// <summary>Something is speaking right now.</summary>
        public bool IsSpeaking => _current != null;

        public SpeechPriority CurrentPriority =>
            _current != null ? _current.Line.Priority : SpeechPriority.Idle;

        /// <summary>A multi-line sequence (a briefing) is running.</summary>
        public bool IsSequenceRunning => _sequenceRemaining > 0;

        /// <summary>
        /// The microphone must stay shut. True while anything is speaking, plus a tail.
        ///
        /// Static so the voice producer can consult it without holding a reference; that
        /// component is compiled out of WebGL entirely and must not depend on this one.
        /// </summary>
        public static bool IsListeningBlocked => _staticClock < _listenBlockedUntil;

        /// <summary>A skippable sequence is running, so a keypress should skip rather than command.</summary>
        public static bool IsInputBlocked { get; private set; }

        public event Action<SpeechLine> LineStarted;
        public event Action<SpeechLine> LineFinished;
        public event Action SequenceFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            _bank = new VoiceClipBank(voiceFolder);

            // Statics survive a session; a stale block would keep the mic shut forever.
            _listenBlockedUntil = 0f;
            _staticClock = 0f;
            IsInputBlocked = false;
        }

        private void OnEnable() => GamePause.Changed += OnPauseChanged;

        private void OnDisable()
        {
            GamePause.Changed -= OnPauseChanged;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Saying things
        // ------------------------------------------------------------------

        public SpeechHandle Say(SpeechLine line)
        {
            if (line.Clip == null)
                return SpeechHandle.Finished();

            var handle = new SpeechHandle();
            var queued = new QueuedLine { Line = line, Handle = handle };

            // Nothing speaking: go now.
            if (_current == null && _queue.Count == 0 && _clock >= _gapUntil)
            {
                StartLine(queued);
                return handle;
            }

            var blockingPriority = _current != null ? _current.Line.Priority : HighestQueued();

            if (line.Priority > blockingPriority)
            {
                // More important than what is happening. Interrupt, and clear anything
                // waiting that matters less than this does.
                InterruptCurrent();
                DropQueuedBelow(line.Priority);
                StartLine(queued);
                return handle;
            }

            if (line.Priority == blockingPriority || line.Essential)
            {
                _queue.Add(queued);
                return handle;
            }

            // Less important and not essential: say nothing rather than say it late.
            handle.Drop();
            return handle;
        }

        /// <summary>Say the next line from a recorded group.</summary>
        public SpeechHandle SayGroup(string group, SpeechPriority priority,
                                     Speaker speaker = Speaker.Control, bool essential = false)
        {
            var clip = _bank.Next(group);
            if (clip == null)
                return SpeechHandle.Finished();

            return Say(SpeechLine.Make(clip, VoiceLines.Caption(clip.name), speaker, priority, essential));
        }

        /// <summary>
        /// Say one named clip. For lines that belong to a specific level rather than to a
        /// cycling pool — the sector introductions, where level three wants its own line
        /// and not simply the next one in the list.
        /// </summary>
        public SpeechHandle SayClip(string clipName, SpeechPriority priority,
                                    Speaker speaker = Speaker.Control, bool essential = false)
        {
            var clip = Resources.Load<AudioClip>($"{voiceFolder}/{clipName}");
            if (clip == null)
                return SpeechHandle.Finished();

            return Say(SpeechLine.Make(clip, VoiceLines.Caption(clipName), speaker, priority, essential));
        }

        /// <summary>
        /// Say every line of a group back to back, as one continuous piece of speech.
        ///
        /// Used for briefings. Because these queue like anything else, "wait for the
        /// briefing to finish" stops being a special case anybody has to implement and
        /// becomes simply what a queue does.
        /// </summary>
        public SpeechHandle SaySequence(string group, SpeechPriority priority, bool skippable = true)
        {
            var clips = _bank.All(group);
            if (clips.Count == 0)
                return SpeechHandle.Finished();

            _sequenceRemaining += clips.Count;
            _sequenceSkippable = skippable;
            IsInputBlocked = skippable;

            SpeechHandle last = null;
            for (var i = 0; i < clips.Count; i++)
            {
                var line = SpeechLine.Make(clips[i], VoiceLines.Caption(clips[i].name),
                                           Speaker.Control, priority, essential: true);
                last = SayAsSequencePart(line);
            }

            return last ?? SpeechHandle.Finished();
        }

        private SpeechHandle SayAsSequencePart(SpeechLine line)
        {
            var handle = Say(line);

            // Mark it so finishing it decrements the sequence counter.
            if (_current != null && _current.Handle == handle) _current.PartOfSequence = true;
            for (var i = 0; i < _queue.Count; i++)
                if (_queue[i].Handle == handle) _queue[i].PartOfSequence = true;

            return handle;
        }

        /// <summary>Cut a running sequence short. The mic stays shut briefly so the tail does not leak.</summary>
        public void SkipCurrentSequence()
        {
            if (_sequenceRemaining <= 0)
                return;

            if (_current != null && _current.PartOfSequence)
                InterruptCurrent();

            for (var i = _queue.Count - 1; i >= 0; i--)
            {
                if (!_queue[i].PartOfSequence) continue;
                _queue[i].Handle.Interrupt();
                _queue.RemoveAt(i);
            }

            _sequenceRemaining = 0;
            IsInputBlocked = false;

            // Never zero. Speakers take a moment to fall silent and the mic would catch
            // the decay of whatever was just cut off.
            _listenBlockedUntil = _staticClock + 0.25f;

            ClearCaption();
            SequenceFinished?.Invoke();
        }

        public void StopAll()
        {
            InterruptCurrent();

            foreach (var queued in _queue)
                queued.Handle.Interrupt();
            _queue.Clear();

            _sequenceRemaining = 0;
            IsInputBlocked = false;
            _listenBlockedUntil = _staticClock + 0.25f;
            ClearCaption();
        }

        // ------------------------------------------------------------------
        // The loop
        // ------------------------------------------------------------------

        private void Update()
        {
            if (GamePause.IsPaused)
                return;

            var step = Time.unscaledDeltaTime;
            _clock += step;
            _staticClock += step;

            if (_current != null)
            {
                if (_clock < _lineEndsAt)
                    return;

                FinishCurrent();
                return;
            }

            if (_queue.Count == 0 || _clock < _gapUntil)
                return;

            var next = TakeHighestQueued();
            StartLine(next);
        }

        private void StartLine(QueuedLine queued)
        {
            _current = queued;

            if (source != null)
            {
                source.clip = queued.Line.Clip;
                source.Play();
            }

            _lineEndsAt = _clock + queued.Line.Seconds;

            // Shut the microphone for the clip plus its tail, or the game transcribes its
            // own voice coming back through the speakers and answers itself.
            _listenBlockedUntil = Mathf.Max(_listenBlockedUntil,
                                            _staticClock + queued.Line.Seconds + queued.Line.TailSeconds);

            ShowCaption(queued.Line);
            LineStarted?.Invoke(queued.Line);
        }

        private void FinishCurrent()
        {
            var finished = _current;
            _current = null;

            if (source != null)
                source.Stop();

            _gapUntil = _clock + finished.Line.GapAfter;

            finished.Handle.Complete();
            LineFinished?.Invoke(finished.Line);

            if (finished.PartOfSequence)
            {
                _sequenceRemaining = Mathf.Max(0, _sequenceRemaining - 1);
                if (_sequenceRemaining == 0)
                {
                    IsInputBlocked = false;
                    SequenceFinished?.Invoke();
                }
            }

            // Only clear the caption if nothing is queued to replace it immediately,
            // so a briefing does not flicker between its own lines.
            if (_queue.Count == 0)
                ClearCaption();
        }

        private void InterruptCurrent()
        {
            if (_current == null)
                return;

            if (source != null)
                source.Stop();

            _current.Handle.Interrupt();

            if (_current.PartOfSequence)
                _sequenceRemaining = Mathf.Max(0, _sequenceRemaining - 1);

            _current = null;
        }

        // ------------------------------------------------------------------
        // Queue helpers
        // ------------------------------------------------------------------

        private SpeechPriority HighestQueued()
        {
            var highest = SpeechPriority.Idle;
            foreach (var queued in _queue)
                if (queued.Line.Priority > highest)
                    highest = queued.Line.Priority;

            return highest;
        }

        private QueuedLine TakeHighestQueued()
        {
            var bestIndex = 0;
            for (var i = 1; i < _queue.Count; i++)
                if (_queue[i].Line.Priority > _queue[bestIndex].Line.Priority)
                    bestIndex = i;

            var queued = _queue[bestIndex];
            _queue.RemoveAt(bestIndex);
            return queued;
        }

        private void DropQueuedBelow(SpeechPriority priority)
        {
            for (var i = _queue.Count - 1; i >= 0; i--)
            {
                if (_queue[i].Line.Priority >= priority || _queue[i].Line.Essential)
                    continue;

                _queue[i].Handle.Drop();
                _queue.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------------
        // Captions and pause
        // ------------------------------------------------------------------

        private void ShowCaption(SpeechLine line)
        {
            if (comms == null || string.IsNullOrWhiteSpace(line.Caption))
                return;

            switch (line.Speaker)
            {
                case Speaker.Station:
                    comms.ShowStationLine(line.Caption);
                    break;
                case Speaker.Salty:
                    comms.ShowRoverQuestion(line.Caption);
                    break;
                default:
                    comms.ShowSupervisorLine(line.Caption, IsSequenceRunning);
                    break;
            }
        }

        private void ClearCaption()
        {
            if (comms != null)
                comms.ClearSupervisor();
        }

        private void OnPauseChanged(bool paused)
        {
            if (source == null)
                return;

            // Pause, not stop. Resuming mid-sentence is what a pause means, and the clock
            // being frozen means the line still gets its full remaining time afterwards.
            if (paused) source.Pause();
            else source.UnPause();
        }
    }
}
