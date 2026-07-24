using Dikdik.Commands;
using Dikdik.Producers;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Jams the key currently bound to one command, for the length of a level.
    ///
    /// The fiction is a stuck control on the console. The physical key goes dead, and the
    /// only way through is to bind that command to a different key, or, if you are on
    /// voice, to keep going untouched. That last part is the point of the level: when one
    /// way in fails, the other carries you, and voice is the one still standing rather
    /// than the flaky option it is usually cast as.
    ///
    /// Reaches into the persistent keyboard producer on start and lets go on the way out,
    /// so the jam never leaks into another level.
    /// </summary>
    public class KeyJam : MonoBehaviour
    {
        [Tooltip("The command whose key is stuck")]
        [SerializeField] private IntentId jammedIntent = IntentId.Open;

        private KeyboardCommandProducer _producer;

        private void Start()
        {
            _producer = FindAnyObjectByType<KeyboardCommandProducer>();
            if (_producer == null)
                return;

            var stuck = _producer.KeyFor(jammedIntent);
            _producer.SetJammedKey(stuck);
        }

        private void OnDisable()
        {
            if (_producer != null)
                _producer.ClearJam();
        }
    }
}
