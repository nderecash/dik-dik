using System;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// A pad the rover has to come to rest on. Registers only when the rover is inside
    /// AND stopped, which is the whole skill of the slope level.
    ///
    /// You cannot register it by rolling through. On a downhill grade, with momentum and
    /// a 2.6 second delay on your word, stopping on a mark means saying "stop" well before
    /// you reach it and judging the coast. Turn the game speed down and the coast shrinks,
    /// which is the setting this level exists to teach.
    ///
    /// Nothing here can fail. Overshoot a mark and you roll on, or hit the wall past the
    /// last one and reverse back up to it. It costs time, never a life.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class StopMark : MonoBehaviour
    {
        [Header("Feedback")]
        [SerializeField] private Renderer pad;
        [SerializeField] private RoverLight roverLight;
        [SerializeField] private AudioSource pingSource;
        [SerializeField] private AudioClip pingClip;

        [SerializeField] private Color waitingColour = new Color(0.7f, 0.55f, 0.2f);
        [SerializeField] private Color doneColour = new Color(0.3f, 0.85f, 0.45f);

        /// <summary>Raised once, when the rover first comes to rest on this pad.</summary>
        public event Action<StopMark> Registered;

        public bool IsRegistered { get; private set; }

        private RoverController _roverInside;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            Paint(waitingColour);
        }

        private void OnTriggerEnter(Collider other)
        {
            var rover = other.GetComponentInParent<RoverController>();
            if (rover != null)
                _roverInside = rover;
        }

        private void OnTriggerExit(Collider other)
        {
            var rover = other.GetComponentInParent<RoverController>();
            if (rover != null && rover == _roverInside)
                _roverInside = null;
        }

        private void Update()
        {
            if (IsRegistered || _roverInside == null)
                return;

            // Rest, not passage. The rover has to actually stop on the pad.
            if (_roverInside.IsMoving)
                return;

            Register();
        }

        private void Register()
        {
            IsRegistered = true;
            Paint(doneColour);

            if (pingSource != null && pingClip != null)
                pingSource.PlayOneShot(pingClip);

            // Sound and light together, the standing rule.
            if (roverLight != null)
                roverLight.SignalJunction();

            Registered?.Invoke(this);
        }

        private void Paint(Color colour)
        {
            if (pad != null)
                pad.material.color = colour;
        }

        /// <summary>Reset for a fresh rehearsal run.</summary>
        public void ResetMark()
        {
            IsRegistered = false;
            _roverInside = null;
            Paint(waitingColour);
        }
    }
}
