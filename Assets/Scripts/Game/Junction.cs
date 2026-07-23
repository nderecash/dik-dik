using System;
using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// A place where the corridor branches, and where Salty tells you it has arrived.
    ///
    /// The announcement is the whole teaching mechanism of Level 1. It happens in two
    /// channels at once, always: a tone, and a pulse of light across the rover's shell.
    /// Neither carries anything the other does not. Turn the sound off and you lose
    /// nothing at all, which is the guideline this level exists to embody rather than
    /// merely satisfy.
    ///
    /// If you ever add a third thing a junction communicates, it needs a home in both
    /// channels or it is not finished.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Junction : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Order along the corridor. Only used for logging and level scripting.")]
        [SerializeField] private int index;

        [Tooltip("Announce once, or every time the rover passes through")]
        [SerializeField] private bool announceOnce = true;

        [Header("Feedback")]
        [Tooltip("Left empty, the rover's own light handles the visual half")]
        [SerializeField] private RoverLight roverLight;

        [SerializeField] private AudioSource pingSource;
        [SerializeField] private AudioClip pingClip;

        [Tooltip("Marker geometry that lights up. A second visual channel for anyone " +
                 "not watching the rover itself.")]
        [SerializeField] private Renderer marker;

        [SerializeField] private Color restingColour = new Color(0.15f, 0.16f, 0.2f);
        [SerializeField] private Color reachedColour = new Color(0.6f, 0.9f, 1f);

        /// <summary>Raised when the rover first arrives. Levels use it for progress.</summary>
        public event Action<int> Reached;

        public int Index => index;
        public bool HasBeenReached { get; private set; }

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            Paint(restingColour);
        }

        [Header("Behaviour")]
        [Tooltip("Stop the rover on arrival and wait to be told which way. " +
                 "Without this, a 2.6 second delay means every corner is overshot.")]
        [SerializeField] private bool holdRoverOnArrival = true;

        private void OnTriggerEnter(Collider other)
        {
            var rover = other.GetComponentInParent<RoverController>();
            if (rover == null)
                return;

            if (announceOnce && HasBeenReached)
                return;

            Announce(rover);
        }

        private void Announce(RoverController rover)
        {
            HasBeenReached = true;

            // Stop first, announce second. The ping is then a question rather than a
            // notification: it says "I am here, which way", and the rover is genuinely
            // waiting for the answer instead of rolling on while you decide.
            if (holdRoverOnArrival && rover != null)
                rover.HoldAtJunction();

            // Sound.
            if (pingSource != null && pingClip != null)
                pingSource.PlayOneShot(pingClip);

            // Light, at the same instant. Not a fallback, not an accessibility mode.
            // The same information, twice, for everyone.
            if (roverLight != null)
                roverLight.SignalJunction();

            Paint(reachedColour);

            Reached?.Invoke(index);
        }

        private void Paint(Color colour)
        {
            if (marker != null)
                marker.material.color = colour;
        }

        /// <summary>Put it back for a fresh rehearsal run.</summary>
        public void ResetJunction()
        {
            HasBeenReached = false;
            Paint(restingColour);
        }
    }
}
