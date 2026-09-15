using System;
using System.Collections.Generic;
using System.Linq;
using Setus.HorrorFramework.Atmosphere.Scares;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.Editor.Validators
{
    public static class ScareScenarioSceneValidator
    {
        [MenuItem("Setus/Horror Framework/Atmosphere/Validate Active Scene Scare References")]
        public static void ValidateActiveSceneFromMenu()
        {
            var result = ValidateScene(SceneManager.GetActiveScene());
            for (var i = 0; i < result.Issues.Count; i++)
            {
                var issue = result.Issues[i];
                Debug.LogError(issue.ToDiagnostic(), issue.Owner);
            }

            if (result.IsValid)
            {
                Debug.Log($"Scare reference validation passed for scene '{SceneManager.GetActiveScene().name}'.");
            }
        }

        public static ScareScenarioValidationResult ValidateScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return new ScareScenarioValidationResult(new[]
                {
                    new ScareScenarioValidationIssue(
                        "<invalid scene>",
                        null,
                        string.Empty,
                        "Scene is invalid and cannot provide a scare catalog.")
                });
            }

            var adapters = UnityEngine.Object
                .FindObjectsByType<ScareScenarioAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(adapter => adapter != null && adapter.isActiveAndEnabled && adapter.gameObject.scene == scene)
                .ToArray();
            var triggers = UnityEngine.Object
                .FindObjectsByType<ScareScenarioTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(trigger => trigger != null && trigger.gameObject.scene == scene)
                .ToArray();
            return Validate(scene.name, adapters, triggers);
        }

        public static ScareScenarioValidationResult Validate(
            string sceneName,
            IEnumerable<ScareScenarioAdapter> adapters,
            IEnumerable<ScareScenarioTrigger> triggers)
        {
            var issues = new List<ScareScenarioValidationIssue>();
            var definitionsById = new Dictionary<string, DefinitionSource>(StringComparer.Ordinal);
            foreach (var adapter in adapters ?? Enumerable.Empty<ScareScenarioAdapter>())
            {
                if (adapter == null)
                {
                    continue;
                }

                var definitions = adapter.Definitions;
                for (var i = 0; i < definitions.Count; i++)
                {
                    var definition = definitions[i];
                    if (definition == null || string.IsNullOrWhiteSpace(definition.ScareId))
                    {
                        issues.Add(new ScareScenarioValidationIssue(
                            sceneName,
                            adapter,
                            string.Empty,
                            "ScareScenarioAdapter contains a null definition or a definition with an empty ScareId."));
                        continue;
                    }

                    if (!definitionsById.TryGetValue(definition.ScareId, out var existing))
                    {
                        definitionsById.Add(definition.ScareId, new DefinitionSource(adapter, definition));
                        continue;
                    }

                    if (existing.Adapter == adapter || !ReferenceEquals(existing.Definition, definition))
                    {
                        issues.Add(new ScareScenarioValidationIssue(
                            sceneName,
                            adapter,
                            definition.ScareId,
                            "ScareId is registered more than once by this scene with incompatible definition ownership."));
                    }
                }
            }

            foreach (var trigger in triggers ?? Enumerable.Empty<ScareScenarioTrigger>())
            {
                if (trigger == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(trigger.ScareId))
                {
                    issues.Add(new ScareScenarioValidationIssue(
                        sceneName,
                        trigger,
                        string.Empty,
                        "ScareScenarioTrigger requires a non-empty scareId."));
                    continue;
                }

                if (!definitionsById.ContainsKey(trigger.ScareId))
                {
                    issues.Add(new ScareScenarioValidationIssue(
                        sceneName,
                        trigger,
                        trigger.ScareId,
                        "No active ScareScenarioAdapter in this scene registers the referenced scareId."));
                }
            }

            return new ScareScenarioValidationResult(issues);
        }

        private readonly struct DefinitionSource
        {
            public DefinitionSource(ScareScenarioAdapter adapter, ScareDefinition definition)
            {
                Adapter = adapter;
                Definition = definition;
            }

            public ScareScenarioAdapter Adapter { get; }
            public ScareDefinition Definition { get; }
        }
    }

    public sealed class ScareScenarioValidationResult
    {
        public ScareScenarioValidationResult(IEnumerable<ScareScenarioValidationIssue> issues)
        {
            Issues = (issues ?? Enumerable.Empty<ScareScenarioValidationIssue>()).ToArray();
        }

        public IReadOnlyList<ScareScenarioValidationIssue> Issues { get; }
        public bool IsValid => Issues.Count == 0;
    }

    public sealed class ScareScenarioValidationIssue
    {
        public ScareScenarioValidationIssue(string sceneName, UnityEngine.Object owner, string scareId, string reason)
        {
            SceneName = sceneName ?? string.Empty;
            Owner = owner;
            ScareId = scareId ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public string SceneName { get; }
        public UnityEngine.Object Owner { get; }
        public string ScareId { get; }
        public string Reason { get; }

        public string ToDiagnostic()
        {
            var ownerName = Owner != null ? Owner.name : "<unknown object>";
            var id = string.IsNullOrWhiteSpace(ScareId) ? "<empty>" : ScareId;
            return $"Scare reference validation failed: Scene '{SceneName}', object '{ownerName}', scareId '{id}'. {Reason}";
        }
    }
}
