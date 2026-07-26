using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dikdik.Game.Cable
{
    /// <summary>
    /// How much of this stretch of relay line has been checked, and how much is left.
    ///
    /// <para>The whole reason this class exists is the first playtest note: there was no
    /// stated mission and no sense of progress. A player could drive competently for five
    /// minutes with no idea whether they were nearly done or had not started. Distance
    /// remaining and checkpoints scanned are the two numbers that fix that, and everything
    /// here exists to produce them honestly.</para>
    ///
    /// <para>Checkpoints are found rather than registered. Registration means every
    /// checkpoint has to run before this does, and Unity does not promise that; finding
    /// them in Start and sorting by distance along the cable gives the same answer without
    /// depending on an ordering nobody controls.</para>
    /// </summary>
    public class MissionProgress : MonoBehaviour
    {
        [SerializeField] private CablePath path;
        [SerializeField] private CableVisual visual;
        [SerializeField] private Transform rover;
        [SerializeField] private LevelDirector director;

        [Tooltip("Complete the level once every checkpoint is scanned. On for every level: " +
                 "the mission is the scan, so finishing the scan is finishing the level.")]
        [SerializeField] private bool completeLevelWhenScanned = true;

        private readonly List<CableCheckpoint> _checkpoints = new List<CableCheckpoint>();

        /// <summary>Raised whenever a number the HUD shows has changed.</summary>
        public event Action Changed;

        public int TotalCheckpoints => _checkpoints.Count;
        public int ScannedCheckpoints { get; private set; }
        public CablePath Path => path;

        /// <summary>The checkpoint the rover should head for next, or null when all are done.</summary>
        public CableCheckpoint NextCheckpoint { get; private set; }

        private void Start()
        {
            if (path == null)
            {
                Debug.LogError("[MissionProgress] No CablePath. Progress cannot be reported.");
                enabled = false;
                return;
            }

            _checkpoints.Clear();
            // No sort mode: Unity deprecated that overload, and we sort by cable distance
            // immediately below anyway, which is the only order that means anything here.
            _checkpoints.AddRange(FindObjectsByType<CableCheckpoint>(FindObjectsInactive.Exclude));

            // Along the cable, not by name and not by scene order. The order the player
            // meets them in is the only order that means anything here.
            _checkpoints.Sort((a, b) => a.DistanceAlongCable.CompareTo(b.DistanceAlongCable));

            for (var i = 0; i < _checkpoints.Count; i++)
                _checkpoints[i].Bind(this, i);

            Recount();
        }

        /// <summary>Called by a checkpoint once its scan has finished and its report is spoken.</summary>
        public void ReportScanned(CableCheckpoint checkpoint)
        {
            // Light the stretch of cable that led here. The line behind the rover glowing
            // and the line ahead of it not is the progress bar the player is already
            // looking at, which is worth more than the one in the corner of the screen.
            if (visual != null && checkpoint != null)
                visual.MarkSectionScanned(checkpoint.Index, checkpoint.IsFault);

            Recount();

            if (!completeLevelWhenScanned || ScannedCheckpoints < TotalCheckpoints || TotalCheckpoints == 0)
                return;

            if (director != null)
                director.Complete();
        }

        private void Recount()
        {
            var scanned = 0;
            CableCheckpoint next = null;

            foreach (var checkpoint in _checkpoints)
            {
                if (checkpoint.IsScanned)
                {
                    scanned++;
                    continue;
                }

                if (next == null)
                    next = checkpoint;
            }

            ScannedCheckpoints = scanned;
            NextCheckpoint = next;
            Changed?.Invoke();
        }

        /// <summary>
        /// Metres of cable between the rover and the last checkpoint of this stretch.
        ///
        /// <para>Measured to the final checkpoint rather than to the end of the cable,
        /// because the mission is the scanning and the last few metres of cable past the
        /// last mark are not work. A counter that still reads 12 remaining when the job is
        /// done is a counter nobody believes twice.</para>
        /// </summary>
        public float DistanceRemaining()
        {
            if (path == null || rover == null || _checkpoints.Count == 0)
                return 0f;

            var last = _checkpoints[_checkpoints.Count - 1];
            var here = path.DistanceAlong(rover.position);
            return Mathf.Max(0f, last.DistanceAlongCable - here);
        }

        /// <summary>How far off the cable the rover currently is, in world units.</summary>
        public float OffCableDistance()
        {
            if (path == null || rover == null)
                return 0f;

            return path.OffsetFrom(rover.position);
        }

        /// <summary>
        /// Direction the player should steer to get back on the line, as a world vector.
        /// Zero when they are on it. Feeds the HUD arrow.
        /// </summary>
        public Vector3 DirectionToCable()
        {
            if (path == null || rover == null)
                return Vector3.zero;

            var closest = path.ClosestPoint(rover.position, out _, out var offset);
            if (offset < 0.01f)
                return Vector3.zero;

            var delta = closest - rover.position;
            delta.y = 0f;
            return delta.normalized;
        }
    }
}
