using UnityEngine;

namespace Dikdik.Game.Cable
{
    /// <summary>
    /// The cable you can see, and the running record of how much of it has been checked.
    ///
    /// <para>Two coincident strips make up the line: a dark lit body so it reads as a real
    /// object lying on the ground, and a thinner unlit core on top so it is visible without
    /// depending on the light in the scene. That second strip is why the cable does not
    /// need the high contrast setting to be found. An object the player must turn on an
    /// accessibility option to see would invert the argument this whole game is making.</para>
    ///
    /// <para>Sections repaint from dim to bright as they are cleared, so the line behind
    /// the rover is lit and the line ahead is not. That is the progress bar, drawn on the
    /// world instead of on the HUD, and it is the one the player is already looking at.</para>
    ///
    /// <para>Built from cubes rather than a LineRenderer. A LineRenderer is a camera-facing
    /// billboard, and at this camera's grazing angle a billboard lying on the ground
    /// shimmers and thins out as it recedes. A box is a box from every angle.</para>
    /// </summary>
    public class CableVisual : MonoBehaviour, IResettable
    {
        [Tooltip("One transform per section, in order along the cable. Everything under " +
                 "each is repainted together when that section is cleared.")]
        [SerializeField] private Transform[] sections = new Transform[0];

        [SerializeField] private Color unscannedColour = new Color(0.22f, 0.34f, 0.40f);
        [SerializeField] private Color scannedColour = new Color(0.45f, 0.95f, 1f);
        [SerializeField] private Color faultColour = new Color(1f, 0.38f, 0.32f);

        [Header("Pulse")]
        [Tooltip("The unscanned line breathes slightly, so it reads as powered and " +
                 "waiting rather than as painted scenery.")]
        [SerializeField] private float pulseSeconds = 2.6f;

        [SerializeField] private float pulseDepth = 0.18f;

        private MaterialPropertyBlock _block;
        private bool[] _scanned;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            _scanned = new bool[sections.Length];

            for (var i = 0; i < sections.Length; i++)
                Paint(i, unscannedColour);
        }

        /// <summary>Light up the stretch leading to a checkpoint that has just reported.</summary>
        public void MarkSectionScanned(int index, bool fault)
        {
            if (sections == null || index < 0 || index >= sections.Length)
                return;

            _scanned[index] = true;
            Paint(index, fault ? faultColour : scannedColour);
        }

        private void Update()
        {
            if (GamePause.IsPaused || sections == null || sections.Length == 0)
                return;

            // Unscanned sections only. A cleared section holds its colour steady, because
            // "done" should look settled and "not done yet" should look alive.
            var breath = 1f + Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / pulseSeconds)) * pulseDepth;

            for (var i = 0; i < sections.Length; i++)
            {
                if (_scanned[i])
                    continue;

                Paint(i, unscannedColour * breath);
            }
        }

        private void Paint(int index, Color colour)
        {
            var section = sections[index];
            if (section == null)
                return;

            foreach (var renderer in section.GetComponentsInChildren<Renderer>())
            {
                // Only the core strip carries the signal. The body stays dark rock so the
                // cable still looks like a cable and not a strip light.
                if (!renderer.gameObject.name.StartsWith("Core"))
                    continue;

                renderer.GetPropertyBlock(_block);
                _block.SetColor("_Color", colour);
                _block.SetColor("_BaseColor", colour);
                renderer.SetPropertyBlock(_block);
            }
        }

        /// <summary>Put every section back to unscanned for a fresh rehearsal run.</summary>
        public void ResetForSimulation()
        {
            if (sections == null)
                return;

            for (var i = 0; i < sections.Length; i++)
            {
                _scanned[i] = false;
                Paint(i, unscannedColour);
            }
        }
    }
}
