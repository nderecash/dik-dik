using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Something the rover should not drive into. Reaching it aborts the rehearsal run.
    ///
    /// It costs nothing. There is no failure state, no counter and no penalty: the sim is
    /// discarded and you take it again. What a hazard actually costs is time and the mild
    /// indignity of having driven a rover into a hole, which is enough.
    ///
    /// On the night side these are almost invisible without high contrast, which is the
    /// entire point of the level. Note "almost": Salty's lamp will pick one out at close
    /// range if you turn and look. Without the setting you are slower, not blind, and
    /// certainly not guessing. A level where the accessibility option is the difference
    /// between seeing and rolling dice would be a worse argument, not a better one.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Hazard : MonoBehaviour
    {
        [SerializeField] private SimulationReset simulation;

        [Tooltip("Shown on the console when the rover drives in. Never blames the player.")]
        [SerializeField] private string message = "That is a hole. Sim aborted.";

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<RoverController>() == null)
                return;

            if (simulation != null)
                simulation.Abort();
        }

        public string Message => message;
    }
}
