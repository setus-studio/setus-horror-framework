using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Input;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Setus.HorrorFramework.Tests.PlayMode.Player
{
    public sealed class InputBindingRestoreTests
    {
        [UnityTest]
        public IEnumerator InvalidRestoreDoesNotMutateSettingsOrCurrentInputBindings()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Settings.SetInputBindingOverridesJson(fixture.UpArrowOverridesJson);
                yield return null;

                var beforeState = fixture.Settings.Current;
                var changedCount = 0;
                fixture.Settings.Changed += _ => changedCount++;

                var report = fixture.Registry.Restore(
                    new[]
                    {
                        new SaveStateRecord(
                            fixture.Settings.StateKey,
                            SaveStateTypeKeys.RuntimeSettingsV2,
                            new RuntimeSettingsState(inputBindingOverridesJson: "{\"bindings\":["))
                    },
                    Array.Empty<StableSaveStateRecord>(),
                    null);

                Assert.That(report.HasErrors, Is.True);
                Assert.That(fixture.Settings.Current, Is.SameAs(beforeState));
                Assert.That(fixture.Settings.Current.InputBindingOverridesJson, Is.EqualTo(fixture.UpArrowOverridesJson));
                Assert.That(fixture.MoveAction.bindings[0].overridePath, Is.EqualTo("<Keyboard>/upArrow"));
                Assert.That(changedCount, Is.Zero);
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ValidRestoreStillAppliesInputBindingOverrides()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Settings.SetInputBindingOverridesJson(fixture.UpArrowOverridesJson);
                yield return null;

                var downArrowJson = fixture.UpArrowOverridesJson.Replace(
                    "<Keyboard>/upArrow",
                    "<Keyboard>/downArrow");
                var report = fixture.Registry.Restore(
                    new[]
                    {
                        new SaveStateRecord(
                            fixture.Settings.StateKey,
                            SaveStateTypeKeys.RuntimeSettingsV2,
                            new RuntimeSettingsState(inputBindingOverridesJson: downArrowJson))
                    },
                    Array.Empty<StableSaveStateRecord>(),
                    null);
                yield return null;

                Assert.That(report.HasErrors, Is.False);
                Assert.That(fixture.Settings.Current.InputBindingOverridesJson, Is.EqualTo(downArrowJson));
                Assert.That(fixture.MoveAction.bindings[0].overridePath, Is.EqualTo("<Keyboard>/downArrow"));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator PersistedBindingOverridesApplyAfterContextRecreationWithoutASaveSlot()
        {
            HorrorGameContext.ShutdownActive();
            var testDirectory = Path.Combine(
                Path.GetTempPath(),
                "SetusHorrorFrameworkBindingTests",
                Guid.NewGuid().ToString("N"));
            var settingsPath = Path.Combine(testDirectory, "user-settings.json");
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            var move = map.AddAction("Move");
            move.AddBinding("<Keyboard>/w");
            actions.AddActionMap(map);
            move.ApplyBindingOverride(0, "<Keyboard>/upArrow");
            var overridesJson = actions.SaveBindingOverridesAsJson();
            move.RemoveAllBindingOverrides();
            GameObject inputObject = null;

            try
            {
                var first = HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(settingsPath));
                first.RuntimeSettings.SetInputBindingOverridesJson(overridesJson);
                first.RuntimeSettings.Flush();
                HorrorGameContext.ShutdownActive();

                HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(settingsPath));
                inputObject = new GameObject("persisted-input-binding-test");
                inputObject.SetActive(false);
                var input = inputObject.AddComponent<InputSystemPlayerInputSource>();
                input.ConfigureActionsAsset(actions);
                inputObject.SetActive(true);
                yield return null;

                Assert.That(move.bindings[0].overridePath, Is.EqualTo("<Keyboard>/upArrow"));
            }
            finally
            {
                if (inputObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(inputObject);
                }

                UnityEngine.Object.DestroyImmediate(actions);
                HorrorGameContext.ShutdownActive();
                UnityEngine.Object.DestroyImmediate(config);
                if (Directory.Exists(testDirectory))
                {
                    Directory.Delete(testDirectory, true);
                }
            }
        }

        private static Fixture CreateFixture()
        {
            HorrorGameContext.ShutdownActive();
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var settings = HorrorGameContext.Initialize(config).RuntimeSettings;
            var actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            var move = map.AddAction("Move");
            move.AddBinding("<Keyboard>/w");
            actions.AddActionMap(map);

            move.ApplyBindingOverride(0, "<Keyboard>/upArrow");
            var upArrowJson = actions.SaveBindingOverridesAsJson();
            move.RemoveAllBindingOverrides();

            var inputObject = new GameObject("input-binding-restore-test");
            inputObject.SetActive(false);
            var input = inputObject.AddComponent<InputSystemPlayerInputSource>();
            input.ConfigureActionsAsset(actions);
            inputObject.SetActive(true);

            var declarations = new GlobalSaveStateRegistry();
            declarations.Register(new GlobalSaveStateDeclaration(
                settings.StateKey,
                SaveStateTypeKeys.RuntimeSettingsV2,
                GlobalSaveStateRequirement.Required,
                SaveSchemaVersion.Current,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            var registry = new SaveableRegistry(HorrorSaveStateTypeRegistry.CreateDefault(), declarations);
            var registration = registry.RegisterGlobal(settings);

            return new Fixture(
                config,
                settings,
                actions,
                move,
                inputObject,
                registry,
                registration,
                upArrowJson);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly HorrorFrameworkConfig config;
            private readonly InputActionAsset actions;
            private readonly GameObject inputObject;
            private readonly IDisposable registration;

            public Fixture(
                HorrorFrameworkConfig config,
                RuntimeSettingsModel settings,
                InputActionAsset actions,
                InputAction moveAction,
                GameObject inputObject,
                SaveableRegistry registry,
                IDisposable registration,
                string upArrowOverridesJson)
            {
                this.config = config;
                this.actions = actions;
                this.inputObject = inputObject;
                this.registration = registration;
                Settings = settings;
                MoveAction = moveAction;
                Registry = registry;
                UpArrowOverridesJson = upArrowOverridesJson;
            }

            public RuntimeSettingsModel Settings { get; }
            public InputAction MoveAction { get; }
            public SaveableRegistry Registry { get; }
            public string UpArrowOverridesJson { get; }

            public void Dispose()
            {
                registration.Dispose();
                UnityEngine.Object.DestroyImmediate(inputObject);
                UnityEngine.Object.DestroyImmediate(actions);
                HorrorGameContext.ShutdownActive();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }
    }
}
