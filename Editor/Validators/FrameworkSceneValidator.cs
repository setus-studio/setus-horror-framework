using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.AI.Debugging;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Bootstrap;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Interaction.Triggers;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Validators
{
    public static class FrameworkSceneValidator
    {
        [MenuItem("Setus/Horror Framework/Validation/Validate Active Scene")]
        public static void ValidateActiveSceneFromMenu()
        {
            var manifest = Selection.activeObject as StableIdManifest;
            var result = ValidateScene(SceneManager.GetActiveScene(), manifest);
            LogResult(SceneManager.GetActiveScene().name, result);
        }

        public static FrameworkSceneValidationResult ValidateScene(Scene scene, StableIdManifest manifest)
        {
            var issues = new List<FrameworkSceneValidationIssue>();
            if (!scene.IsValid())
            {
                issues.Add(Error("scene.invalid", "Scene is invalid and cannot be validated."));
                return new FrameworkSceneValidationResult(issues);
            }

            var components = CollectComponents(scene);
            ValidateStableIds(components, manifest, ResolveSceneId(scene), issues);
            ValidateNarrative(components, issues);
            ValidateScares(scene, components, issues);
            ValidateInteractions(components, issues);
            ValidateAi(components, issues);
            ValidateDebugConfiguration(components, issues);
            return new FrameworkSceneValidationResult(issues);
        }

        public static void LogResult(string sceneName, FrameworkSceneValidationResult result)
        {
            for (var i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                if (issue.IsError)
                {
                    Debug.LogError(issue.ToString(), issue.Owner);
                }
                else
                {
                    Debug.LogWarning(issue.ToString(), issue.Owner);
                }
            }

            if (result.Issues.Count == 0)
            {
                Debug.Log($"Framework scene validation passed for '{sceneName}'.");
            }
            else
            {
                Debug.Log(
                    $"Framework scene validation finished for '{sceneName}': " +
                    $"{result.ErrorCount} error(s), {result.WarningCount} warning(s).");
            }
        }

        private static MonoBehaviour[] CollectComponents(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null)
                .ToArray();
        }

        private static string ResolveSceneId(Scene scene)
        {
            return string.IsNullOrWhiteSpace(scene.name) ? "<unsaved-scene>" : scene.name;
        }

        private static void ValidateStableIds(
            IReadOnlyList<MonoBehaviour> components,
            StableIdManifest manifest,
            string sceneId,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            var stableIds = components.OfType<StableId>().ToArray();
            var result = StableIdValidator.Validate(stableIds.Cast<IStableIdProvider>(), manifest, sceneId);
            for (var i = 0; i < result.Issues.Count; i++)
            {
                var source = result.Issues[i];
                var owner = source.Providers.FirstOrDefault()?.Owner;
                var severity = source.IsError
                    ? FrameworkSceneValidationSeverity.Error
                    : FrameworkSceneValidationSeverity.Warning;
                issues.Add(new FrameworkSceneValidationIssue(
                    severity,
                    $"stable-id.{source.IssueType}",
                    $"Stable ID '{DisplayId(source.StableId)}' failed {source.IssueType} validation.",
                    owner));
            }

            var persistentIds = stableIds
                .Where(HasPersistentOwner)
                .ToArray();
            if (persistentIds.Length > 0 && manifest == null)
            {
                issues.Add(Error(
                    "save.manifest.missing",
                    "Scene contains persistent objects but no StableIdManifest was supplied to validation.",
                    persistentIds[0]));
                return;
            }

            if (manifest == null)
            {
                return;
            }

            var manifestIds = new HashSet<string>(StringComparer.Ordinal);
            var applicableManifestIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < manifest.Entries.Count; i++)
            {
                var entry = manifest.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.StableId))
                {
                    issues.Add(Error("save.manifest.empty-entry", $"Manifest entry {i} has no StableId.", manifest));
                    continue;
                }

                if (!manifestIds.Add(entry.StableId))
                {
                    issues.Add(Error(
                        "save.manifest.duplicate-entry",
                        $"Manifest contains duplicate StableId '{entry.StableId}'.",
                        manifest));
                }

                if (entry.AppliesToScene(sceneId))
                {
                    applicableManifestIds.Add(entry.StableId);
                }
            }

            for (var i = 0; i < persistentIds.Length; i++)
            {
                var stableId = persistentIds[i];
                if (stableId.HasStableId && !applicableManifestIds.Contains(stableId.Id))
                {
                    issues.Add(Error(
                        "save.manifest.undeclared-id",
                        $"Persistent StableId '{stableId.Id}' is not declared for scene '{sceneId}' " +
                        "by the selected manifest.",
                        stableId));
                }
            }
        }

        private static bool HasPersistentOwner(StableId stableId)
        {
            var behaviours = stableId.GetComponents<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ISaveableStateOwner)
                {
                    return true;
                }
            }

            return stableId.GetComponent<StalkerAiController>() != null;
        }

        private static void ValidateNarrative(
            IReadOnlyList<MonoBehaviour> components,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var adapter in components.OfType<NarrativeScenarioAdapter>())
            {
                var scenario = ReadObjectReference<NarrativeScenarioDefinition>(adapter, "scenario");
                if (scenario == null)
                {
                    issues.Add(Error(
                        "narrative.scenario.missing",
                        "NarrativeScenarioAdapter requires a scenario definition.",
                        adapter));
                    continue;
                }

                var graph = scenario.ValidateObjectiveGraph();
                if (!graph.IsValid)
                {
                    issues.Add(Error(
                        "narrative.objective-graph.invalid",
                        graph.ToDiagnostic(scenario.ScenarioId),
                        scenario));
                }
            }
        }

        private static void ValidateScares(
            Scene scene,
            IReadOnlyList<MonoBehaviour> components,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            var result = ScareScenarioSceneValidator.Validate(
                scene.name,
                components.OfType<ScareScenarioAdapter>(),
                components.OfType<ScareScenarioTrigger>());
            for (var i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                issues.Add(Error("atmosphere.scare-reference.invalid", issue.ToDiagnostic(), issue.Owner));
            }

            var triggerSourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in components.OfType<InteractionTriggerZone>())
            {
                var stableId = source.GetComponent<StableId>();
                if (stableId != null && stableId.HasStableId)
                {
                    triggerSourceIds.Add(stableId.Id);
                }
            }

            foreach (var trigger in components.OfType<ScareScenarioTrigger>())
            {
                if (string.IsNullOrWhiteSpace(trigger.TriggerId))
                {
                    issues.Add(Error(
                        "atmosphere.trigger-id.empty",
                        $"ScareScenarioTrigger '{trigger.name}' field 'triggerId' must reference an " +
                        "InteractionTriggerZone StableId in this scene.",
                        trigger));
                }
                else if (!triggerSourceIds.Contains(trigger.TriggerId))
                {
                    issues.Add(Error(
                        "atmosphere.trigger-id.unresolved",
                        $"ScareScenarioTrigger '{trigger.name}' field 'triggerId' references " +
                        $"'{trigger.TriggerId}', but no InteractionTriggerZone with that StableId exists in " +
                        $"scene '{scene.name}'.",
                        trigger));
                }
            }
        }

        private static void ValidateInteractions(
            IReadOnlyList<MonoBehaviour> components,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var interactable in components.OfType<PersistentInteractableBase>())
            {
                if (interactable.GetComponentInChildren<Collider>(true) == null)
                {
                    issues.Add(Error(
                        "interaction.collider.missing",
                        "Persistent interactable requires a Collider on itself or a child.",
                        interactable));
                }
            }

            foreach (var trigger in components.OfType<InteractionTriggerZone>())
            {
                var collider = trigger.GetComponent<Collider>();
                if (collider == null || !collider.isTrigger)
                {
                    issues.Add(Error(
                        "interaction.trigger.collider-invalid",
                        "InteractionTriggerZone requires a trigger Collider on the same GameObject.",
                        trigger));
                }
            }

            foreach (var interactor in components.OfType<RaycastInteractor>())
            {
                if (ReadLayerMask(interactor, "interactionMask") == 0)
                {
                    issues.Add(Error(
                        "interaction.mask.empty",
                        "RaycastInteractor interactionMask selects no layers.",
                        interactor));
                }
            }
        }

        private static void ValidateAi(
            IReadOnlyList<MonoBehaviour> components,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var route in components.OfType<StalkerPatrolRoute>())
            {
                if (!IsUsablePatrolRoute(route))
                {
                    issues.Add(Error(
                        "ai.patrol-route.invalid",
                        "StalkerPatrolRoute requires at least one non-null waypoint.",
                        route));
                }
            }

            foreach (var controller in components.OfType<StalkerAiController>())
            {
                if (controller.TuningProfile == null)
                {
                    issues.Add(Error("ai.profile.missing", "Stalker AI requires a tuning profile.", controller));
                }

                if (controller.GetComponent<NavMeshAgent>() == null)
                {
                    issues.Add(Error("ai.navmesh-agent.missing", "Stalker AI requires a NavMeshAgent.", controller));
                }

                var stableId = controller.GetComponent<StableId>();
                if (stableId == null || !stableId.HasStableId)
                {
                    issues.Add(Error("ai.stable-id.missing", "Stalker AI requires an authored StableId.", controller));
                }

                if (controller.GetComponent<StalkerAiSaveAdapter>() == null)
                {
                    issues.Add(Error(
                        "ai.save-adapter.missing",
                        "Stalker AI requires StalkerAiSaveAdapter for persistence.",
                        controller));
                }

                var patrolRoute = ReadObjectReference<StalkerPatrolRoute>(controller, "patrolRoute");
                if (patrolRoute == null)
                {
                    issues.Add(Error(
                        "ai.patrol-route.missing",
                        $"Stalker AI '{controller.name}' field 'patrolRoute' is required because the current " +
                        "M8 state contract initializes the controller in Patrol.",
                        controller));
                }
                else if (!IsUsablePatrolRoute(patrolRoute))
                {
                    issues.Add(Error(
                        "ai.patrol-route.unusable",
                        $"Stalker AI '{controller.name}' field 'patrolRoute' references route " +
                        $"'{patrolRoute.name}', which must contain at least one non-null waypoint.",
                        controller));
                }

                var targetMask = ReadLayerMask(controller, "targetMask");
                var occluderMask = ReadLayerMask(controller, "occluderMask");
                if (targetMask == 0)
                {
                    issues.Add(Error("ai.target-mask.empty", "Stalker AI targetMask selects no layers.", controller));
                }

                if (occluderMask == 0)
                {
                    issues.Add(Error("ai.occluder-mask.empty", "Stalker AI occluderMask selects no layers.", controller));
                }

                if (targetMask != 0 && controller.Target != null &&
                    !TargetMaskIncludesAuthoredTarget(controller.Target, targetMask))
                {
                    issues.Add(Error(
                        "ai.target-mask.excludes-authored-target",
                        $"Stalker AI targetMask does not include any Collider layer used by the assigned target " +
                        $"'{controller.Target.name}'. Sight can never confirm this target.",
                        controller));
                }

                if (controller.GetComponent<StalkerAiDebugView>() == null)
                {
                    issues.Add(Warning(
                        "ai.debug-overlay.missing",
                        "No StalkerAiDebugView is attached; designer AI diagnostics will be unavailable when debug is enabled.",
                        controller));
                }
            }
        }

        private static bool IsUsablePatrolRoute(StalkerPatrolRoute route)
        {
            return route != null && route.Count > 0 && route.Waypoints.All(waypoint => waypoint != null);
        }

        private static bool TargetMaskIncludesAuthoredTarget(Transform target, int targetMask)
        {
            var colliders = target.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                // Collider/reference validation is outside the layer-mask contract. Runtime remains fail-closed.
                return true;
            }

            for (var i = 0; i < colliders.Length; i++)
            {
                if (((1 << colliders[i].gameObject.layer) & targetMask) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateDebugConfiguration(
            IReadOnlyList<MonoBehaviour> components,
            ICollection<FrameworkSceneValidationIssue> issues)
        {
            foreach (var bootstrapper in components.OfType<HorrorGameBootstrapper>())
            {
                var config = ReadObjectReference<HorrorFrameworkConfig>(bootstrapper, "config");
                if (config == null)
                {
                    issues.Add(Error(
                        "bootstrap.config.missing",
                        "HorrorGameBootstrapper requires HorrorFrameworkConfig.",
                        bootstrapper));
                }
                else if (config.DebugEnabledByDefault)
                {
                    issues.Add(Warning(
                        "debug.enabled-by-default",
                        "Framework diagnostics are enabled by default. Production configuration should normally start with debug disabled.",
                        config));
                }
            }
        }

        private static T ReadObjectReference<T>(UnityEngine.Object owner, string propertyName)
            where T : UnityEngine.Object
        {
            var property = new SerializedObject(owner).FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static int ReadLayerMask(UnityEngine.Object owner, string propertyName)
        {
            var property = new SerializedObject(owner).FindProperty(propertyName);
            return property != null ? property.intValue : 0;
        }

        private static string DisplayId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? "<empty>" : id;
        }

        private static FrameworkSceneValidationIssue Error(
            string code,
            string message,
            UnityEngine.Object owner = null)
        {
            return new FrameworkSceneValidationIssue(
                FrameworkSceneValidationSeverity.Error,
                code,
                message,
                owner);
        }

        private static FrameworkSceneValidationIssue Warning(
            string code,
            string message,
            UnityEngine.Object owner = null)
        {
            return new FrameworkSceneValidationIssue(
                FrameworkSceneValidationSeverity.Warning,
                code,
                message,
                owner);
        }
    }
}
