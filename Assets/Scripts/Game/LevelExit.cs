using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// The way out. Reaching it finishes the level.
    ///
    /// Deliberately a plain trigger with no conditions attached. There is no failure
    /// state in this game, so there is nothing to check on the way past: no score, no
    /// time, no minimum of junctions visited. You got the rover here, which was the
    /// whole task.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class LevelExit : MonoBehaviour
    {
        [SerializeField] private LevelDirector director;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<RoverController>() == null)
                return;

            if (director != null)
                director.Complete();
        }
    }
}
