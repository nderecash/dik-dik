using System.Collections;
using System.Collections.Generic;
using Dikdik.Game.Voice;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The opening. Stars, then a planet, then down through the sky to the rover sitting
    /// where the game starts, all of it under Control's briefing.
    ///
    /// <para><b>It has no timer.</b> Every camera move and every highlight is driven off
    /// <see cref="VoiceArbiter.LineStarted"/>, so the visuals cannot drift out of step with
    /// the audio no matter how long a recorded line turns out to be. A cinematic with its
    /// own schedule accumulates error across nine lines, and it also breaks the moment
    /// somebody skips, which they will. This one survives a skip for free: the arbiter
    /// stops firing line events, the sequence-finished event lands, and the camera goes
    /// where it was always going to end up.</para>
    ///
    /// <para><b>One camera.</b> This project is on the built-in render pipeline, confirmed
    /// on disk rather than assumed, so there is no camera stacking to do and no second
    /// camera to keep in sync. The cinematic borrows the level's own camera, drives it, and
    /// hands it back to <see cref="CameraFollow"/> at the end. The final keyframe is
    /// exactly where CameraFollow would put it, so the handover is invisible.</para>
    ///
    /// <para><b>The sky is the existing skybox.</b> Two new shader properties do the work:
    /// one darkens toward space, one puts stars below the horizon where in normal play
    /// there is no horizon to be below. A second sky shader would have been a second thing
    /// to keep looking like the first.</para>
    ///
    /// <para>The material is instanced in Awake. Animating the loaded asset directly leaves
    /// permanent edits in Assets/Materials every time the game runs in the editor, which is
    /// the sort of thing that shows up as an unexplained diff a week later.</para>
    /// </summary>
    public class IntroCinematic : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private Camera cinematicCamera;
        [SerializeField] private CameraFollow follow;
        [SerializeField] private Transform rover;
        [SerializeField] private Material skyAsset;

        [Header("Shot")]
        [Tooltip("Where the camera starts: far out, above the plane of the ground.")]
        [SerializeField] private Vector3 spaceStart = new Vector3(-60f, 210f, -190f);

        [Tooltip("Halfway point, closing on the planet.")]
        [SerializeField] private Vector3 approach = new Vector3(-24f, 92f, -96f);

        [Tooltip("Just above the site, before the last drop into the follow position.")]
        [SerializeField] private Vector3 arrival = new Vector3(-6f, 26f, -34f);

        [Header("Planet")]
        [SerializeField] private float planetRadius = 46f;
        [SerializeField] private Color planetColour = new Color(0.42f, 0.35f, 0.30f);

        [Header("Feel")]
        [Tooltip("Seconds to fade up from black at the very start.")]
        [SerializeField] private float fadeSeconds = 0.6f;

        [Tooltip("How quickly the camera eases toward the current keyframe. Low is slow " +
                 "and floaty, which is what a descent from orbit should feel like.")]
        [SerializeField] private float easing = 0.9f;

        [Tooltip("Safety net. However the briefing goes, the cinematic ends by this many " +
                 "seconds and hands the camera back.")]
        [SerializeField] private float maximumSeconds = 75f;

        private Material _sky;
        private Material _previousSky;
        private GameObject _planet;
        private Texture2D _fadeTexture;
        private float _fade = 1f;
        private bool _running;

        private Vector3 _targetPosition;
        private Vector3 _lookAt;
        private int _beat;

        /// <summary>The highlights, in the order the briefing names the things they point at.</summary>
        private readonly List<GameObject> _brackets = new List<GameObject>();

        private void Awake()
        {
            // Instanced, never the asset. See the class comment.
            if (skyAsset != null)
            {
                _previousSky = RenderSettings.skybox;
                _sky = new Material(skyAsset);
                RenderSettings.skybox = _sky;
            }

            _fadeTexture = new Texture2D(1, 1);
            _fadeTexture.SetPixel(0, 0, Color.black);
            _fadeTexture.Apply();
        }

        private void OnDestroy()
        {
            // Put the shared skybox back, or every scene after this one inherits a sky
            // that a cinematic left halfway to space.
            if (_previousSky != null)
                RenderSettings.skybox = _previousSky;
        }

        private IEnumerator Start()
        {
            if (cinematicCamera == null || rover == null)
            {
                Finish();
                yield break;
            }

            _running = true;

            if (follow != null)
                follow.enabled = false;

            BuildPlanet();
            BuildBrackets();

            _targetPosition = spaceStart;
            cinematicCamera.transform.position = spaceStart;
            _lookAt = Vector3.zero;

            if (_sky != null)
            {
                _sky.SetFloat("_SpaceBlend", 1f);
                _sky.SetFloat("_StarsEverywhere", 1f);
            }

            // Subscribe before yielding, not after. SupervisorVoice starts the briefing
            // when the level's director comes up, which is the same frame as this, and two
            // frames of waiting is easily enough to miss the first line and start the shot
            // a beat behind the words.
            var arbiter = VoiceArbiter.Instance;
            if (arbiter != null)
            {
                arbiter.LineStarted += OnLineStarted;
                arbiter.SequenceFinished += OnSequenceFinished;
            }

            // Two frames before anything moves, so the new shader variant finishes
            // compiling behind a black screen rather than as a hitch on the first move.
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            StartCoroutine(FadeUp());
            StartCoroutine(Deadline());
        }

        /// <summary>
        /// Each spoken line advances the shot by one beat. Nine lines, four positions: the
        /// camera holds between moves rather than crawling continuously, which reads as
        /// deliberate rather than as drifting.
        /// </summary>
        private void OnLineStarted(SpeechLine line)
        {
            if (!_running)
                return;

            _beat++;

            switch (_beat)
            {
                case 2:
                    _targetPosition = approach;
                    break;

                case 4:
                    _targetPosition = arrival;
                    // Out of space and into a sky. This is the descent.
                    StartCoroutine(BlendSky(1f, 0f, 6f));
                    break;

                case 6:
                    _targetPosition = FollowPosition();
                    break;
            }

            // Highlight things as they are named. Line 6 mentions the rover, line 8 the
            // line it has to follow. Indices are one behind the beat because the beat has
            // already been incremented.
            if (_beat == 6 && _brackets.Count > 0)
                StartCoroutine(ShowBracket(_brackets[0]));
            else if (_beat == 8 && _brackets.Count > 1)
                StartCoroutine(ShowBracket(_brackets[1]));
        }

        private void OnSequenceFinished()
        {
            Finish();
        }

        private IEnumerator Deadline()
        {
            var elapsed = 0f;
            while (_running && elapsed < maximumSeconds)
            {
                if (!GamePause.IsPaused)
                    elapsed += Time.unscaledDeltaTime;

                yield return null;
            }

            if (_running)
                Finish();
        }

        private void Update()
        {
            if (!_running || cinematicCamera == null || GamePause.IsPaused)
                return;

            // Exponential ease toward the current keyframe. Frame-rate independent, so the
            // shot looks the same on a fast machine and a slow one.
            var t = 1f - Mathf.Exp(-easing * Time.unscaledDeltaTime);

            cinematicCamera.transform.position =
                Vector3.Lerp(cinematicCamera.transform.position, _targetPosition, t);

            var toward = _lookAt - cinematicCamera.transform.position;
            if (toward.sqrMagnitude > 0.001f)
            {
                var wanted = Quaternion.LookRotation(toward.normalized, Vector3.up);
                cinematicCamera.transform.rotation =
                    Quaternion.Slerp(cinematicCamera.transform.rotation, wanted, t);
            }

            // Look at the planet early and the landing site later, crossing over as the
            // camera comes down.
            var height = cinematicCamera.transform.position.y;
            _lookAt = height > 60f ? Vector3.zero : rover.position + Vector3.up * 2f;
        }

        private Vector3 FollowPosition()
        {
            // Exactly where CameraFollow will want the camera, so the handover is a
            // continuation rather than a cut.
            return rover.TransformPoint(new Vector3(0f, 6.5f, -12f));
        }

        private void BuildPlanet()
        {
            _planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _planet.name = "Planet";
            Destroy(_planet.GetComponent<Collider>());

            // Below the ground plane and enormous, so from far out the camera sees a
            // curved lit body and from close up it is simply gone behind the terrain.
            _planet.transform.position = new Vector3(0f, -planetRadius - 1.5f, 40f);
            _planet.transform.localScale = Vector3.one * (planetRadius * 2f);

            var material = new Material(Shader.Find("Standard")) { color = planetColour };
            material.SetFloat("_Glossiness", 0.05f);
            _planet.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>
        /// Corner brackets around a thing being named, built from four thin cubes.
        ///
        /// <para>Not the HazardOutline shader, for two reasons. It requires a MeshFilter
        /// and works on one shared mesh, and the things being highlighted here are
        /// multi-part hierarchies. And it already means "hazard" in Level 3, so reusing it
        /// would teach the player a signal in the opening minute and contradict it an hour
        /// later.</para>
        /// </summary>
        private void BuildBrackets()
        {
            _brackets.Add(MakeBracket(rover.position + Vector3.up * 1.2f, 2.6f));

            // Roughly where the cable leaves the start, which is what the briefing is
            // pointing at when it says to follow the line.
            _brackets.Add(MakeBracket(rover.position + rover.forward * 14f + Vector3.up * 0.6f, 3.4f));
        }

        private GameObject MakeBracket(Vector3 centre, float size)
        {
            var root = new GameObject("Highlight");
            root.transform.position = centre;

            var material = new Material(Shader.Find("Unlit/Color"))
            {
                color = new Color(1f, 0.85f, 0.4f)
            };

            var half = size * 0.5f;
            var arm = size * 0.35f;
            var thickness = 0.10f;

            // Four L-shaped corners rather than a closed box: a box hides what it is
            // pointing at, and corners read as "look here" in a way a rectangle does not.
            foreach (var sx in new[] { -1f, 1f })
            {
                foreach (var sy in new[] { -1f, 1f })
                {
                    MakeBar(root.transform, material,
                            new Vector3(sx * (half - arm * 0.5f), sy * half, 0f),
                            new Vector3(arm, thickness, thickness));

                    MakeBar(root.transform, material,
                            new Vector3(sx * half, sy * (half - arm * 0.5f), 0f),
                            new Vector3(thickness, arm, thickness));
                }
            }

            root.SetActive(false);
            return root;
        }

        private static void MakeBar(Transform parent, Material material, Vector3 position, Vector3 scale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = position;
            bar.transform.localScale = scale;
            bar.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(bar.GetComponent<Collider>());
        }

        private IEnumerator ShowBracket(GameObject bracket)
        {
            if (bracket == null)
                yield break;

            bracket.SetActive(true);

            // Face the camera, and keep facing it, so the corners never foreshorten into
            // a line as the camera comes down.
            var elapsed = 0f;
            while (elapsed < 4f)
            {
                if (!GamePause.IsPaused)
                {
                    elapsed += Time.unscaledDeltaTime;

                    if (cinematicCamera != null)
                        bracket.transform.rotation =
                            Quaternion.LookRotation(bracket.transform.position -
                                                    cinematicCamera.transform.position);
                }

                yield return null;
            }

            bracket.SetActive(false);
        }

        private IEnumerator BlendSky(float from, float to, float seconds)
        {
            if (_sky == null)
                yield break;

            var elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!GamePause.IsPaused)
                    elapsed += Time.unscaledDeltaTime;

                var t = Mathf.Clamp01(elapsed / seconds);
                var value = Mathf.Lerp(from, to, t);

                _sky.SetFloat("_SpaceBlend", value);
                _sky.SetFloat("_StarsEverywhere", value);

                // Ambient is Trilight with explicit colours, so it does not follow the
                // skybox on its own. Without this the ground stays lit for space while the
                // sky says otherwise.
                RenderSettings.ambientSkyColor = Color.Lerp(
                    new Color(0.10f, 0.11f, 0.16f), new Color(0.38f, 0.40f, 0.46f), 1f - value);

                yield return null;
            }
        }

        private IEnumerator FadeUp()
        {
            var elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _fade = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }

            _fade = 0f;
        }

        private void OnGUI()
        {
            if (_fade <= 0.001f)
                return;

            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, _fade);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _fadeTexture);
            GUI.color = previous;
        }

        /// <summary>Hand the camera back and get out of the way.</summary>
        private void Finish()
        {
            if (!_running)
                return;

            _running = false;

            var arbiter = VoiceArbiter.Instance;
            if (arbiter != null)
            {
                arbiter.LineStarted -= OnLineStarted;
                arbiter.SequenceFinished -= OnSequenceFinished;
            }

            if (_sky != null)
            {
                _sky.SetFloat("_SpaceBlend", 0f);
                _sky.SetFloat("_StarsEverywhere", 0f);
            }

            RenderSettings.ambientSkyColor = new Color(0.38f, 0.40f, 0.46f);

            if (_planet != null)
                Destroy(_planet);

            foreach (var bracket in _brackets)
                if (bracket != null)
                    Destroy(bracket);

            _brackets.Clear();

            if (follow != null)
            {
                follow.enabled = true;
                follow.SetTarget(rover);
            }
        }
    }
}
