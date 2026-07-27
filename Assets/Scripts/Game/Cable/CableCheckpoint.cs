using System.Collections;
using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game.Cable
{
    /// <summary>
    /// A place on the relay line where the rover stops and scans a section.
    ///
    /// <para>This is the game's core loop, and it is a loop of four beats: arrive, stop,
    /// scan, be told. Everything about the ordering here is deliberate.</para>
    ///
    /// <list type="number">
    /// <item><b>The rover stops itself.</b> It is never the player's job to land a stop on
    /// a mark, because under a 2.6 second transport delay that is a coin toss dressed up as
    /// a skill. Precision under latency is Level 5's argument and it gets to make it alone.</item>
    /// <item><b>The light and the marker change instantly.</b> Before any audio, before the
    /// sweep. The player knows they have arrived at the same moment the rover does.</item>
    /// <item><b>The sweep and the tone happen together</b>, never one without the other.</item>
    /// <item><b>Control reports.</b> Through the arbiter, so it queues behind anything
    /// already being said instead of talking over it.</item>
    /// </list>
    ///
    /// <para><b>Commands during a scan are held, not dropped.</b> A player who says "go"
    /// while the sweep is running gets it obeyed when the sweep ends, and sees it marked
    /// as waiting in the meantime. Swallowing an instruction in silence is the one failure
    /// this game may never commit, because the entire premise is a machine that listens.
    /// The hold is one deep and the newest wins, which matches how commands behave
    /// everywhere else: the rover always does the most recent thing it was told.</para>
    /// </summary>
    public class CableCheckpoint : MonoBehaviour, IResettable
    {
        [Header("Placement")]
        [Tooltip("Arc distance along the cable. Written by CableBuilder; used to sort " +
                 "checkpoints into the order the player meets them.")]
        [SerializeField] private float distanceAlongCable;

        [Tooltip("The break in the line. Exactly one of these exists in the whole game, on " +
                 "the last checkpoint of the last sector, and it reports a fault instead of " +
                 "a clean section.")]
        [SerializeField] private bool isFault;

        [Header("Wiring")]
        [SerializeField] private RoverController rover;
        [SerializeField] private RoverLight roverLight;
        [Tooltip("Parent of the ring and both posts. Everything under it repaints together.")]
        [SerializeField] private Transform marker;
        [SerializeField] private Transform sweepBar;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip scanTone;

        [Header("Timing")]
        [Tooltip("How long the sweep takes. Long enough to read as work being done, short " +
                 "enough that twenty of them across a playthrough is not a tax.")]
        [SerializeField] private float sweepSeconds = 1.4f;

        [SerializeField] private float sweepLength = 7f;

        [Header("Colours")]
        [SerializeField] private Color waitingColour = new Color(0.30f, 0.48f, 0.56f);
        [SerializeField] private Color scanningColour = new Color(1f, 0.62f, 0.18f);
        [SerializeField] private Color clearColour = new Color(0.4f, 0.95f, 0.6f);
        [SerializeField] private Color faultColour = new Color(1f, 0.35f, 0.3f);

        private MissionProgress _progress;
        private MaterialPropertyBlock _block;
        private Coroutine _running;

        /// <summary>Where this sits along the cable. Read by MissionProgress to order them.</summary>
        public float DistanceAlongCable => distanceAlongCable;

        /// <summary>True once its section has been reported on.</summary>
        public bool IsScanned { get; private set; }

        /// <summary>True while the sweep is running.</summary>
        public bool IsScanning { get; private set; }

        /// <summary>The break in the line. The last checkpoint of the last sector.</summary>
        public bool IsFault => isFault;

        /// <summary>Zero-based position along the cable, filled in by MissionProgress.</summary>
        public int Index { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Paint(waitingColour);

            if (sweepBar != null)
                sweepBar.gameObject.SetActive(false);
        }

        /// <summary>Called by MissionProgress once it has sorted every checkpoint into order.</summary>
        public void Bind(MissionProgress progress, int index)
        {
            _progress = progress;
            Index = index;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsScanned || IsScanning)
                return;

            // Only the rover. The trigger sits on the cable and plenty of scenery passes
            // near it, and a rock rolling through must not report a clean section.
            if (rover == null || other.attachedRigidbody == null ||
                other.attachedRigidbody.gameObject != rover.gameObject)
                return;

            _running = StartCoroutine(Scan());
        }

        private IEnumerator Scan()
        {
            IsScanning = true;

            // Beat 1: stop, and take the wheel off the player for the duration. The hold
            // keeps their commands rather than eating them.
            rover.BeginScanHold();

            // Beat 2: say so immediately, on both channels, before anything slow starts.
            Paint(scanningColour);
            if (roverLight != null)
                roverLight.SignalScanning();

            // Beat 3: sweep and tone, together.
            if (audioSource != null && scanTone != null)
                audioSource.PlayOneShot(scanTone);

            yield return Sweep();

            // Beat 4: the report. Queued behind whatever else is speaking, so a checkpoint
            // reached during a sector introduction waits its turn instead of colliding.
            IsScanned = true;
            IsScanning = false;
            Paint(isFault ? faultColour : clearColour);

            if (roverLight != null)
                roverLight.SignalScanComplete(isFault);

            var arbiter = VoiceArbiter.Instance;
            if (arbiter != null)
            {
                var group = isFault ? "fault" : "scan";
                yield return arbiter.SayGroup(group, SpeechPriority.Beat, Speaker.Control,
                                              essential: true);
            }

            // Beat 5: hand it back, and honour whatever they said while we were busy.
            rover.EndScanHold();

            if (_progress != null)
                _progress.ReportScanned(this);

            _running = null;
        }

        /// <summary>
        /// A bright bar travelling along the cable through the checkpoint.
        ///
        /// <para>A moving solid rather than a fading ring on purpose: this project renders
        /// with the built-in pipeline and no transparency in its material vocabulary, and a
        /// shape that moves is legible at this camera's grazing angle in a way an alpha
        /// gradient is not. It also survives high contrast unchanged, which a soft effect
        /// would not.</para>
        /// </summary>
        private IEnumerator Sweep()
        {
            if (sweepBar == null)
            {
                yield return new WaitForSeconds(sweepSeconds);
                yield break;
            }

            sweepBar.gameObject.SetActive(true);

            var forward = transform.forward;
            var start = transform.position - forward * (sweepLength * 0.5f);
            var end = transform.position + forward * (sweepLength * 0.5f);

            var elapsed = 0f;
            while (elapsed < sweepSeconds)
            {
                // Real seconds. A scan is the machine working, not the world moving, so it
                // takes the same time however the player has set the game speed.
                if (!GamePause.IsPaused)
                    elapsed += Time.unscaledDeltaTime;

                sweepBar.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / sweepSeconds));
                yield return null;
            }

            sweepBar.gameObject.SetActive(false);
        }

        private void Paint(Color colour)
        {
            if (marker == null)
                return;

            foreach (var renderer in marker.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(_block);

                // Built-in pipeline: Standard uses _Color, and so do the unlit marker
                // shaders in this project. Setting both is harmless and saves caring which
                // material a level happened to pick.
                _block.SetColor("_Color", colour);
                _block.SetColor("_BaseColor", colour);
                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>
        /// Put it back for a fresh rehearsal run.
        ///
        /// <para>Stopping the scan coroutine leaves whatever it had already done half done:
        /// the marker orange, the lamp orange, the sweep bar mid-flight. Each of those has
        /// to be undone here, because the code that would normally undo them is on the line
        /// after the yield that just got cancelled.</para>
        ///
        /// <para>The rover's own hold is released by RoverController.ResetTo rather than
        /// here, so that it happens exactly once no matter how many things were interrupted.</para>
        /// </summary>
        public void ResetForSimulation()
        {
            var wasScanning = IsScanning;

            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }

            IsScanned = false;
            IsScanning = false;
            Paint(waitingColour);

            // Only if this checkpoint is the one that left it orange. Releasing
            // unconditionally would clear a signal some other component had just set.
            if (wasScanning && roverLight != null)
                roverLight.Release();

            if (sweepBar != null)
                sweepBar.gameObject.SetActive(false);
        }
    }
}
