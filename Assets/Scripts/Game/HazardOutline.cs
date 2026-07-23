using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Gives a hazard a visible edge when high contrast is on.
    ///
    /// <para>Level 3 exists because of an assumption baked into this game's art. Every
    /// other level is black shapes against a bright sky, and they read cleanly because
    /// the sky does the work. On the night side there is no sky glow and nothing behind
    /// anything: shapes stop being shapes, and a gap in the floor looks exactly like
    /// floor.</para>
    ///
    /// <para>High contrast puts the edges back, artificially, because the sky is no
    /// longer providing them. That is the whole argument of the game rendered in light
    /// instead of speech, and nobody has to say it out loud.</para>
    ///
    /// <para>The level stays completable with this off. Slower, more rehearsal runs, and
    /// genuinely unpleasant, which is the honest version of the experience rather than a
    /// gate.</para>
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class HazardOutline : MonoBehaviour
    {
        [SerializeField] private Material outlineMaterial;
        [SerializeField] private Color colour = new Color(1f, 0.85f, 0.3f);
        [SerializeField] private float width = 0.12f;

        [Tooltip("Show the outline even without high contrast. For hazards that should " +
                 "always be legible regardless of settings.")]
        [SerializeField] private bool alwaysOn;

        private GameObject _outline;

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= Apply;
        }

        private void Build()
        {
            if (_outline != null || outlineMaterial == null)
                return;

            var mesh = GetComponent<MeshFilter>().sharedMesh;
            if (mesh == null)
                return;

            _outline = new GameObject("Outline");
            _outline.transform.SetParent(transform, false);

            _outline.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = _outline.AddComponent<MeshRenderer>();

            // Instanced so each hazard can be tinted separately without repainting
            // every other object sharing the material.
            var material = new Material(outlineMaterial);
            material.SetColor("_Color", colour);
            material.SetFloat("_Width", width);
            renderer.sharedMaterial = material;

            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void Apply()
        {
            if (_outline == null)
                Build();

            if (_outline != null)
                _outline.SetActive(alwaysOn || GameSettings.HighContrast);
        }
    }
}
