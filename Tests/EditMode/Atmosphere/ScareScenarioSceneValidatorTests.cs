using System.Collections.Generic;
using NUnit.Framework;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.Validators;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.EditMode.Atmosphere
{
    public sealed class ScareScenarioSceneValidatorTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    Object.DestroyImmediate(createdObjects[i]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ValidReferencesAcrossMultipleScareIdsPass()
        {
            var first = CreateDefinition("m7.validation.first");
            var second = CreateDefinition("m7.validation.second");
            var adapter = CreateAdapter("M7 Catalog", first, second);
            var firstTrigger = CreateTrigger("M7 First Trigger", first.ScareId);
            var secondTrigger = CreateTrigger("M7 Second Trigger", second.ScareId);

            var result = ScareScenarioSceneValidator.Validate(
                "M7 Validation Scene",
                new[] { adapter },
                new[] { firstTrigger, secondTrigger });

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void EmptyTriggerScareIdFailsWithActionableDiagnostic()
        {
            var definition = CreateDefinition("m7.validation.valid");
            var adapter = CreateAdapter("M7 Catalog", definition);
            var trigger = CreateTrigger("M7 Empty Trigger", string.Empty);

            var result = ScareScenarioSceneValidator.Validate("M7 Validation Scene", new[] { adapter }, new[] { trigger });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues.Count, Is.EqualTo(1));
            Assert.That(result.Issues[0].Owner, Is.EqualTo(trigger));
            StringAssert.Contains("M7 Empty Trigger", result.Issues[0].ToDiagnostic());
            StringAssert.Contains("<empty>", result.Issues[0].ToDiagnostic());
        }

        [Test]
        public void MissingTriggerScareIdFailsWithReferencedIdAndOwner()
        {
            var definition = CreateDefinition("m7.validation.valid");
            var adapter = CreateAdapter("M7 Catalog", definition);
            var trigger = CreateTrigger("M7 Missing Trigger", "m7.validation.missing");

            var result = ScareScenarioSceneValidator.Validate("M7 Validation Scene", new[] { adapter }, new[] { trigger });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues[0].Owner, Is.EqualTo(trigger));
            StringAssert.Contains("m7.validation.missing", result.Issues[0].ToDiagnostic());
            StringAssert.Contains("M7 Missing Trigger", result.Issues[0].ToDiagnostic());
        }

        [Test]
        public void ValidReferenceRemainsValidWhenScareWouldBeConsumedAtRuntime()
        {
            var definition = CreateDefinition("m7.validation.consumed");
            var adapter = CreateAdapter("M7 Catalog", definition);
            var trigger = CreateTrigger("M7 Consumed Trigger", definition.ScareId);

            var result = ScareScenarioSceneValidator.Validate("M7 Validation Scene", new[] { adapter }, new[] { trigger });

            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void IncompatibleDefinitionAssetsWithTheSameIdFail()
        {
            var first = CreateDefinition("m7.validation.duplicate");
            var second = CreateDefinition("m7.validation.duplicate");
            var firstAdapter = CreateAdapter("M7 First Catalog", first);
            var secondAdapter = CreateAdapter("M7 Second Catalog", second);

            var result = ScareScenarioSceneValidator.Validate(
                "M7 Validation Scene",
                new[] { firstAdapter, secondAdapter },
                System.Array.Empty<ScareScenarioTrigger>());

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Issues[0].Owner, Is.EqualTo(secondAdapter));
            StringAssert.Contains("m7.validation.duplicate", result.Issues[0].ToDiagnostic());
        }

        [Test]
        public void ExistingGameplayFixturePassesScareReferenceValidation()
        {
            const string gameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
            var scene = SceneManager.GetSceneByPath(gameplayScenePath);
            var closeAfterValidation = !scene.isLoaded;
            if (closeAfterValidation)
            {
                scene = EditorSceneManager.OpenScene(gameplayScenePath, OpenSceneMode.Additive);
            }

            try
            {
                var result = ScareScenarioSceneValidator.ValidateScene(scene);
                Assert.That(result.IsValid, Is.True,
                    result.Issues.Count > 0 ? result.Issues[0].ToDiagnostic() : string.Empty);
            }
            finally
            {
                if (closeAfterValidation)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private ScareDefinition CreateDefinition(string scareId)
        {
            var definition = Track(ScriptableObject.CreateInstance<ScareDefinition>());
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("scareId").stringValue = scareId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private ScareScenarioAdapter CreateAdapter(string name, params ScareDefinition[] definitions)
        {
            var gameObject = Track(new GameObject(name));
            gameObject.SetActive(false);
            var adapter = gameObject.AddComponent<ScareScenarioAdapter>();
            var serialized = new SerializedObject(adapter);
            var property = serialized.FindProperty("scares");
            property.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return adapter;
        }

        private ScareScenarioTrigger CreateTrigger(string name, string scareId)
        {
            var gameObject = Track(new GameObject(name));
            gameObject.SetActive(false);
            var trigger = gameObject.AddComponent<ScareScenarioTrigger>();
            var serialized = new SerializedObject(trigger);
            serialized.FindProperty("scareId").stringValue = scareId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return trigger;
        }

        private T Track<T>(T target) where T : Object
        {
            createdObjects.Add(target);
            return target;
        }
    }
}
