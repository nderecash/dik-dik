using System.Collections.Generic;
using System.Linq;
using Dikdik.Commands;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dikdik.Game
{
    /// <summary>
    /// The other human on the loop. Plays the recorded supervisor lines at the right
    /// moments, always with the caption on screen.
    ///
    /// <para>This is the one human voice in the game, and it is deliberately radio-degraded,
    /// so the caption is not a courtesy: it carries the meaning while the audio carries the
    /// mood. Every line plays through <see cref="CommsDisplay"/> so subtitles and audio can
    /// never drift apart.</para>
    ///
    /// <para>Lives in the persistent Boot scene. It re-finds the per-level components each
    /// time a scene loads, because the rover and the level director are recreated per level
    /// while this is not.</para>
    ///
    /// <para>Lines within a group cycle rather than shuffle, the same rule as everywhere
    /// else: random repeats itself in a way players read as the game not paying attention.</para>
    /// </summary>
    public class SupervisorVoice : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private CommsDisplay comms;

        [Header("How often the optional lines are allowed")]
        [Tooltip("Seconds between spoken not-understood lines. The console text still shows " +
                 "every time; the voice chimes in less often so it does not nag.")]
        [SerializeField] private float missCooldown = 9f;

        [Tooltip("Seconds of stillness before the first idle line. Each one after pushes " +
                 "the next further out, so it stops repeating that it does not mind waiting.")]
        [SerializeField] private float idleAfter = 40f;

        [Tooltip("How much further out each idle line pushes the next. 1.6 means the gap " +
                 "grows by 60 percent each time, up to the cap.")]
        [SerializeField] private float idleBackoff = 1.6f;

        [SerializeField] private float idleMaxInterval = 180f;

        private readonly Dictionary<string, List<AudioClip>> _groups =
            new Dictionary<string, List<AudioClip>>();
        private readonly Dictionary<string, int> _cursor = new Dictionary<string, int>();

        private float _nextMissAllowed;
        private float _idleAt;
        private float _idleInterval;
        private bool _bootPlayed;
        private bool _ackThisLevel;
        private bool _briefing;

        /// <summary>
        /// While Control is speaking, the game stops listening.
        ///
        /// Two reasons. The microphone hears the game's own voice through the speakers
        /// and would transcribe Control talking to itself, and a player who starts giving
        /// commands over the briefing turns the whole thing to noise. So the voice
        /// producer checks this and drops anything captured while it is set. Extended a
        /// little past each clip so the tail does not leak in.
        /// </summary>
        private static float _listenBlockedUntil;

        public static bool IsListeningBlocked => Time.time < _listenBlockedUntil;

        /// <summary>True while the opening briefing is running. It can be skipped.</summary>
        public bool IsBriefing => _briefing;

        // Per-level components, re-found on each scene load.
        private RoverController _rover;
        private SimulationReset _simulation;
        private LevelDirector _director;

        private void Awake()
        {
            LoadClips();
        }

        private void OnEnable()
        {
            var bus = CommandBus.Instance;
            if (bus != null)
            {
                bus.CommandIssued += OnCommandIssued;
                bus.CommandNotUnderstood += OnNotUnderstood;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            var bus = CommandBus.Instance;
            if (bus != null)
            {
                bus.CommandIssued -= OnCommandIssued;
                bus.CommandNotUnderstood -= OnNotUnderstood;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnhookLevel();
        }

        private void LoadClips()
        {
            foreach (var clip in Resources.LoadAll<AudioClip>("Voice"))
            {
                // Name is sup_<group>_<nn>. Group is the middle token.
                var parts = clip.name.Split('_');
                if (parts.Length < 3)
                    continue;

                var group = parts[1];
                if (!_groups.TryGetValue(group, out var list))
                {
                    list = new List<AudioClip>();
                    _groups[group] = list;
                }

                list.Add(clip);
            }

            // Sort each group by name so cycling follows the script order, not load order.
            foreach (var list in _groups.Values)
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        // ---------------------------------------------------------------------
        // Per-level wiring
        // ---------------------------------------------------------------------
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnhookLevel();

            _rover = FindAnyObjectByType<RoverController>();
            _simulation = FindAnyObjectByType<SimulationReset>();
            _director = FindAnyObjectByType<LevelDirector>();

            _ackThisLevel = false;
            ResetIdle();

            if (_rover != null)
                _rover.Blocked += OnBlocked;

            if (_simulation != null)
                _simulation.Aborted += OnAborted;

            if (_director != null)
            {
                _director.Completed += OnLevelComplete;

                // Boot lines are the tutorial. Play them the first time a level with a
                // director comes up, once per session, whatever level that happens to be
                // so a playtester jumping straight to level 3 still gets oriented.
                if (!_bootPlayed)
                {
                    _bootPlayed = true;
                    PlaySequence("boot");
                }
            }
        }

        private void UnhookLevel()
        {
            if (_rover != null) _rover.Blocked -= OnBlocked;
            if (_simulation != null) _simulation.Aborted -= OnAborted;
            if (_director != null) _director.Completed -= OnLevelComplete;

            _rover = null;
            _simulation = null;
            _director = null;
        }

        // ---------------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------------
        private void OnCommandIssued(Intent intent)
        {
            ResetIdle();

            // One reassurance per level, on the first command that worked. After that
            // the console's own feedback is enough and the supervisor stays quiet.
            if (!_ackThisLevel)
            {
                _ackThisLevel = true;
                PlayOne("ack");
            }
        }

        private void OnNotUnderstood(Intent intent)
        {
            ResetIdle();

            // The console already shows "I did not understand" every time. The supervisor
            // speaks less often, so being misheard twice in a row does not turn into a
            // lecture.
            if (Time.time < _nextMissAllowed)
                return;

            _nextMissAllowed = Time.time + missCooldown;
            PlayOne("miss");
        }

        private void OnBlocked()
        {
            PlayOne("block");
        }

        private void OnAborted(string _)
        {
            // SimulationReset passes its own text, but the voice system owns the paired
            // audio and caption so they always match. The passed string is ignored.
            PlayOne("reset");
        }

        private void OnLevelComplete()
        {
            PlayOne("done");
        }

        private void Update()
        {
            // The briefing can be skipped. Someone who has heard it, or a returning
            // player, should not have to sit through it to start.
            if (_briefing)
            {
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.Escape))
                    SkipBriefing();

                return;
            }

            if (Time.time < _idleAt)
                return;

            // Only when genuinely idle: nothing moving, nothing in flight.
            var bus = CommandBus.Instance;
            var quiet = (_rover == null || !_rover.IsMoving) &&
                        (bus == null || bus.InTransitCount == 0) &&
                        (source == null || !source.isPlaying);

            if (quiet)
            {
                PlayOne("idle");

                // Each idle line pushes the next one further out. Standing still for a
                // long stretch should not mean being told every forty seconds that the
                // rover does not mind waiting. The ambient hum already says you are
                // connected; this is just an occasional human check-in.
                _idleInterval = Mathf.Min(_idleInterval * idleBackoff, idleMaxInterval);
            }

            _idleAt = Time.time + _idleInterval;
        }

        private void ResetIdle()
        {
            _idleInterval = idleAfter;
            _idleAt = Time.time + _idleInterval;
        }

        // ---------------------------------------------------------------------
        // Playback
        // ---------------------------------------------------------------------

        /// <summary>Play the next line in a group, cycling.</summary>
        public void PlayOne(string group)
        {
            var clip = Next(group);
            if (clip != null)
                Play(clip);
        }

        /// <summary>
        /// Play every line of a group in order, back to back. Used for the boot sequence,
        /// where the five lines are one continuous briefing.
        /// </summary>
        public void PlaySequence(string group)
        {
            if (!_groups.TryGetValue(group, out var list) || list.Count == 0)
                return;

            StopAllCoroutines();
            _briefing = true;
            StartCoroutine(PlayAll(list));
        }

        private System.Collections.IEnumerator PlayAll(List<AudioClip> list)
        {
            foreach (var clip in list)
            {
                if (!_briefing)
                    yield break;

                Play(clip);

                // Wait out the clip plus a breath, unscaled so a slowed game does not
                // stretch the briefing.
                var wait = clip.length + 0.4f;
                var until = Time.realtimeSinceStartup + wait;
                while (Time.realtimeSinceStartup < until)
                {
                    if (!_briefing)
                        yield break;

                    yield return null;
                }
            }

            EndBriefing();
        }

        private void SkipBriefing()
        {
            StopAllCoroutines();
            if (source != null)
                source.Stop();

            EndBriefing();
        }

        private void EndBriefing()
        {
            _briefing = false;
            _listenBlockedUntil = 0f;
            ResetIdle();

            if (comms != null)
                comms.ClearSupervisor();
        }

        private AudioClip Next(string group)
        {
            if (!_groups.TryGetValue(group, out var list) || list.Count == 0)
                return null;

            var index = _cursor.TryGetValue(group, out var c) ? c : 0;
            var clip = list[index % list.Count];
            _cursor[group] = index + 1;
            return clip;
        }

        private void Play(AudioClip clip)
        {
            if (source != null)
                source.PlayOneShot(clip);

            // Stop listening for the length of the clip plus a tail, so the game does not
            // transcribe its own voice coming back through the microphone.
            _listenBlockedUntil = Mathf.Max(_listenBlockedUntil, Time.time + clip.length + 0.6f);

            if (comms != null)
                comms.ShowSupervisorLine(VoiceLines.Caption(clip.name), _briefing);
        }
    }
}
