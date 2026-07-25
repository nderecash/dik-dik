using Dikdik.Commands;
using Dikdik.Matching;
using Dikdik.Producers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dikdik.Game
{
    /// <summary>
    /// Everything that outlives a level, created once and carried across scene loads.
    ///
    /// The alternative was rebuilding the bus, the producers, the journal and the
    /// console in all six level scenes. That would mean six copies of the wiring, six
    /// chances for one of them to drift, and a voice journal that forgot everything the
    /// player said each time a level ended, which would quietly destroy the ending.
    ///
    /// So level scenes contain level content and nothing else.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Bootstrap : MonoBehaviour
    {
        public static Bootstrap Instance { get; private set; }

        [Header("Persistent systems")]
        [SerializeField] private CommandBus bus;
        [SerializeField] private VoiceJournal journal;
        [SerializeField] private CommsDisplay comms;

        [Header("First scene")]
        [SerializeField] private string firstScene = "Level01";

        [Tooltip("Load the first scene automatically. Off when testing a level directly.")]
        [SerializeField] private bool loadFirstScene = true;

        public CommandBus Bus => bus;
        public VoiceJournal Journal => journal;
        public CommsDisplay Comms => comms;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Statics survive scene loads and, in the editor, survive leaving play mode.
            // A pause left set by a previous run would soft-lock the next one.
            GamePause.Reset();

            GameSettings.Load();
            LoadVocabulary();
        }

        private const string VocabularyKey = "dikdik.taught";

        /// <summary>
        /// Bring the player's taught words back, and keep them saved.
        ///
        /// Deserialize first, then subscribe: the deserialize itself raises Changed, and
        /// we do not want to write the file back out during load. The matcher stays free
        /// of PlayerPrefs so it can be tested with plain dotnet; the persistence lives
        /// here, in Unity, where it belongs.
        /// </summary>
        private void LoadVocabulary()
        {
            IntentVocabulary.Deserialize(PlayerPrefs.GetString(VocabularyKey, string.Empty));
            IntentVocabulary.Changed += SaveVocabulary;
        }

        private void SaveVocabulary()
        {
            PlayerPrefs.SetString(VocabularyKey, IntentVocabulary.Serialize());
            PlayerPrefs.Save();
        }

        private void Start()
        {
            WireVoice();

            if (loadFirstScene && !string.IsNullOrWhiteSpace(firstScene))
                SceneManager.LoadScene(firstScene);
        }

        /// <summary>
        /// Connect the voice producer to the journal and the console.
        ///
        /// Done here rather than by dragging references in the inspector because the
        /// producer is compiled out of WebGL entirely. A serialized reference to a type
        /// that does not exist on a platform is a broken scene on that platform; a
        /// lookup that returns nothing is just a browser build with no microphone,
        /// which is what we actually want.
        /// </summary>
        private void WireVoice()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            var voice = FindAnyObjectByType<VoiceCommandProducer>();
            if (voice == null)
                return;

            if (journal != null)
                voice.UtteranceCaptured += journal.Capture;

            if (comms != null)
                voice.VoiceDetectedChanged += comms.SetListening;

            if (GameSettings.VoiceEnabled)
                voice.StartListening();
#endif
        }

        /// <summary>True when this build can listen at all. The settings screen reads it.</summary>
        public bool VoiceAvailable
        {
            get
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                var voice = FindAnyObjectByType<VoiceCommandProducer>();
                return voice != null && voice.IsAvailable;
#else
                return false;
#endif
            }
        }
    }
}
