using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Dust moving across the ground, and the wind that carries it.
    ///
    /// <para>The scenes were completely still. Nothing moved unless the player moved it,
    /// which made a plain of rocks read as a diagram rather than a place, and made every
    /// pause feel like the game had frozen. A little motion in the background is the
    /// difference between waiting somewhere and waiting at nothing.</para>
    ///
    /// <para>Cubes on a slow drift, not a particle system. Particles here would mean a
    /// material, a shader variant and a renderer sorting pass for an effect that is a few
    /// dozen specks; this is the same idiom as the rest of the world and costs nothing.
    /// They wrap around a box centred on the camera, so the field follows the player and
    /// there is never an edge to it.</para>
    ///
    /// <para>Deliberately faint. Dust that catches the eye competes with the cable, the
    /// checkpoint posts and the rover's lamp, and all three of those are carrying meaning
    /// that this is not.</para>
    /// </summary>
    public class DriftingDust : MonoBehaviour
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private AudioSource windSource;
        [SerializeField] private AudioClip windLoop;

        [Header("Field")]
        [SerializeField] private int count = 60;
        [SerializeField] private Vector3 fieldSize = new Vector3(70f, 6f, 70f);
        [SerializeField] private float minSize = 0.06f;
        [SerializeField] private float maxSize = 0.20f;

        [Header("Drift")]
        [Tooltip("World direction the wind blows. Normalised on start.")]
        [SerializeField] private Vector3 windDirection = new Vector3(1f, 0.06f, 0.35f);

        [SerializeField] private float minSpeed = 1.4f;
        [SerializeField] private float maxSpeed = 4.2f;

        [Header("Look")]
        [SerializeField] private Color dustColour = new Color(0.55f, 0.52f, 0.48f, 1f);

        [Header("Wind")]
        [SerializeField] private float windVolume = 0.22f;

        private Transform[] _motes;
        private float[] _speeds;
        private Vector3 _wind;

        private void Start()
        {
            _wind = windDirection.sqrMagnitude < 0.0001f
                ? Vector3.right
                : windDirection.normalized;

            if (followTarget == null && Camera.main != null)
                followTarget = Camera.main.transform;

            Build();
            StartWind();
        }

        private void Build()
        {
            // One shared unlit material. Sixty renderers sharing one material batch
            // together; sixty materials would not.
            var material = new Material(Shader.Find("Unlit/Color")) { color = dustColour };

            _motes = new Transform[count];
            _speeds = new float[count];

            // Fixed seed. The dust should look the same every run, so a screenshot taken
            // to check something else is comparable with the last one.
            var rng = new System.Random(9137);

            for (var i = 0; i < count; i++)
            {
                var mote = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mote.name = $"Mote {i}";
                mote.transform.SetParent(transform);
                Destroy(mote.GetComponent<Collider>());
                mote.GetComponent<Renderer>().sharedMaterial = material;

                var size = Mathf.Lerp(minSize, maxSize, (float)rng.NextDouble());
                mote.transform.localScale = Vector3.one * size;
                mote.transform.rotation = Random.rotation;

                _motes[i] = mote.transform;
                _speeds[i] = Mathf.Lerp(minSpeed, maxSpeed, (float)rng.NextDouble());

                mote.transform.position = RandomPointInField(rng);
            }
        }

        private Vector3 RandomPointInField(System.Random rng)
        {
            var centre = followTarget != null ? followTarget.position : transform.position;

            return centre + new Vector3(
                ((float)rng.NextDouble() - 0.5f) * fieldSize.x,
                (float)rng.NextDouble() * fieldSize.y,
                ((float)rng.NextDouble() - 0.5f) * fieldSize.z);
        }

        private void StartWind()
        {
            if (windSource == null || windLoop == null)
                return;

            windSource.clip = windLoop;
            windSource.loop = true;
            windSource.spatialBlend = 0f;
            windSource.volume = windVolume * GameSettings.EffectsVolume;
            windSource.Play();
        }

        private void Update()
        {
            if (GamePause.IsPaused)
            {
                if (windSource != null)
                    windSource.volume = 0f;

                return;
            }

            if (windSource != null)
                windSource.volume = windVolume * GameSettings.EffectsVolume;

            if (_motes == null || followTarget == null)
                return;

            var centre = followTarget.position;
            var step = _wind * Time.deltaTime * GameSettings.GameSpeed;
            var half = fieldSize * 0.5f;

            for (var i = 0; i < _motes.Length; i++)
            {
                var mote = _motes[i];
                if (mote == null)
                    continue;

                mote.position += step * _speeds[i];

                // Wrap around the box rather than respawning, so the field has no edge and
                // no popping. A mote that leaves the right side re-enters on the left at
                // the same height.
                var local = mote.position - centre;

                if (local.x > half.x) mote.position -= new Vector3(fieldSize.x, 0f, 0f);
                else if (local.x < -half.x) mote.position += new Vector3(fieldSize.x, 0f, 0f);

                if (local.z > half.z) mote.position -= new Vector3(0f, 0f, fieldSize.z);
                else if (local.z < -half.z) mote.position += new Vector3(0f, 0f, fieldSize.z);

                if (local.y > fieldSize.y) mote.position -= new Vector3(0f, fieldSize.y, 0f);
                else if (local.y < 0f) mote.position += new Vector3(0f, fieldSize.y, 0f);
            }
        }
    }
}
