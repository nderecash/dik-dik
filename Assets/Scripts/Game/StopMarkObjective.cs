using UnityEngine;

namespace Dikdik.Game
{
    /// <summary>
    /// Completes the level once every stop mark has been registered.
    ///
    /// Kept separate from the marks themselves so a level's win condition lives in one
    /// obvious place rather than being smeared across the objects that make it up.
    /// </summary>
    public class StopMarkObjective : MonoBehaviour
    {
        [SerializeField] private LevelDirector director;
        [SerializeField] private StopMark[] marks;

        private int _remaining;

        private void Start()
        {
            if (marks == null || marks.Length == 0)
                marks = FindObjectsByType<StopMark>(FindObjectsInactive.Exclude);

            _remaining = marks.Length;

            foreach (var mark in marks)
                mark.Registered += OnRegistered;
        }

        private void OnDestroy()
        {
            if (marks == null)
                return;

            foreach (var mark in marks)
                if (mark != null)
                    mark.Registered -= OnRegistered;
        }

        private void OnRegistered(StopMark mark)
        {
            _remaining--;
            if (_remaining <= 0 && director != null)
                director.Complete();
        }
    }
}
