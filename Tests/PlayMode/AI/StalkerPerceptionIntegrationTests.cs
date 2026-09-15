using System.Collections;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Perception;
using Setus.HorrorFramework.AI.Tuning;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.PlayMode.AI
{
    public sealed class StalkerPerceptionIntegrationTests
    {
        private const int TargetLayer = 8;
        private const int OccluderLayer = 9;

        [UnityTest]
        public IEnumerator UnobstructedTargetIsConfirmed()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, 5f);
                target.layer = TargetLayer;
                yield return null;
                Physics.SyncTransforms();

                var result = EvaluateSight(observer.transform, target.transform, profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));
                Assert.That(result.TargetPosition, Is.EqualTo(target.transform.position));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator CharacterControllerWithGroundPivotIsConfirmedAtBodyCenter()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = new GameObject("M8 CharacterController Target");
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, 5f);
                target.layer = TargetLayer;
                var characterController = target.AddComponent<CharacterController>();
                characterController.height = 1.8f;
                characterController.radius = 0.28f;
                characterController.center = new Vector3(0f, 0.9f, 0f);
                yield return null;
                Physics.SyncTransforms();

                var result = new StalkerPerceptionSensor().EvaluateSight(
                    observer.transform,
                    target.transform,
                    observer.transform.position,
                    1 << TargetLayer,
                    1 << OccluderLayer,
                    profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));
                Assert.That(result.TargetPosition, Is.EqualTo(target.transform.position));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator CharacterControllerBehindWallRemainsBlocked()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = new GameObject("M8 CharacterController Target");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, 6f);
                target.layer = TargetLayer;
                var characterController = target.AddComponent<CharacterController>();
                characterController.height = 1.8f;
                characterController.radius = 0.28f;
                characterController.center = new Vector3(0f, 0.9f, 0f);
                wall.transform.position = new Vector3(0f, 0.45f, 3f);
                wall.transform.localScale = new Vector3(3f, 1.8f, 0.25f);
                wall.layer = OccluderLayer;
                yield return null;
                Physics.SyncTransforms();

                var result = new StalkerPerceptionSensor().EvaluateSight(
                    observer.transform,
                    target.transform,
                    observer.transform.position,
                    1 << TargetLayer,
                    1 << OccluderLayer,
                    profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.TargetBlocked));
                Assert.That(result.Blocker, Is.Not.Null);
                Assert.That(result.Blocker.gameObject, Is.EqualTo(wall));
                Assert.That(result.TargetPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(wall);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator OccluderHitBlocksTargetConfirmation()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, 6f);
                target.layer = TargetLayer;
                wall.transform.position = new Vector3(0f, 0.8f, 3f);
                wall.transform.localScale = new Vector3(3f, 2f, 0.25f);
                wall.layer = OccluderLayer;
                yield return null;
                Physics.SyncTransforms();

                var result = EvaluateSight(observer.transform, target.transform, profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.TargetBlocked));
                Assert.That(result.Blocker, Is.Not.Null);
                Assert.That(result.Blocker.gameObject, Is.EqualTo(wall));
                Assert.That(result.TargetPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(wall);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator FullHitBufferFailsClosedWithoutConfirmingTarget()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var decoys = new GameObject[8];
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, 12f);
                target.layer = TargetLayer;
                for (var index = 0; index < decoys.Length; index++)
                {
                    var distanceAlongRay = 1f + index;
                    var heightAlongRay = 0.8f * (1f - distanceAlongRay / 12f);
                    decoys[index] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    decoys[index].name = $"M8 Non-Occluding Target-Layer Collider {index}";
                    decoys[index].transform.position = new Vector3(0f, heightAlongRay, distanceAlongRay);
                    decoys[index].transform.localScale = new Vector3(0.2f, 0.2f, 0.1f);
                    decoys[index].layer = TargetLayer;
                }

                wall.transform.position = new Vector3(0f, 0.8f, 10f);
                wall.transform.localScale = new Vector3(3f, 2f, 0.2f);
                wall.layer = OccluderLayer;
                yield return null;
                Physics.SyncTransforms();

                var result = EvaluateSight(observer.transform, target.transform, profile);

                Assert.That(result.IsConfirmed, Is.False);
                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.QueryOverflow));
                Assert.That(result.TargetPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                for (var index = 0; index < decoys.Length; index++)
                {
                    Object.Destroy(decoys[index]);
                }

                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(wall);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator ObserverOwnedColliderDoesNotBlockTarget()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var selfCollider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                selfCollider.name = "M8 Observer Self Collider";
                selfCollider.transform.SetParent(observer.transform, false);
                selfCollider.transform.localPosition = new Vector3(0f, 0.8f, 0.8f);
                selfCollider.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
                selfCollider.layer = OccluderLayer;
                target.transform.position = new Vector3(0f, 0f, 5f);
                target.layer = TargetLayer;
                yield return null;
                Physics.SyncTransforms();

                var result = EvaluateSight(observer.transform, target.transform, profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator TargetBeforeOccluderIsConfirmed()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, 3f);
                target.layer = TargetLayer;
                wall.transform.position = new Vector3(0f, 0.8f, 6f);
                wall.transform.localScale = new Vector3(3f, 2f, 0.25f);
                wall.layer = OccluderLayer;
                yield return null;
                Physics.SyncTransforms();

                var result = EvaluateSight(observer.transform, target.transform, profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(wall);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator OutsideFieldOfViewDoesNotExposeCurrentTargetPosition()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = new GameObject("M8 Target");
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, -4f);
                yield return null;

                var result = new StalkerPerceptionSensor().EvaluateSight(
                    observer.transform,
                    target.transform,
                    observer.transform.position,
                    ~0,
                    ~0,
                    profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.OutsideFieldOfView));
                Assert.That(result.TargetPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(profile);
            }
        }

        [UnityTest]
        public IEnumerator TargetNotHitDoesNotExposeCurrentTargetPosition()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            var observer = new GameObject("M8 Observer");
            var target = new GameObject("M8 Target Without Collider");
            try
            {
                observer.transform.position = Vector3.zero;
                observer.transform.forward = Vector3.forward;
                target.transform.position = new Vector3(0f, 0f, 4f);
                yield return null;

                var result = new StalkerPerceptionSensor().EvaluateSight(
                    observer.transform,
                    target.transform,
                    observer.transform.position,
                    ~0,
                    ~0,
                    profile);

                Assert.That(result.Reason, Is.EqualTo(StalkerSightReason.TargetNotHit));
                Assert.That(result.TargetPosition, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.Destroy(observer);
                Object.Destroy(target);
                Object.Destroy(profile);
            }
        }

        private static StalkerSightResult EvaluateSight(
            Transform observer,
            Transform target,
            StalkerAiTuningProfile profile)
        {
            return new StalkerPerceptionSensor().EvaluateSight(
                observer,
                target,
                new Vector3(observer.position.x, observer.position.y + 0.8f, observer.position.z),
                1 << TargetLayer,
                1 << OccluderLayer,
                profile);
        }
    }
}
