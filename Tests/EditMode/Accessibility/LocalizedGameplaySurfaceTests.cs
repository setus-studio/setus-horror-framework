using System.Collections.Generic;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Perception;
using Setus.HorrorFramework.AI.States;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.Interaction.Interactors;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Setus.HorrorFramework.Tests.EditMode.Accessibility
{
    public sealed class LocalizedGameplaySurfaceTests
    {
        [SetUp]
        public void SetUp()
        {
            HorrorGameContext.ShutdownActive();
        }

        [TearDown]
        public void TearDown()
        {
            var roots = UnityEngine.Object.FindObjectsByType<StableId>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (var index = 0; index < roots.Length; index++)
            {
                if (roots[index] != null && roots[index].name.StartsWith("localized-test-"))
                {
                    UnityEngine.Object.DestroyImmediate(roots[index].gameObject);
                }
            }

            UnityEngine.Object.DestroyImmediate(GameObject.Find("localized-test-interactor"));
            HorrorGameContext.ShutdownActive();
        }

        [Test]
        public void StandardInteractionPromptsExposeStableKeysAndEnglishFallbacks()
        {
            var context = HorrorGameContext.Ensure();
            var interactor = new GameObject("localized-test-interactor");
            var interactionContext = new InteractionContext(interactor, context);

            var door = CreateInteractable<DoorInteractable>("door");
            var drawer = CreateInteractable<DrawerInteractable>("drawer");
            var pickup = CreateInteractable<PickupInteractable>("pickup");
            SetString(pickup, "itemIdOverride", "localized-test-item");
            var inspectable = CreateInteractable<InspectableInteractable>("inspectable");

            AssertPrompt(door.GetPrompt(interactionContext), FrameworkTextKeys.DoorOpenPrompt, "Open");
            AssertPrompt(drawer.GetPrompt(interactionContext), FrameworkTextKeys.DrawerOpenPrompt, "Open drawer");
            AssertPrompt(pickup.GetPrompt(interactionContext), FrameworkTextKeys.PickupPrompt, "Pick up");
            AssertPrompt(inspectable.GetPrompt(interactionContext), FrameworkTextKeys.InspectablePrompt, "Inspect");

            var generic = new InteractionPrompt(
                new LocalizedTextReference(FrameworkTextKeys.InteractionPrompt, "Interact"),
                true,
                InteractionType.Use);
            AssertPrompt(generic, FrameworkTextKeys.InteractionPrompt, "Interact");
        }

        [Test]
        public void LegacyInteractionPromptAndAiFeedbackConstructorsRemainKeylessFallbacks()
        {
            var prompt = new InteractionPrompt("Legacy prompt", true, InteractionType.Use);
            var feedback = new StalkerAiHighStakesFeedbackRequested("legacy-stalker", "Legacy warning");

            Assert.That(prompt.LocalizedText.HasKey, Is.False);
            Assert.That(prompt.Text, Is.EqualTo("Legacy prompt"));
            Assert.That(feedback.SubtitleReference.HasKey, Is.False);
            Assert.That(feedback.SubtitleText, Is.EqualTo("Legacy warning"));
        }

        [Test]
        public void ChaseFeedbackCarriesStableLocalizationKeyAndAuthoredFallback()
        {
            var profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();
            try
            {
                var events = new GameplayEventBus();
                var model = new StalkerAiRuntimeModel("localized-test-stalker", events);
                var feedback = default(StalkerAiHighStakesFeedbackRequested);
                events.Subscribe<StalkerAiHighStakesFeedbackRequested>(value => feedback = value);
                model.Configure(profile, 1);

                var sight = new StalkerSightResult(StalkerSightReason.TargetConfirmed, Vector3.forward, null);
                model.Tick(0.1f, new StalkerPerceptionInput(sight, false));

                Assert.That(profile.ChaseSubtitleLocalizationKey, Is.EqualTo(FrameworkTextKeys.StalkerChaseSubtitle));
                Assert.That(feedback.SubtitleReference.EntryKey, Is.EqualTo(FrameworkTextKeys.StalkerChaseSubtitle));
                Assert.That(feedback.SubtitleText, Is.EqualTo("Something has noticed you."));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void NonEnglishLocaleResolvesByStableKeyAndMissingKeyUsesFallback()
        {
            var previousSettings = LocalizationSettings.GetInstanceDontCreateDefault();
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            var english = Locale.CreateLocale("en");
            var vietnamese = Locale.CreateLocale("vi");
            var provider = new TestStringTableProvider(new Dictionary<string, string>
            {
                { FrameworkTextKeys.DoorOpenPrompt, "Mo cua" },
                { FrameworkTextKeys.SaveErrorNoSave, "Khong co ban luu." }
            });

            try
            {
                LocalizationSettings.Instance = settings;
                var locales = new LocalesProvider();
                locales.AddLocale(english);
                locales.AddLocale(vietnamese);
                LocalizationSettings.AvailableLocales = locales;
                LocalizationSettings.StringDatabase.TableProvider = provider;
                LocalizationSettings.ProjectLocale = english;
                LocalizationSettings.SelectedLocale = vietnamese;
                LocalizationSettings.InitializeSynchronously = true;
                LocalizationSettings.InitializationOperation.WaitForCompletion();

                var translated = new LocalizedTextReference(FrameworkTextKeys.DoorOpenPrompt, "Open");
                var missing = new LocalizedTextReference("interaction.test.missing", "Safe fallback");
                var noSave = SaveLoadUserFailure.NoSave().Message;
                var missingSaveEntry = SaveLoadUserFailure.RestoreFailed().Message;

                Assert.That(LocalizationTextResolver.Resolve(translated), Is.EqualTo("Mo cua"));
                Assert.That(LocalizationTextResolver.Resolve(noSave), Is.EqualTo("Khong co ban luu."));
                Assert.That(LocalizationTextResolver.Resolve(missing), Is.EqualTo("Safe fallback"));
                Assert.That(
                    LocalizationTextResolver.Resolve(missingSaveEntry),
                    Is.EqualTo("The saved game could not be restored."));
            }
            finally
            {
                LocalizationSettings.Instance = previousSettings;
                provider.Dispose();
                UnityEngine.Object.DestroyImmediate(settings);
                UnityEngine.Object.DestroyImmediate(english);
                UnityEngine.Object.DestroyImmediate(vietnamese);
            }
        }

        private static T CreateInteractable<T>(string suffix) where T : Component
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = $"localized-test-{suffix}";
            target.AddComponent<StableId>().Assign($"localized.test.{suffix}");
            return target.AddComponent<T>();
        }

        private static void AssertPrompt(InteractionPrompt prompt, string key, string fallback)
        {
            Assert.That(prompt.LocalizedText.EntryKey, Is.EqualTo(key));
            Assert.That(prompt.Text, Is.EqualTo(fallback));
        }

        private static void SetString(UnityEngine.Object target, string fieldName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(fieldName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class TestStringTableProvider : ITableProvider, System.IDisposable
        {
            private readonly IReadOnlyDictionary<string, string> values;
            private readonly List<UnityEngine.Object> createdObjects = new List<UnityEngine.Object>();

            public TestStringTableProvider(IReadOnlyDictionary<string, string> values)
            {
                this.values = values;
            }

            public AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(
                string tableCollectionName,
                Locale locale)
                where TTable : LocalizationTable
            {
                if (typeof(TTable) != typeof(StringTable) ||
                    tableCollectionName != LocalizedTextReference.DefaultTable)
                {
                    return default;
                }

                var shared = ScriptableObject.CreateInstance<SharedTableData>();
                shared.TableCollectionName = tableCollectionName;
                var table = ScriptableObject.CreateInstance<StringTable>();
                table.SharedData = shared;
                table.LocaleIdentifier = locale.Identifier;
                foreach (var value in values)
                {
                    table.AddEntry(value.Key, value.Value);
                }

                createdObjects.Add(table);
                createdObjects.Add(shared);
                return Addressables.ResourceManager.CreateCompletedOperation(table as TTable, null);
            }

            public void Dispose()
            {
                for (var index = createdObjects.Count - 1; index >= 0; index--)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }

                createdObjects.Clear();
            }
        }
    }
}
