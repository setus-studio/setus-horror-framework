using System;
using System.Collections.Generic;
using UnityEngine;

namespace Setus.HorrorFramework.AI.Navigation
{
    [DisallowMultipleComponent]
    public sealed class StalkerPatrolRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] waypoints = Array.Empty<Transform>();

        public int Count => waypoints?.Length ?? 0;

        public bool TryGetWaypoint(int index, out Transform waypoint)
        {
            waypoint = null;
            if (waypoints == null || index < 0 || index >= waypoints.Length)
            {
                return false;
            }

            waypoint = waypoints[index];
            return waypoint != null;
        }

        public IReadOnlyList<Transform> Waypoints => waypoints ?? Array.Empty<Transform>();

        private void OnDrawGizmosSelected()
        {
            if (waypoints == null)
            {
                return;
            }

            Gizmos.color = new Color(0.9f, 0.75f, 0.2f, 0.9f);
            for (var i = 0; i < waypoints.Length; i++)
            {
                var waypoint = waypoints[i];
                if (waypoint == null)
                {
                    continue;
                }

                Gizmos.DrawSphere(waypoint.position, 0.18f);
                if (i > 0 && waypoints[i - 1] != null)
                {
                    Gizmos.DrawLine(waypoints[i - 1].position, waypoint.position);
                }
            }
        }
    }
}
