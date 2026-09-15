using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.AI.Perception;
using Setus.HorrorFramework.AI.States;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.PlayMode.AI
{
    public sealed class StalkerNavigationKnowledgeBoundaryTests
    {
        private const int TargetLayer = 8;
        private const int OccluderLayer = 9;

        private GameObject stalkerObject;
        private GameObject targetObject;
        private GameObject wallObject;
        private GameObject eyeObject;
        private GameObject routeObject;
        private StalkerAiTuningProfile profile;
        private HorrorFrameworkConfig config;
        private NavMeshData navMeshData;
        private NavMeshDataInstance navMeshInstance;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (navMeshInstance.valid)
            {
                navMeshInstance.Remove();
            }

            Object.Destroy(stalkerObject);
            Object.Destroy(targetObject);
            Object.Destroy(wallObject);
            Object.Destroy(routeObject);
            Object.Destroy(navMeshData);
            Object.Destroy(profile);
            yield return null;
            HorrorGameContext.ShutdownActive();
            Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator ChaseDestinationTracksOnlyConfirmedOrOtherwiseValidAwareness()
        {
            HorrorGameContext.ShutdownActive();
            config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            HorrorGameContext.Initialize(config);
            profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            SetField(profile, "chaseAnticipationDuration", 0.1f);
            SetField(profile, "lostSightDelay", 0.5f);

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            targetObject.name = "M8 Navigation Knowledge Target";
            targetObject.layer = TargetLayer;
            targetObject.transform.position = new Vector3(0f, 0f, 5f);

            wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = "M8 Navigation Knowledge Wall";
            wallObject.layer = OccluderLayer;
            wallObject.transform.position = new Vector3(0f, 0.8f, 3f);
            wallObject.transform.localScale = new Vector3(6f, 2f, 0.25f);
            wallObject.SetActive(false);

            stalkerObject = new GameObject("M8 Navigation Knowledge Stalker");
            stalkerObject.SetActive(false);
            var agent = stalkerObject.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            var stableId = stalkerObject.AddComponent<StableId>();
            stableId.Assign("m8.test.navigation-knowledge");
            eyeObject = new GameObject("M8 Navigation Knowledge Eye");
            eyeObject.transform.SetParent(stalkerObject.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var controller = stalkerObject.AddComponent<StalkerAiController>();
            SetField(controller, "tuningProfile", profile);
            SetField(controller, "target", targetObject.transform);
            SetField(controller, "eye", eyeObject.transform);
            SetField(controller, "stableId", stableId);
            SetField(controller, "agent", agent);
            SetField(controller, "targetMask", (LayerMask)(1 << TargetLayer));
            SetField(controller, "occluderMask", (LayerMask)(1 << OccluderLayer));
            stalkerObject.SetActive(true);

            yield return null;
            Physics.SyncTransforms();

            var sensor = new StalkerPerceptionSensor();
            var model = GetField<StalkerAiRuntimeModel>(controller, "model");
            var firstVisiblePosition = targetObject.transform.position;
            var initialSight = EvaluateSight(sensor, controller, targetObject.transform);
            Assert.That(initialSight.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));

            model.Tick(0.01f, new StalkerPerceptionInput(initialSight, false));
            model.Tick(0.11f, new StalkerPerceptionInput(initialSight, false));
            Assert.That(model.State, Is.EqualTo(StalkerAiState.Chasing));
            Assert.That(GetDesiredDestination(controller), Is.EqualTo(firstVisiblePosition));

            wallObject.SetActive(true);
            targetObject.transform.position = new Vector3(2f, 0f, 6f);
            Physics.SyncTransforms();
            var blockedSight = EvaluateSight(sensor, controller, targetObject.transform);
            Assert.That(blockedSight.Reason, Is.EqualTo(StalkerSightReason.TargetBlocked));
            model.Tick(0.1f, new StalkerPerceptionInput(blockedSight, false));

            targetObject.transform.position = new Vector3(-2f, 0f, 6f);
            Physics.SyncTransforms();
            blockedSight = EvaluateSight(sensor, controller, targetObject.transform);
            model.Tick(0.1f, new StalkerPerceptionInput(blockedSight, false));

            Assert.That(model.State, Is.EqualTo(StalkerAiState.Chasing));
            Assert.That(model.LastKnownPosition, Is.EqualTo(firstVisiblePosition));
            Assert.That(GetDesiredDestination(controller), Is.EqualTo(firstVisiblePosition));
            Assert.That(GetDesiredDestination(controller), Is.Not.EqualTo(targetObject.transform.position));

            wallObject.SetActive(false);
            targetObject.transform.position = new Vector3(1f, 0f, 4f);
            Physics.SyncTransforms();
            var reacquiredSight = EvaluateSight(sensor, controller, targetObject.transform);
            Assert.That(reacquiredSight.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));
            model.Tick(0.1f, new StalkerPerceptionInput(reacquiredSight, false));

            Assert.That(model.LastKnownPosition, Is.EqualTo(targetObject.transform.position));
            Assert.That(GetDesiredDestination(controller), Is.EqualTo(targetObject.transform.position));

            wallObject.SetActive(true);
            targetObject.transform.position = new Vector3(-1f, 0f, 6f);
            Physics.SyncTransforms();
            blockedSight = EvaluateSight(sensor, controller, targetObject.transform);
            model.Tick(0.51f, new StalkerPerceptionInput(blockedSight, false));

            Assert.That(model.State, Is.EqualTo(StalkerAiState.Searching));
            Assert.That(GetDesiredDestination(controller), Is.EqualTo(new Vector3(1f, 0f, 4f)));
        }

        [UnityTest]
        public IEnumerator PatrolSearchChaseAndLostSightUseARealNavMeshWithoutTrackingThroughWalls()
        {
            HorrorGameContext.ShutdownActive();
            config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var context = HorrorGameContext.Initialize(config);
            profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            SetField(profile, "perceptionInterval", 100f);
            SetField(profile, "chaseAnticipationDuration", 0.1f);
            SetField(profile, "lostSightDelay", 0.5f);
            BuildTestNavMesh();

            Assert.That(NavMesh.SamplePosition(Vector3.zero, out var startHit, 1f, NavMesh.AllAreas), Is.True);
            routeObject = new GameObject("M8 Runtime NavMesh Patrol Route");
            var route = routeObject.AddComponent<StalkerPatrolRoute>();
            var patrolPointA = new GameObject("M8 Runtime NavMesh Patrol A").transform;
            patrolPointA.SetParent(routeObject.transform, false);
            patrolPointA.position = new Vector3(0f, 0f, 3f);
            var patrolPointB = new GameObject("M8 Runtime NavMesh Patrol B").transform;
            patrolPointB.SetParent(routeObject.transform, false);
            patrolPointB.position = new Vector3(2f, 0f, 3f);
            SetField(route, "waypoints", new[] { patrolPointA, patrolPointB });

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            targetObject.name = "M8 Runtime NavMesh Target";
            targetObject.layer = TargetLayer;
            targetObject.transform.position = new Vector3(0f, 0f, -6f);

            wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallObject.name = "M8 Runtime NavMesh LOS Wall";
            wallObject.layer = OccluderLayer;
            wallObject.transform.position = new Vector3(0f, 0.8f, 2.5f);
            wallObject.transform.localScale = new Vector3(6f, 2f, 0.25f);
            wallObject.SetActive(false);

            stalkerObject = new GameObject("M8 Runtime NavMesh Stalker");
            stalkerObject.SetActive(false);
            stalkerObject.transform.position = startHit.position;
            var agent = stalkerObject.AddComponent<NavMeshAgent>();
            agent.updateRotation = false;
            var stableId = stalkerObject.AddComponent<StableId>();
            stableId.Assign("m8.test.runtime-navmesh");
            eyeObject = new GameObject("M8 Runtime NavMesh Eye");
            eyeObject.transform.SetParent(stalkerObject.transform, false);
            eyeObject.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            var controller = stalkerObject.AddComponent<StalkerAiController>();
            SetField(controller, "tuningProfile", profile);
            SetField(controller, "patrolRoute", route);
            SetField(controller, "target", targetObject.transform);
            SetField(controller, "eye", eyeObject.transform);
            SetField(controller, "stableId", stableId);
            SetField(controller, "agent", agent);
            SetField(controller, "targetMask", (LayerMask)(1 << TargetLayer));
            SetField(controller, "occluderMask", (LayerMask)(1 << OccluderLayer));
            var patrolStartPosition = stalkerObject.transform.position;
            stalkerObject.SetActive(true);

            for (var frame = 0; frame < 30 && (!agent.isOnNavMesh || agent.pathPending || !agent.hasPath); frame++)
            {
                yield return null;
            }

            Assert.That(agent.isOnNavMesh, Is.True);
            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Patrol));
            Assert.That(agent.pathStatus, Is.EqualTo(NavMeshPathStatus.PathComplete));
            AssertHorizontalDestination(agent, patrolPointA.position);

            const float minimumPatrolMovement = 0.01f;
            var movementDeadline = Time.realtimeSinceStartup + 2f;
            while (HorizontalDistance(stalkerObject.transform.position, patrolStartPosition) <= minimumPatrolMovement &&
                   Time.realtimeSinceStartup < movementDeadline)
            {
                yield return null;
            }

            Assert.That(
                HorizontalDistance(stalkerObject.transform.position, patrolStartPosition),
                Is.GreaterThan(minimumPatrolMovement));

            var heardPosition = new Vector3(0f, 0f, 2f);
            context.Events.Publish(new PlayerFootstepEmitted(heardPosition, 1f));
            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Suspicious));
            yield return null;
            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Searching));

            Assert.That(agent.Warp(startHit.position), Is.True);
            stalkerObject.transform.rotation = Quaternion.identity;
            targetObject.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();

            var sensor = new StalkerPerceptionSensor();
            var model = GetField<StalkerAiRuntimeModel>(controller, "model");
            var confirmedPosition = targetObject.transform.position;
            var confirmedSight = EvaluateSight(sensor, controller, targetObject.transform);
            Assert.That(confirmedSight.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));
            model.Tick(0.01f, new StalkerPerceptionInput(confirmedSight, false));
            model.Tick(0.11f, new StalkerPerceptionInput(confirmedSight, false));
            InvokePrivate(controller, "UpdateNavigation");

            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Chasing));
            AssertHorizontalDestination(agent, confirmedPosition);

            wallObject.SetActive(true);
            targetObject.transform.position = new Vector3(2f, 0f, 6f);
            Physics.SyncTransforms();
            var blockedSight = EvaluateSight(sensor, controller, targetObject.transform);
            Assert.That(blockedSight.Reason, Is.EqualTo(StalkerSightReason.TargetBlocked));
            model.Tick(0.1f, new StalkerPerceptionInput(blockedSight, false));
            InvokePrivate(controller, "UpdateNavigation");

            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Chasing));
            AssertHorizontalDestination(agent, confirmedPosition);
            Assert.That(
                HorizontalDistance(agent.destination, targetObject.transform.position),
                Is.GreaterThan(0.1f));

            wallObject.SetActive(false);
            Assert.That(agent.Warp(startHit.position), Is.True);
            stalkerObject.transform.rotation = Quaternion.identity;
            targetObject.transform.position = new Vector3(1f, 0f, 4f);
            Physics.SyncTransforms();
            confirmedSight = EvaluateSight(sensor, controller, targetObject.transform);
            Assert.That(confirmedSight.Reason, Is.EqualTo(StalkerSightReason.TargetConfirmed));
            model.Tick(0.1f, new StalkerPerceptionInput(confirmedSight, false));
            InvokePrivate(controller, "UpdateNavigation");
            AssertHorizontalDestination(agent, targetObject.transform.position);

            wallObject.SetActive(true);
            targetObject.transform.position = new Vector3(-1f, 0f, 6f);
            Physics.SyncTransforms();
            blockedSight = EvaluateSight(sensor, controller, targetObject.transform);
            model.Tick(0.51f, new StalkerPerceptionInput(blockedSight, false));
            InvokePrivate(controller, "UpdateNavigation");

            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Searching));
            AssertHorizontalDestination(agent, new Vector3(1f, 0f, 4f));
        }

        [UnityTest]
        public IEnumerator SearchReenableReissuesInvalidatedPathWithoutResettingSemanticState()
        {
            HorrorGameContext.ShutdownActive();
            config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            HorrorGameContext.Initialize(config);
            profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            SetField(profile, "perceptionInterval", 100f);
            SetField(profile, "searchDuration", 10f);
            BuildTestNavMesh();

            Assert.That(NavMesh.SamplePosition(Vector3.zero, out var startHit, 1f, NavMesh.AllAreas), Is.True);
            stalkerObject = new GameObject("M8 Navigation Cache Lifecycle Stalker");
            stalkerObject.SetActive(false);
            stalkerObject.transform.position = startHit.position;
            var agent = stalkerObject.AddComponent<NavMeshAgent>();
            agent.updateRotation = false;
            var stableId = stalkerObject.AddComponent<StableId>();
            stableId.Assign("m8.test.navigation-cache-lifecycle");
            var controller = stalkerObject.AddComponent<StalkerAiController>();
            SetField(controller, "tuningProfile", profile);
            SetField(controller, "stableId", stableId);
            SetField(controller, "agent", agent);
            stalkerObject.SetActive(true);
            yield return null;

            var searchDestination = new Vector3(3f, 0f, 4f);
            controller.RestoreState(new StalkerAiRuntimeState(
                StalkerAiState.Searching,
                0.75f,
                searchDestination,
                0,
                0f,
                10f));
            InvokePrivate(controller, "UpdateNavigation");
            for (var frame = 0; frame < 30 && (agent.pathPending || !agent.hasPath); frame++)
            {
                yield return null;
            }

            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Searching));
            Assert.That(agent.hasPath, Is.True);
            AssertHorizontalDestination(agent, searchDestination);

            controller.enabled = false;
            Assert.That(agent.hasPath, Is.False);
            Assert.That(GetField<bool>(controller, "hasDestination"), Is.False);
            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Searching));

            controller.enabled = true;
            for (var frame = 0; frame < 30 && (agent.pathPending || !agent.hasPath); frame++)
            {
                yield return null;
            }

            Assert.That(controller.State, Is.EqualTo(StalkerAiState.Searching));
            Assert.That(agent.hasPath, Is.True);
            AssertHorizontalDestination(agent, searchDestination);

            agent.isStopped = true;
            InvokePrivate(controller, "UpdateNavigation");
            InvokePrivate(controller, "UpdateNavigation");
            InvokePrivate(controller, "UpdateNavigation");
            Assert.That(agent.isStopped, Is.True);
            Assert.That(GetField<bool>(controller, "hasDestination"), Is.True);
            AssertHorizontalDestination(agent, searchDestination);
        }

        private void BuildTestNavMesh()
        {
            var source = new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(new Vector3(0f, -0.05f, 3f), Quaternion.identity, Vector3.one),
                size = new Vector3(20f, 0.1f, 20f),
                area = 0
            };
            navMeshData = NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByIndex(0),
                new List<NavMeshBuildSource> { source },
                new Bounds(new Vector3(0f, 0f, 3f), new Vector3(24f, 4f, 24f)),
                Vector3.zero,
                Quaternion.identity);
            Assert.That(navMeshData, Is.Not.Null);
            navMeshInstance = NavMesh.AddNavMeshData(navMeshData);
            Assert.That(navMeshInstance.valid, Is.True);
        }

        private StalkerSightResult EvaluateSight(
            StalkerPerceptionSensor sensor,
            StalkerAiController controller,
            Transform target)
        {
            return sensor.EvaluateSight(
                controller.transform,
                target,
                eyeObject.transform.position,
                1 << TargetLayer,
                1 << OccluderLayer,
                profile);
        }

        private static Vector3 GetDesiredDestination(StalkerAiController controller)
        {
            var method = typeof(StalkerAiController).GetMethod(
                "TryGetDesiredDestination",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            var arguments = new object[] { Vector3.zero };
            var hasDestination = (bool)method.Invoke(controller, arguments);
            Assert.That(hasDestination, Is.True);
            return (Vector3)arguments[0];
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing test method: {methodName}");
            method.Invoke(target, null);
        }

        private static void AssertHorizontalDestination(NavMeshAgent agent, Vector3 expected)
        {
            Assert.That(HorizontalDistance(agent.destination, expected), Is.LessThan(0.01f));
        }

        private static float HorizontalDistance(Vector3 left, Vector3 right)
        {
            return Vector2.Distance(new Vector2(left.x, left.z), new Vector2(right.x, right.z));
        }

        private static T GetField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field: {fieldName}");
            return (T)field.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field: {fieldName}");
            field.SetValue(target, value);
        }
    }
}
