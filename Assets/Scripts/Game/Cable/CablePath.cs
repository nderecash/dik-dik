using System.Collections.Generic;
using UnityEngine;

namespace Dikdik.Game.Cable
{
    /// <summary>
    /// The relay line, as geometry. A list of corners and the arithmetic to ask questions
    /// about them: where am I along it, what is the point at this distance, which way does
    /// it run from here.
    ///
    /// <para>This is the spine of the whole mission. The cable is what makes the game
    /// legible: without it an open plain is a place to be lost in, and with it the same
    /// plain is a route with a thing at the end. Every other cable class asks this one
    /// where things are.</para>
    ///
    /// <para><b>Corners, not samples.</b> An earlier sketch built a dense sampled polyline
    /// with cumulative arc lengths, which is the standard answer for curved splines. This
    /// path is straight segments between a handful of corners, so exact projection onto
    /// each segment is both simpler and more accurate than sampling, and there is no
    /// resolution to tune. Six segments cost nothing to iterate.</para>
    ///
    /// <para>The corners come from the same <c>(heading, length)</c> array the level builder
    /// uses to lay out its terrain. That is not a convenience, it is a guarantee: the cable
    /// cannot run through a rock, because the rocks are scattered clear of the same route.
    /// Recovery driving the cable blind depends on that being true.</para>
    /// </summary>
    public class CablePath : MonoBehaviour
    {
        [Tooltip("Corners in world space, in order from the start of the run to the fault. " +
                 "Written by CableBuilder from the level's own route.")]
        [SerializeField] private Vector3[] corners = new Vector3[0];

        private float[] _distanceAtCorner;
        private bool _built;

        /// <summary>Total length of the run, in world units.</summary>
        public float TotalLength { get; private set; }

        public int CornerCount => corners == null ? 0 : corners.Length;

        public IReadOnlyList<Vector3> Corners => corners;

        private void Awake()
        {
            Build();
        }

        /// <summary>
        /// Work out the arc distance at each corner. Cheap, idempotent, and called lazily
        /// by every query, because component Awake order is not something to rely on and a
        /// checkpoint asking its distance during its own Awake is entirely reasonable.
        /// </summary>
        public void Build()
        {
            if (_built)
                return;

            _built = true;

            if (corners == null || corners.Length < 2)
            {
                _distanceAtCorner = new float[0];
                TotalLength = 0f;
                Debug.LogWarning($"[CablePath] {name} has fewer than two corners. " +
                                 "Nothing on the cable will work.");
                return;
            }

            _distanceAtCorner = new float[corners.Length];
            _distanceAtCorner[0] = 0f;

            for (var i = 1; i < corners.Length; i++)
                _distanceAtCorner[i] = _distanceAtCorner[i - 1] +
                                       Vector3.Distance(corners[i - 1], corners[i]);

            TotalLength = _distanceAtCorner[corners.Length - 1];
        }

        /// <summary>The point this far along the cable. Clamped at both ends.</summary>
        public Vector3 PointAtDistance(float distance)
        {
            Build();

            if (corners == null || corners.Length == 0)
                return transform.position;

            if (corners.Length == 1 || distance <= 0f)
                return corners[0];

            if (distance >= TotalLength)
                return corners[corners.Length - 1];

            for (var i = 1; i < corners.Length; i++)
            {
                if (distance > _distanceAtCorner[i])
                    continue;

                var segmentLength = _distanceAtCorner[i] - _distanceAtCorner[i - 1];
                if (segmentLength <= 0.0001f)
                    return corners[i];

                var t = (distance - _distanceAtCorner[i - 1]) / segmentLength;
                return Vector3.Lerp(corners[i - 1], corners[i], t);
            }

            return corners[corners.Length - 1];
        }

        /// <summary>Unit direction the cable runs at this distance, pointing toward the fault.</summary>
        public Vector3 DirectionAtDistance(float distance)
        {
            Build();

            if (corners == null || corners.Length < 2)
                return Vector3.forward;

            for (var i = 1; i < corners.Length; i++)
            {
                if (distance > _distanceAtCorner[i] && i < corners.Length - 1)
                    continue;

                var direction = corners[i] - corners[i - 1];
                return direction.sqrMagnitude < 0.0001f ? Vector3.forward : direction.normalized;
            }

            return Vector3.forward;
        }

        /// <summary>
        /// Closest point on the cable to a world position, with how far along it is and how
        /// far off it the position sits.
        ///
        /// <para><paramref name="lateralOffset"/> is the honest answer to "am I lost", and
        /// the HUD's off-cable arrow is driven by it. Measured on the horizontal plane only:
        /// the rover never changes height, and letting a y difference count as being off
        /// the route would make standing on a slope read as wandering.</para>
        /// </summary>
        public Vector3 ClosestPoint(Vector3 world, out float distanceAlong, out float lateralOffset)
        {
            Build();

            distanceAlong = 0f;
            lateralOffset = 0f;

            if (corners == null || corners.Length == 0)
                return world;

            if (corners.Length == 1)
            {
                lateralOffset = Flat(world - corners[0]).magnitude;
                return corners[0];
            }

            var best = corners[0];
            var bestSqr = float.MaxValue;

            for (var i = 1; i < corners.Length; i++)
            {
                var a = corners[i - 1];
                var b = corners[i];
                var ab = Flat(b - a);
                var lengthSqr = ab.sqrMagnitude;

                var t = lengthSqr < 0.0001f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(Flat(world - a), ab) / lengthSqr);

                var point = a + ab * t;
                var offsetSqr = Flat(world - point).sqrMagnitude;

                if (offsetSqr >= bestSqr)
                    continue;

                bestSqr = offsetSqr;
                best = point;
                distanceAlong = _distanceAtCorner[i - 1] + Mathf.Sqrt(lengthSqr) * t;
            }

            lateralOffset = Mathf.Sqrt(bestSqr);
            return best;
        }

        /// <summary>How far along the cable a world position sits.</summary>
        public float DistanceAlong(Vector3 world)
        {
            ClosestPoint(world, out var distance, out _);
            return distance;
        }

        /// <summary>How far off the cable a world position sits.</summary>
        public float OffsetFrom(Vector3 world)
        {
            ClosestPoint(world, out _, out var offset);
            return offset;
        }

        /// <summary>
        /// Points to drive through to get from one distance along the cable to another,
        /// ending exactly on <paramref name="toDistance"/>.
        ///
        /// <para>Every corner in between is included, which is what keeps a recovery drive
        /// on the route instead of cutting a diagonal across whatever the route was going
        /// around. Works in both directions, because recovery usually means going back.</para>
        /// </summary>
        public List<Vector3> WaypointsBetween(float fromDistance, float toDistance)
        {
            Build();

            var points = new List<Vector3>();

            if (corners == null || corners.Length == 0)
                return points;

            var forward = toDistance >= fromDistance;

            if (forward)
            {
                for (var i = 0; i < corners.Length; i++)
                    if (_distanceAtCorner[i] > fromDistance && _distanceAtCorner[i] < toDistance)
                        points.Add(corners[i]);
            }
            else
            {
                for (var i = corners.Length - 1; i >= 0; i--)
                    if (_distanceAtCorner[i] < fromDistance && _distanceAtCorner[i] > toDistance)
                        points.Add(corners[i]);
            }

            points.Add(PointAtDistance(toDistance));
            return points;
        }

        /// <summary>Drop the vertical. The rover drives on a plane; height is never route information.</summary>
        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private void OnDrawGizmos()
        {
            if (corners == null || corners.Length < 2)
                return;

            Gizmos.color = new Color(0.4f, 0.9f, 1f);
            for (var i = 1; i < corners.Length; i++)
                Gizmos.DrawLine(corners[i - 1], corners[i]);
        }
    }
}
