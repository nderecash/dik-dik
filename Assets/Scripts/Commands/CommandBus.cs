using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dikdik.Commands
{
    /// <summary>
    /// The one place player commands arrive, from any producer.
    ///
    /// Producers register themselves. Gameplay listens to <see cref="CommandIssued"/>
    /// and never talks to a producer directly. Adding a third way to play later,
    /// a gamepad, a switch, an eye tracker, means writing one producer and
    /// changing nothing else in the game.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CommandBus : MonoBehaviour
    {
        public static CommandBus Instance { get; private set; }

        /// <summary>Raised for commands we understood.</summary>
        public event Action<Intent> CommandIssued;

        /// <summary>
        /// Raised for input we heard but could not place.
        /// The feedback panel shows these too. Silence after someone speaks
        /// reads as being ignored, which is the exact feeling this game is about.
        /// </summary>
        public event Action<Intent> CommandNotUnderstood;

        private readonly List<ICommandProducer> _producers = new List<ICommandProducer>();

        public IReadOnlyList<ICommandProducer> Producers => _producers;

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

        private void OnCommandProduced(Intent intent)
        {
            if (intent.IsRecognised)
                CommandIssued?.Invoke(intent);
            else
                CommandNotUnderstood?.Invoke(intent);
        }

        /// <summary>
        /// For cutscenes and tests. Marked <see cref="CommandSource.Script"/> so the
        /// spike log can tell scripted commands apart from real player input.
        /// </summary>
        public void IssueScripted(IntentId id)
        {
            OnCommandProduced(new Intent(id, CommandSource.Script, id.ToString()));
        }
    }
}
