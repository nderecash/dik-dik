using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dikdik.Commands
{
    /// <summary>
    /// The one place player commands arrive, from any producer, and the transport that
    /// carries them to the rover.
    ///
    /// Producers register themselves. Gameplay listens to <see cref="CommandIssued"/>
    /// and never talks to a producer directly. Adding a third way to play later, a
    /// gamepad, a switch, an eye tracker, means writing one producer and changing
    /// nothing else in the game.
    ///
    /// <para><b>Transport delay.</b> Commands are held for <see cref="TransportDelay"/>
    /// seconds measured from <see cref="Intent.CreatedAt"/>, which is when the player
    /// finished giving them rather than when we finished understanding them.</para>
    ///
    /// <para>This is not decoration and it is not a difficulty setting. Speech takes
    /// about 1.9 seconds to transcribe and a key press takes none. Without a delay
    /// measured from the person, the keyboard would beat the voice to the rover every
    /// single time, and every claim this project makes about neither way of playing
    /// being privileged would be false in the one place it actually counts. So both
    /// wait until the same moment. Voice has already spent most of its budget in
    /// whisper; the keyboard spends all of its waiting.</para>
    ///
    /// <para>The fiction is true to it as well: mission control's typed command uplinks
    /// crossed the same distance at the same speed as the voice loop.</para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CommandBus : MonoBehaviour
    {
        public static CommandBus Instance { get; private set; }

        /// <summary>Raised for commands we understood, after the transport delay.</summary>
        public event Action<Intent> CommandIssued;

        /// <summary>
        /// Raised for input we heard but could not place, after the transport delay.
        /// The feedback panel shows these too. Silence after someone speaks reads as
        /// being ignored, which is the exact feeling this game is about.
        /// </summary>
        public event Action<Intent> CommandNotUnderstood;

        /// <summary>
        /// Raised the instant a command is accepted, before any delay. Feedback uses
        /// this: the console should confirm it heard you immediately, even though the
        /// rover cannot possibly have moved yet. Confirming receipt and confirming
        /// action are different promises and should not be made at the same time.
        /// </summary>
        public event Action<Intent> CommandAccepted;

        [Tooltip("Seconds from the player finishing input to the rover acting. " +
                 "2.6 seconds. It began as round-trip light time to the Moon, which is " +
                 "about 2.56s. The story is no longer set on the Moon and the number " +
                 "stayed, because what it is really doing is holding voice and keyboard " +
                 "level with each other: speech spends most of that budget inside whisper, " +
                 "so a key press has to wait the same total or it silently wins every race.")]
        [SerializeField] private float transportDelay = 2.6f;

        public float TransportDelay
        {
            get => transportDelay;
            set => transportDelay = Mathf.Max(0f, value);
        }

        private readonly List<ICommandProducer> _producers = new List<ICommandProducer>();
        private readonly List<Intent> _inTransit = new List<Intent>();

        public IReadOnlyList<ICommandProducer> Producers => _producers;

        /// <summary>How many commands are currently crossing the gap. Drives the indicator.</summary>
        public int InTransitCount => _inTransit.Count;

        /// <summary>
        /// How far along the earliest in-flight command is, 0 to 1. Returns 1 when
        /// nothing is in transit. The signal indicator reads this instead of a spinner:
        /// a spinner says the software is struggling, a travelling signal says the rover
        /// is a long way away, and only one of those is true.
        /// </summary>
        public float TransitProgress
        {
            get
            {
                if (_inTransit.Count == 0 || transportDelay <= 0f)
                    return 1f;

                var elapsed = Clock - _inTransit[0].CreatedAt;
                return Mathf.Clamp01(elapsed / transportDelay);
            }
        }

        /// <summary>
        /// The clock everything to do with the transport delay is measured against.
        ///
        /// <para>Not <c>Time.time</c>, for one specific reason: it must stop while the game
        /// is paused. A command sent just before the player opens the settings screen has
        /// to have the same distance left to travel when they close it. Measuring against
        /// wall-clock time would mean that pausing for ten seconds delivers every queued
        /// command the instant you unpause, which is both wrong and startling.</para>
        ///
        /// <para>Unscaled, so it is unaffected by the game speed setting. That split is
        /// deliberate: the world slows down, the distance to the rover does not.</para>
        /// </summary>
        public static float Clock { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            for (var i = _producers.Count - 1; i >= 0; i--)
                Unregister(_producers[i]);

            if (Instance == this)
                Instance = null;
        }

        public void Register(ICommandProducer producer)
        {
            if (producer == null || _producers.Contains(producer))
                return;

            _producers.Add(producer);
            producer.CommandProduced += OnCommandProduced;
        }

        public void Unregister(ICommandProducer producer)
        {
            if (producer == null || !_producers.Remove(producer))
                return;

            producer.CommandProduced -= OnCommandProduced;
        }

        /// <summary>
        /// Restrict what the rover will act on, set by the current level. Null means
        /// everything is allowed.
        ///
        /// A command outside the set is reported as not understood rather than quietly
        /// dropped. Being ignored and being misunderstood feel identical from the
        /// player's chair, and only one of them is honest here.
        /// </summary>
        public IReadOnlyList<IntentId> AllowedIntents { get; set; }

        /// <summary>
        /// Always available, whatever the level says. Asking for help, asking the rover
        /// to repeat itself, or asking to run the sim again are not level content and
        /// must never be switched off by one.
        /// </summary>
        private static readonly IntentId[] AlwaysAllowed =
        {
            IntentId.Help, IntentId.Repeat, IntentId.Restart,

            // Back, everywhere, always.
            //
            // It was in the allowed list for two levels out of six, which meant that in the
            // other four the answer to "back up" was "I did not understand". A playtest
            // found the consequence in the dark level: the rover noses into a wall, the
            // player says back, is told the word means nothing, turns instead, and the turn
            // carries them sideways into a hazard.
            //
            // Reversing out of a mistake is not level content. It is the thing that makes a
            // game with no failure state true rather than merely claimed, and a level that
            // switches it off is a level that can strand you.
            IntentId.Back,

            // The delight commands, everywhere, always. An easter egg that works in two
            // levels out of six is not an easter egg, it is a bug that some players will
            // find and reasonably report. And the reply to "can you jump" being "I did not
            // understand that" in exactly the levels where nobody thought to allow it is
            // the single worst thing this game could say.
            IntentId.Jump, IntentId.Spin, IntentId.Dance, IntentId.Greet, IntentId.Who,

            // The blockage answers. They belong to one beat in two levels, and a level
            // that forgot to allow them would report the words its own prompt just named
            // as not understood.
            IntentId.Cut, IntentId.Dissolve, IntentId.Push,

            // Repair. The last command in the game, and the one nothing may switch off.
            IntentId.Repair
        };

        /// <summary>
        /// Raised when a new command replaced one that was still crossing the gap, with
        /// the command that was dropped. The panel says so out loud, because a command
        /// that vanishes without explanation is the thing this game may never do.
        /// </summary>
        public event Action<Intent> CommandReplaced;

        private void OnCommandProduced(Intent produced)
        {
            var intent = Permit(produced);

            if (transportDelay <= 0f)
            {
                CommandAccepted?.Invoke(intent);
                Deliver(intent);
                return;
            }

            // Clear the gap BEFORE announcing the new command, so listeners already know
            // what is being replaced when they are told what was accepted. The other
            // order leaves the panel saying "Sending" and then learning, too late to
            // draw it, that something was dropped.
            //
            // One command in flight, ever. The newest replaces whatever was still on its
            // way.
            //
            // This is what makes talking over yourself work. Under a 2.6 second delay you
            // will say "left", realise immediately it should have been right, and say
            // "right" while the first one is still travelling. Queueing both would turn
            // that correction into two turns, which is the opposite of what the player
            // meant and unarguably worse than obeying only the second.
            //
            // It also settles the "turn right turn right turn right" case, where somebody
            // repeats themselves because nothing has visibly happened yet. That is one
            // right turn. It was never a request for two hundred and seventy degrees.
            if (_inTransit.Count > 0)
            {
                foreach (var dropped in _inTransit)
                    CommandReplaced?.Invoke(dropped);

                _inTransit.Clear();
            }

            _inTransit.Add(intent);

            // Tell the player we have them straight away. The rover cannot move for
            // another couple of seconds and that is fine, but leaving someone wondering
            // whether the microphone even works is not.
            CommandAccepted?.Invoke(intent);
        }

        private void Update()
        {
            if (!Dikdik.Game.GamePause.IsPaused)
                Clock += Time.unscaledDeltaTime;

            if (_inTransit.Count == 0)
                return;

            var now = Clock;

            // Commands arrive in the order they were sent. A later command cannot
            // overtake an earlier one just because it was understood faster.
            while (_inTransit.Count > 0 && now - _inTransit[0].CreatedAt >= transportDelay)
            {
                var intent = _inTransit[0];
                _inTransit.RemoveAt(0);
                Deliver(intent);
            }
        }

        private void Deliver(Intent intent)
        {
            if (intent.IsRecognised)
                CommandIssued?.Invoke(intent);
            else
                CommandNotUnderstood?.Invoke(intent);
        }

        private Intent Permit(Intent intent)
        {
            if (!intent.IsRecognised || AllowedIntents == null)
                return intent;

            for (var i = 0; i < AlwaysAllowed.Length; i++)
                if (AlwaysAllowed[i] == intent.Id)
                    return intent;

            for (var i = 0; i < AllowedIntents.Count; i++)
                if (AllowedIntents[i] == intent.Id)
                    return intent;

            // Keep the raw text. The player still said something, and the panel should
            // show it back to them rather than pretending the room was silent.
            return Intent.Unrecognised(intent.Source, intent.RawText, intent.CreatedAt);
        }

        /// <summary>Drop anything still crossing the gap. Used when a level resets.</summary>
        public void ClearInTransit()
        {
            _inTransit.Clear();
        }

        /// <summary>
        /// For cutscenes and tests. Marked <see cref="CommandSource.Script"/> so the
        /// spike log can tell scripted commands apart from real player input, and
        /// delivered immediately because nothing scripted is crossing any distance.
        /// </summary>
        public void IssueScripted(IntentId id)
        {
            Deliver(new Intent(id, CommandSource.Script, id.ToString(), 1f, Clock));
        }
    }
}
