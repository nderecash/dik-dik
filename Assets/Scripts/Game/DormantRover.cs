using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// One of the other rovers on the plain. Dark and still until the broadcast reaches
    /// it, then its light comes up.
    ///
    /// It is the same build as Salty, which is the quiet argument of the ending: they
    /// were never broken, they were only never addressed. Once the player's voice goes
    /// out on the open loop, every one of them has what it was always waiting for.
    /// </summary>
    public class DormantRover : MonoBehaviour
    {
        [SerializeField] private Renderer shell;
        [SerializeField] private Light lamp;

        [SerializeField] private Color wokenColour = new Color(0.75f, 0.85f, 1f);
        [SerializeField] private float wakeSeconds = 1.2f;
        [SerializeField] private float lampIntensity = 2.2f;

        private Material _shellMaterial;
        private bool _waking;
        private float _wakeProgress;

        public bool IsAwake { get; private set; }

        private void Awake()
        {
            if (shell != null)
                _shellMaterial = shell.material;

            SetGlow(0f);
        }

        /// <summary>Bring it to life. Idempotent.</summary>
        public void Wake()
        {
            if (IsAwake)
                return;

            IsAwake = true;
            _waking = true;
        }

        private void Update()
        {
            if (!_waking)
                return;

            _wakeProgress += Time.deltaTime / Mathf.Max(0.01f, wakeSeconds);
            SetGlow(Mathf.Clamp01(_wakeProgress));

            if (_wakeProgress >= 1f)
                _waking = false;
        }

        private void SetGlow(float t)
        {
            if (_shellMaterial != null)
                _shellMaterial.color = Color.Lerp(new Color(0.02f, 0.02f, 0.03f), wokenColour, t);

            if (lamp != null)
                lamp.intensity = Mathf.Lerp(0f, lampIntensity, t);
        }

        /// <summary>Back to dark, for a fresh run.</summary>
        public void Reset()
        {
            IsAwake = false;
            _waking = false;
            _wakeProgress = 0f;
            SetGlow(0f);
        }
    }
}
