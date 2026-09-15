using System;
using Setus.HorrorFramework.AI.Tuning;
using UnityEngine;

namespace Setus.HorrorFramework.AI.Perception
{
    public sealed class StalkerPerceptionSensor
    {
        private readonly RaycastHit[] hitBuffer = new RaycastHit[8];

        public StalkerSightResult EvaluateSight(
            Transform observer,
            Transform target,
            Vector3 eyePosition,
            LayerMask targetMask,
            LayerMask occluderMask,
            StalkerAiTuningProfile profile)
        {
            if (target == null || profile == null)
            {
                return new StalkerSightResult(StalkerSightReason.NoTarget, Vector3.zero, null);
            }

            var targetPosition = target.position;
            var targetCollider = target.GetComponent<Collider>();
            var sightPosition = ResolveSightPosition(target, targetCollider);
            var delta = sightPosition - eyePosition;
            var distance = delta.magnitude;
            if (distance > profile.SightRange)
            {
                return new StalkerSightResult(StalkerSightReason.OutOfRange, targetPosition, null);
            }

            if (distance <= Mathf.Epsilon)
            {
                return new StalkerSightResult(StalkerSightReason.TargetConfirmed, targetPosition, null);
            }

            if (observer != null && Vector3.Angle(observer.forward, delta) > profile.FieldOfView * 0.5f)
            {
                return new StalkerSightResult(StalkerSightReason.OutsideFieldOfView, Vector3.zero, null);
            }

            var combinedMask = targetMask.value | occluderMask.value;
            var count = Physics.RaycastNonAlloc(
                eyePosition,
                delta / distance,
                hitBuffer,
                distance,
                combinedMask,
                QueryTriggerInteraction.Ignore);
            if (count >= hitBuffer.Length)
            {
                return new StalkerSightResult(StalkerSightReason.QueryOverflow, Vector3.zero, null);
            }

            SortHitsByDistance(count);

            for (var i = 0; i < count; i++)
            {
                var collider = hitBuffer[i].collider;
                if (collider == null || IsOwnedBy(collider.transform, observer))
                {
                    continue;
                }

                if (IsOwnedBy(collider.transform, target))
                {
                    if (((1 << collider.gameObject.layer) & targetMask.value) != 0)
                    {
                        return new StalkerSightResult(StalkerSightReason.TargetConfirmed, targetPosition, null);
                    }

                    continue;
                }

                if (((1 << collider.gameObject.layer) & occluderMask.value) != 0)
                {
                    return new StalkerSightResult(StalkerSightReason.TargetBlocked, Vector3.zero, collider);
                }
            }

            // CharacterController is a valid explicit target collider but is not guaranteed to be
            // returned by scene ray queries in every lifecycle state. A clear, fail-closed blocker
            // query can still confirm the authored target without exposing sight through occluders.
            if (CanConfirmExplicitTarget(targetCollider, targetMask))
            {
                return new StalkerSightResult(StalkerSightReason.TargetConfirmed, targetPosition, null);
            }

            return new StalkerSightResult(StalkerSightReason.TargetNotHit, Vector3.zero, null);
        }

        public bool IsWithinProximity(Vector3 observerPosition, Transform target, StalkerAiTuningProfile profile)
        {
            return target != null && profile != null &&
                Vector3.Distance(observerPosition, target.position) <= profile.ProximityRange;
        }

        private void SortHitsByDistance(int count)
        {
            Array.Sort(hitBuffer, 0, count, RaycastHitDistanceComparer.Instance);
        }

        private static bool IsOwnedBy(Transform candidate, Transform owner)
        {
            return candidate != null && owner != null && (candidate == owner || candidate.IsChildOf(owner));
        }

        private static Vector3 ResolveSightPosition(Transform target, Collider targetCollider)
        {
            return targetCollider != null && targetCollider.enabled
                ? targetCollider.bounds.center
                : target.position;
        }

        private static bool CanConfirmExplicitTarget(Collider targetCollider, LayerMask targetMask)
        {
            return targetCollider != null &&
                targetCollider.enabled &&
                targetCollider.gameObject.activeInHierarchy &&
                ((1 << targetCollider.gameObject.layer) & targetMask.value) != 0;
        }

        private sealed class RaycastHitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly RaycastHitDistanceComparer Instance = new RaycastHitDistanceComparer();

            public int Compare(RaycastHit left, RaycastHit right)
            {
                return left.distance.CompareTo(right.distance);
            }
        }
    }
}
