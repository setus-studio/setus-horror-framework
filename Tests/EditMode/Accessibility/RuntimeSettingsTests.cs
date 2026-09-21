using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.SaveProgression.Migration;
using Setus.HorrorFramework.SaveProgression.Registry;
using Setus.HorrorFramework.SaveProgression.SaveSlots;
using Setus.HorrorFramework.SaveProgression.Serialization;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
using Setus.HorrorFramework.UI.Settings.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Tests.EditMode.Accessibility
{
    public sealed class RuntimeSettingsTests
    {
        [Test]
        public void AccessibilitySettingsRoundTripThroughOwner()
        {
            var owner = new RuntimeSettingsModel();
            owner.SetMasterVolume(0.8f);
            owner.SetAmbienceVolume(0.7f);
            owner.SetSfxVolume(0.6f);
            owner.SetUiVolume(0.5f);
            owner.SetVoiceVolume(0.4f);
            owner.SetBrightness(0.65f);
            owner.SetCameraShakeIntensity(0.25f);
            owner.SetHeadBobEnabled(false);
            owner.SetHeadBobIntensity(0.3f);
            owner.SetSprintInputMode(SprintInputMode.Toggle);
            owner.SetLocaleCode("vi");
            owner.SetInputBindingOverridesJson("{\"bindings\":[]}");

            var captured = owner.CaptureState();
            owner.RestoreState(new RuntimeSettingsState());
            owner.RestoreState(captured);

            Assert.That(owner.Current.MasterVolume, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(owner.Current.AmbienceVolume, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(owner.Current.SfxVolume, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(owner.Current.UiVolume, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(owner.Current.VoiceVolume, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(owner.Current.Brightness, Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(owner.Current.CameraShakeIntensity, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(owner.Current.HeadBobEnabled, Is.False);
            Assert.That(owner.Current.HeadBobIntensity, Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(owner.Current.SprintInputMode, Is.EqualTo(SprintInputMode.Toggle));
            Assert.That(owner.Current.LocaleCode, Is.EqualTo("vi"));
            Assert.That(owner.Current.InputBindingOverridesJson, Does.Contain("bindings"));
        }

        [Test]
        public void TabResetsRestoreOnlyTheirOwnedPreferences()
        {
            var owner = new RuntimeSettingsModel();
            owner.SetMasterVolume(0.2f);
            owner.SetAmbienceVolume(0.3f);
            owner.SetSfxVolume(0.4f);
            owner.SetUiVolume(0.5f);
            owner.SetVoiceVolume(0.6f);
            owner.SetMouseSensitivity(2f);
            owner.SetSprintInputMode(SprintInputMode.Toggle);
            owner.SetBrightness(0.1f);
            owner.SetSubtitlesEnabled(false);
            owner.SetCameraShakeIntensity(0.2f);
            owner.SetHeadBobEnabled(false);
            owner.SetHeadBobIntensity(0.3f);
            owner.SetLocaleCode("vi");
            owner.SetInputBindingOverridesJson("{\"bindings\":[]}");
            var changes = 0;
            owner.Changed += _ => changes++;

            owner.ResetAudioDefaults();
            Assert.That(owner.Current.MasterVolume, Is.EqualTo(1f));
            Assert.That(owner.Current.AmbienceVolume, Is.EqualTo(1f));
            Assert.That(owner.Current.SfxVolume, Is.EqualTo(1f));
            Assert.That(owner.Current.UiVolume, Is.EqualTo(1f));
            Assert.That(owner.Current.VoiceVolume, Is.EqualTo(1f));
            Assert.That(owner.Current.MouseSensitivity, Is.EqualTo(2f));
            Assert.That(owner.Current.Brightness, Is.EqualTo(0.1f).Within(0.001f));

            owner.ResetMovementDefaults();
            Assert.That(owner.Current.MouseSensitivity, Is.EqualTo(1f));
            Assert.That(owner.Current.SprintInputMode, Is.EqualTo(SprintInputMode.Hold));
            Assert.That(owner.Current.Brightness, Is.EqualTo(0.1f).Within(0.001f));

            owner.ResetAccessibilityDefaults();
            Assert.That(owner.Current.SubtitlesEnabled, Is.True);
            Assert.That(owner.Current.Brightness, Is.EqualTo(0.5f));
            Assert.That(owner.Current.CameraShakeIntensity, Is.EqualTo(1f));
            Assert.That(owner.Current.HeadBobEnabled, Is.True);
            Assert.That(owner.Current.HeadBobIntensity, Is.EqualTo(1f));
            Assert.That(owner.Current.LocaleCode, Is.EqualTo("vi"));
            Assert.That(owner.Current.InputBindingOverridesJson, Is.EqualTo("{\"bindings\":[]}"));
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void RebindConflictRejectsDuplicateAndReservedEscapeWithoutChangingOtherBindings()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            try
            {
                var map = new InputActionMap("Player");
                var move = map.AddAction("Move");
                move.AddBinding("<Keyboard>/w");
                var interact = map.AddAction("Interact");
                interact.AddBinding("<Keyboard>/e");
                var pause = map.AddAction("Pause");
                pause.AddBinding("<Keyboard>/escape");
                asset.AddActionMap(map);

                move.ApplyBindingOverride(0, "<Keyboard>/e");
                Assert.That(InputRebindPresenter.TryFindConflict(asset, move, 0, out var conflict), Is.True);
                Assert.That(conflict, Does.Contain("Interact"));
                Assert.That(interact.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/e"));

                pause.ApplyBindingOverride(0, "<Keyboard>/p");
                move.ApplyBindingOverride(0, "<Keyboard>/escape");
                Assert.That(InputRebindPresenter.TryFindConflict(asset, move, 0, out conflict), Is.True);
                Assert.That(conflict, Does.Contain("Pause"));

                move.ApplyBindingOverride(0, "<Keyboard>/upArrow");
                Assert.That(InputRebindPresenter.TryFindConflict(asset, move, 0, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void DisplayPreviewCommitsOnlyAfterConfirmation()
        {
            var owner = new RuntimeSettingsModel();
            var baseline = Display(GameDisplayMode.Windowed, 1280, 720, false, 60, "Medium");
            var candidate = Display(GameDisplayMode.Borderless, 1920, 1080, true, 0, "High");
            var device = new FakeDisplaySettingsDevice(baseline);
            var preview = new DisplaySettingsPreviewSession(owner, device);

            Assert.That(preview.Begin(candidate, 10f), Is.True);
            Assert.That(device.Current, Is.SameAs(candidate));
            Assert.That(owner.Current.DisplaySettings, Is.Null);
            Assert.That(preview.Confirm(10.1f), Is.False);
            Assert.That(preview.Confirm(10.3f), Is.True);

            Assert.That(owner.Current.DisplaySettings, Is.SameAs(candidate));
            Assert.That(preview.IsActive, Is.False);
        }

        [Test]
        public void DisplayPreviewTimeoutRestoresExactRuntimeBaselineWithoutCommitting()
        {
            var owner = new RuntimeSettingsModel();
            var baseline = Display(GameDisplayMode.Fullscreen, 1920, 1080, true, 10, "High");
            var candidate = Display(GameDisplayMode.Windowed, 1280, 720, false, 60, "Low");
            var device = new FakeDisplaySettingsDevice(baseline);
            var preview = new DisplaySettingsPreviewSession(owner, device);

            Assert.That(preview.Begin(candidate, 2f), Is.True);
            Assert.That(preview.Tick(17f), Is.EqualTo(DisplayPreviewResult.TimedOut));

            Assert.That(device.Current, Is.SameAs(baseline));
            Assert.That(owner.Current.DisplaySettings, Is.Null);
            Assert.That(device.ApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void AntiAliasingPreviewRevertRestoresPreviousModeWithoutSaving()
        {
            var owner = new RuntimeSettingsModel();
            var baseline = new DisplaySettingsState(GameDisplayMode.Windowed,
                1280, 720, 0, 0, 0, 60, "High", GameAntiAliasingMode.Fxaa);
            var candidate = new DisplaySettingsState(GameDisplayMode.Windowed,
                1280, 720, 0, 0, 0, 60, "Low", GameAntiAliasingMode.Smaa);
            var device = new FakeDisplaySettingsDevice(baseline);
            var preview = new DisplaySettingsPreviewSession(owner, device);

            Assert.That(preview.Begin(candidate, 0f), Is.True);
            Assert.That(device.Current.AntiAliasing, Is.EqualTo(GameAntiAliasingMode.Smaa));
            Assert.That(preview.Revert(), Is.EqualTo(DisplayPreviewResult.Reverted));
            Assert.That(device.Current.AntiAliasing, Is.EqualTo(GameAntiAliasingMode.Fxaa));
            Assert.That(owner.Current.DisplaySettings, Is.Null);
        }

        [Test]
        public void LegacyDisplayJsonWithoutAntiAliasingDefaultsToOff()
        {
            var state = JsonUtility.FromJson<RuntimeSettingsState>(
                "{\"displaySettings\":{\"configured\":true,\"mode\":0,\"width\":1280," +
                "\"height\":720,\"qualityName\":\"PC\"}}");

            Assert.That(state.DisplaySettings.AntiAliasing, Is.EqualTo(GameAntiAliasingMode.Off));
            Assert.That(state.DisplaySettings.QualityName, Is.EqualTo("PC"));
            Assert.That(state.DisplaySettings.TryValidate(out _), Is.True);
        }

        [Test]
        public void UnsupportedAntiAliasingFailsSemanticValidation()
        {
            var state = new DisplaySettingsState(GameDisplayMode.Windowed,
                1280, 720, 0, 0, 0, 60, "High", (GameAntiAliasingMode)99);

            Assert.That(state.TryValidate(out var reason), Is.False);
            Assert.That(reason, Does.Contain("Anti-aliasing"));
        }

        [Test]
        public void RejectedDisplayPreviewLeavesOwnerAndRuntimeUnchanged()
        {
            var owner = new RuntimeSettingsModel();
            var baseline = Display(GameDisplayMode.Windowed, 1280, 720, false, 60, "Medium");
            var candidate = Display(GameDisplayMode.Fullscreen, 3840, 2160, true, 240, "Ultra");
            var device = new FakeDisplaySettingsDevice(baseline) { RejectNextApply = true };
            var preview = new DisplaySettingsPreviewSession(owner, device);

            Assert.That(preview.Begin(candidate, 0f), Is.False);

            Assert.That(device.Current, Is.SameAs(baseline));
            Assert.That(owner.Current.DisplaySettings, Is.Null);
            Assert.That(preview.IsActive, Is.False);
        }

        [Test]
        public void DisplayThatIsNotAcceptedAfterApplyRevertsWithoutCommitting()
        {
            var owner = new RuntimeSettingsModel();
            var baseline = Display(GameDisplayMode.Windowed, 1280, 720, false, 60, "Medium");
            var candidate = Display(GameDisplayMode.Fullscreen, 1920, 1080, true, 144, "High");
            var device = new FakeDisplaySettingsDevice(baseline);
            var preview = new DisplaySettingsPreviewSession(owner, device);

            Assert.That(preview.Begin(candidate, 1f), Is.True);
            device.ReportMismatch = true;
            Assert.That(preview.Tick(3f), Is.EqualTo(DisplayPreviewResult.ApplyFailed));

            Assert.That(device.Current, Is.SameAs(baseline));
            Assert.That(owner.Current.DisplaySettings, Is.Null);
        }

        [Test]
        public void InvalidDisplaySettingsFailSemanticValidation()
        {
            var owner = new RuntimeSettingsModel();
            var invalid = new DisplaySettingsState(GameDisplayMode.Windowed,
                0, 720, 0, 0, 0, 60, "Medium");

            var result = owner.ValidateRestoreState(new RuntimeSettingsState(displaySettings: invalid));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason, Does.Contain("resolution"));
        }

        private static DisplaySettingsState Display(GameDisplayMode mode, int width, int height,
            bool vSync, int cap, string quality) =>
            new DisplaySettingsState(mode, width, height, 0, 0, vSync ? 1 : 0, cap, quality);

        private sealed class FakeDisplaySettingsDevice : IDisplaySettingsDevice
        {
            public FakeDisplaySettingsDevice(DisplaySettingsState current) => Current = current;

            public DisplaySettingsState Current { get; private set; }
            public bool RejectNextApply { get; set; }
            public bool ReportMismatch { get; set; }
            public int ApplyCount { get; private set; }

            public DisplaySettingsState CaptureCurrent() => Current;

            public bool TryApply(DisplaySettingsState settings, out string reason)
            {
                ApplyCount++;
                if (RejectNextApply)
                {
                    RejectNextApply = false;
                    reason = "Simulated unsupported display mode.";
                    return false;
                }
                Current = settings;
                reason = string.Empty;
                return true;
            }

            public bool Matches(DisplaySettingsState settings) =>
                !ReportMismatch && ReferenceEquals(Current, settings);
        }

        [Test]
        public void SchemaThreeSettingsMigrateToVersionFourDefaults()
        {
            var types = HorrorSaveStateTypeRegistry.CreateDefault();
            var legacy = types.Resolve(SaveStateTypeKeys.RuntimeSettingsV1).Deserialize(
                "{\"masterVolume\":0.42,\"mouseSensitivity\":1.5,\"subtitlesEnabled\":false}");
            var snapshot = new SaveGameSnapshot(
                3,
                new SaveSlotId("m11-legacy"),
                null,
                new[]
                {
                    new SaveStateRecord(
                        HorrorGlobalSaveStateRegistry.RuntimeSettingsStateKey,
                        SaveStateTypeKeys.RuntimeSettingsV1,
                        legacy)
                },
                Array.Empty<StableSaveStateRecord>());
            var pipeline = new SaveMigrationPipeline();
            HorrorSaveMigrations.RegisterDefaults(pipeline);

            var migrated = pipeline.MigrateToCurrent(snapshot);
            var record = migrated.GlobalStates.Single();
            var settings = (RuntimeSettingsState)record.State;

            Assert.That(migrated.SchemaVersion, Is.EqualTo(SaveSchemaVersion.Current));
            Assert.That(record.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.RuntimeSettingsV2));
            Assert.That(settings.MasterVolume, Is.EqualTo(0.42f).Within(0.001f));
            Assert.That(settings.MouseSensitivity, Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(settings.SubtitlesEnabled, Is.False);
            Assert.That(settings.Brightness, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(settings.HeadBobEnabled, Is.True);
            Assert.That(settings.SprintInputMode, Is.EqualTo(SprintInputMode.Hold));
        }

        [Test]
        public void SchemaThreeLegacyClrAliasSettingsReceiveVersionFourDefaults()
        {
            var snapshot = new SaveGameSnapshot(
                3,
                new SaveSlotId("m11-legacy-clr-alias"),
                null,
                new[]
                {
                    new SaveStateRecord(
                        HorrorGlobalSaveStateRegistry.RuntimeSettingsStateKey,
                        SaveStateTypeKeys.RuntimeSettingsV2,
                        new RuntimeSettingsState(
                            masterVolume: 0.35f,
                            mouseSensitivity: 1.75f,
                            subtitlesEnabled: false,
                            brightness: 0f,
                            headBobEnabled: false,
                            localeCode: "legacy-decoder-empty-field"))
                },
                Array.Empty<StableSaveStateRecord>());
            var pipeline = new SaveMigrationPipeline();
            HorrorSaveMigrations.RegisterDefaults(pipeline);

            var migrated = pipeline.MigrateToCurrent(snapshot);
            var settings = (RuntimeSettingsState)migrated.GlobalStates.Single().State;

            Assert.That(settings.MasterVolume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(settings.MouseSensitivity, Is.EqualTo(1.75f).Within(0.001f));
            Assert.That(settings.SubtitlesEnabled, Is.False);
            Assert.That(settings.Brightness, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(settings.HeadBobEnabled, Is.True);
            Assert.That(settings.LocaleCode, Is.EqualTo("en"));
        }

        [Test]
        public void CurrentRegistryUsesStableSettingsV2Key()
        {
            var types = HorrorSaveStateTypeRegistry.CreateDefault();
            var declaration = HorrorGlobalSaveStateRegistry.CreateDefault()
                .Declarations.Single(item => item.StateKey == HorrorGlobalSaveStateRegistry.RuntimeSettingsStateKey);

            Assert.That(types.Resolve(typeof(RuntimeSettingsState)).TypeKey, Is.EqualTo(SaveStateTypeKeys.RuntimeSettingsV2));
            Assert.That(declaration.StateTypeKey, Is.EqualTo(SaveStateTypeKeys.RuntimeSettingsV2));
        }

        [Test]
        public void KeylessLocalizedReferenceUsesAuthoredFallback()
        {
            var reference = new LocalizedTextReference(string.Empty, "Fallback text");
            Assert.That(LocalizationTextResolver.Resolve(reference), Is.EqualTo("Fallback text"));
        }

        [Test]
        public void SemanticValidationRejectsInvalidAccessibilityRange()
        {
            var state = new RuntimeSettingsState(cameraShakeIntensity: float.NaN);
            var result = new RuntimeSettingsModel().ValidateRestoreState(state);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void SemanticValidationAcceptsCurrentAndLegacyBindingOverridePayloads()
        {
            const string currentJson =
                "{\"bindings\":[{\"action\":\"Player/Move\",\"id\":\"11111111-1111-1111-1111-111111111111\",\"path\":\"<Keyboard>/upArrow\",\"interactions\":\"null\",\"processors\":\"null\"}]}";
            var owner = new RuntimeSettingsModel();

            Assert.That(
                owner.ValidateRestoreState(new RuntimeSettingsState(inputBindingOverridesJson: currentJson)).IsValid,
                Is.True);
            Assert.That(
                owner.ValidateRestoreState(new RuntimeSettingsState(inputBindingOverridesJson: string.Empty)).IsValid,
                Is.True);
        }

        [Test]
        public void SemanticValidationRejectsMalformedBindingOverrideJson()
        {
            var state = new RuntimeSettingsState(inputBindingOverridesJson: "{\"bindings\":[");
            var result = new RuntimeSettingsModel().ValidateRestoreState(state);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason, Does.Contain("binding").IgnoreCase);
        }

        [TestCase("{}")]
        [TestCase("{\"bindings\":null}")]
        [TestCase("{\"bindings\":{}}")]
        [TestCase("{\"bindings\":[{}]}")]
        [TestCase("{\"bindings\":[\"invalid\"]}")]
        [TestCase("{\"bindings\":[{\"id\":\"not-a-guid\"}]}")]
        public void SemanticValidationRejectsStructurallyInvalidBindingOverrideJson(string json)
        {
            var result = new RuntimeSettingsModel().ValidateRestoreState(
                new RuntimeSettingsState(inputBindingOverridesJson: json));

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason, Does.Contain("binding").IgnoreCase);
        }

        [Test]
        public void SlotCompatibilityRejectsMalformedBindingOverridesWithoutMutatingSettings()
        {
            var owner = new RuntimeSettingsModel();
            owner.SetInputBindingOverridesJson("{\"bindings\":[]}");
            var before = owner.Current;
            var declarations = new GlobalSaveStateRegistry();
            declarations.Register(new GlobalSaveStateDeclaration(
                owner.StateKey,
                SaveStateTypeKeys.RuntimeSettingsV2,
                GlobalSaveStateRequirement.Required,
                SaveSchemaVersion.Current,
                GlobalSaveStateOwnerScope.Context,
                GlobalSaveStateIntroductionPolicy.MigrationProvidesRecord));
            var registry = new SaveableRegistry(HorrorSaveStateTypeRegistry.CreateDefault(), declarations);
            using (registry.RegisterGlobal(owner))
            {
                var report = registry.ValidateSlotRestoreCompatibility(
                    new[]
                    {
                        new SaveStateRecord(
                            owner.StateKey,
                            SaveStateTypeKeys.RuntimeSettingsV2,
                            new RuntimeSettingsState(inputBindingOverridesJson: "{\"bindings\":["))
                    },
                    Array.Empty<StableSaveStateRecord>(),
                    null);

                Assert.That(report.HasErrors, Is.True);
                Assert.That(report.Issues.Any(issue => issue.Code == "state-semantic-invalid"), Is.True);
                Assert.That(owner.Current, Is.SameAs(before));
            }
        }
    }

    public sealed class RuntimeSettingsPersistenceTests
    {
        private string testDirectory;

        [SetUp]
        public void SetUp()
        {
            HorrorGameContext.ShutdownActive();
            testDirectory = Path.Combine(
                Path.GetTempPath(),
                "SetusHorrorFrameworkSettingsTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            HorrorGameContext.ShutdownActive();
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, true);
            }
        }

        [Test]
        public void SettingsSurviveContextRecreationWithoutAGameplaySave()
        {
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var settingsPath = GetSettingsPath();
            const string overridesJson =
                "{\"bindings\":[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"path\":\"<Keyboard>/upArrow\"}]}";

            try
            {
                var first = HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(settingsPath));
                first.RuntimeSettings.SetMasterVolume(0.81f);
                first.RuntimeSettings.SetAmbienceVolume(0.71f);
                first.RuntimeSettings.SetSfxVolume(0.61f);
                first.RuntimeSettings.SetUiVolume(0.51f);
                first.RuntimeSettings.SetVoiceVolume(0.41f);
                first.RuntimeSettings.SetMouseSensitivity(1.7f);
                first.RuntimeSettings.SetSubtitlesEnabled(false);
                first.RuntimeSettings.SetBrightness(0.31f);
                first.RuntimeSettings.SetCameraShakeIntensity(0.21f);
                first.RuntimeSettings.SetHeadBobEnabled(false);
                first.RuntimeSettings.SetHeadBobIntensity(0.11f);
                first.RuntimeSettings.SetSprintInputMode(SprintInputMode.Toggle);
                first.RuntimeSettings.SetLocaleCode("vi");
                first.RuntimeSettings.SetInputBindingOverridesJson(overridesJson);

                HorrorGameContext.ShutdownActive();
                var recreated = HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(settingsPath));

                AssertSettings(
                    recreated.RuntimeSettings.Current,
                    master: 0.81f,
                    ambience: 0.71f,
                    sfx: 0.61f,
                    ui: 0.51f,
                    voice: 0.41f,
                    sensitivity: 1.7f,
                    subtitles: false,
                    brightness: 0.31f,
                    shake: 0.21f,
                    headBobEnabled: false,
                    headBob: 0.11f,
                    sprint: SprintInputMode.Toggle,
                    locale: "vi",
                    bindingJson: overridesJson);
            }
            finally
            {
                HorrorGameContext.ShutdownActive();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void NewGameResetDoesNotResetUserPreferences()
        {
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            try
            {
                var context = HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(GetSettingsPath()));
                context.RuntimeSettings.SetBrightness(0.23f);
                context.RuntimeSettings.SetLocaleCode("vi");

                context.ResetForNewGameSession();

                Assert.That(context.RuntimeSettings.Current.Brightness, Is.EqualTo(0.23f).Within(0.001f));
                Assert.That(context.RuntimeSettings.Current.LocaleCode, Is.EqualTo("vi"));
            }
            finally
            {
                HorrorGameContext.ShutdownActive();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SaveSlotSettingsCannotOverrideExistingAppPreferences()
        {
            var settingsPath = GetSettingsPath();
            var authored = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));
            authored.SetMasterVolume(0.27f);
            authored.SetLocaleCode("vi");
            authored.Flush();

            var restarted = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));
            restarted.RestoreState(new RuntimeSettingsState(masterVolume: 0.92f, localeCode: "en"));

            Assert.That(restarted.Current.MasterVolume, Is.EqualTo(0.27f).Within(0.001f));
            Assert.That(restarted.Current.LocaleCode, Is.EqualTo("vi"));
        }

        [Test]
        public void FirstLegacySaveSettingsImportOnceAfterSuccessfulRestore()
        {
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var settingsPath = GetSettingsPath();
            try
            {
                var context = HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(settingsPath));
                var types = HorrorSaveStateTypeRegistry.CreateDefault();
                var legacyState = types.Resolve(SaveStateTypeKeys.RuntimeSettingsV1).Deserialize(
                    "{\"masterVolume\":0.38,\"mouseSensitivity\":1.6,\"subtitlesEnabled\":false}");
                var legacySnapshot = new SaveGameSnapshot(
                    3,
                    new SaveSlotId("legacy-settings"),
                    null,
                    new[]
                    {
                        new SaveStateRecord(
                            HorrorGlobalSaveStateRegistry.RuntimeSettingsStateKey,
                            SaveStateTypeKeys.RuntimeSettingsV1,
                            legacyState)
                    },
                    Array.Empty<StableSaveStateRecord>());
                var migrations = new SaveMigrationPipeline();
                HorrorSaveMigrations.RegisterDefaults(migrations);
                var imported = (RuntimeSettingsState)migrations
                    .MigrateToCurrent(legacySnapshot)
                    .GlobalStates.Single()
                    .State;

                context.RuntimeSettings.RestoreState(imported);
                Assert.That(context.RuntimeSettings.Current.MasterVolume, Is.EqualTo(1f));

                context.Events.Publish(new SaveRestoreCompleted(
                    new SaveSlotId("legacy-settings"),
                    null,
                    new SaveRestoreReport()));

                Assert.That(context.RuntimeSettings.Current.MasterVolume, Is.EqualTo(0.38f).Within(0.001f));
                Assert.That(context.RuntimeSettings.Current.MouseSensitivity, Is.EqualTo(1.6f).Within(0.001f));
                Assert.That(context.RuntimeSettings.Current.SubtitlesEnabled, Is.False);

                HorrorGameContext.ShutdownActive();
                var recreated = HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(settingsPath));
                recreated.RuntimeSettings.RestoreState(
                    new RuntimeSettingsState(masterVolume: 0.9f, localeCode: "vi"));

                Assert.That(recreated.RuntimeSettings.Current.MasterVolume, Is.EqualTo(0.38f).Within(0.001f));
                Assert.That(recreated.RuntimeSettings.Current.LocaleCode, Is.EqualTo("en"));
            }
            finally
            {
                HorrorGameContext.ShutdownActive();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void RolledBackLegacyImportDoesNotMutateOrCreateAppSettings()
        {
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var settingsPath = GetSettingsPath();
            try
            {
                var context = HorrorGameContext.Initialize(
                    config,
                    new PersistentUserSettingsStore(settingsPath));
                var changedCount = 0;
                context.RuntimeSettings.Changed += _ => changedCount++;
                var rollback = context.RuntimeSettings.CaptureRollbackAction();

                context.RuntimeSettings.RestoreState(
                    new RuntimeSettingsState(masterVolume: 0.2f, localeCode: "vi"));
                rollback();

                Assert.That(context.RuntimeSettings.Current.MasterVolume, Is.EqualTo(1f));
                Assert.That(context.RuntimeSettings.Current.LocaleCode, Is.EqualTo("en"));
                Assert.That(changedCount, Is.Zero);

                HorrorGameContext.ShutdownActive();
                Assert.That(
                    new PersistentUserSettingsStore(settingsPath).Load().Status,
                    Is.EqualTo(UserSettingsLoadStatus.Missing));
            }
            finally
            {
                HorrorGameContext.ShutdownActive();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void CorruptPrimaryRecoversLastKnownGoodSettings()
        {
            var store = new PersistentUserSettingsStore(GetSettingsPath());
            store.Save(new RuntimeSettingsState(masterVolume: 0.44f, localeCode: "vi"));
            store.Save(new RuntimeSettingsState(masterVolume: 0.55f, localeCode: "en"));
            File.WriteAllText(store.FilePath, "{invalid-json");

            var recovered = new RuntimeSettingsModel(
                new PersistentUserSettingsStore(store.FilePath));

            Assert.That(recovered.LoadStatus, Is.EqualTo(UserSettingsLoadStatus.RecoveredFromBackup));
            Assert.That(recovered.Current.MasterVolume, Is.EqualTo(0.44f).Within(0.001f));
            Assert.That(recovered.Current.LocaleCode, Is.EqualTo("vi"));
        }

        [Test]
        public void CorruptSettingsWithoutBackupUseDefaultsAndDoNotImportASlot()
        {
            var settingsPath = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath, "{invalid-json");

            var settings = new RuntimeSettingsModel(
                new PersistentUserSettingsStore(settingsPath));
            settings.RestoreState(new RuntimeSettingsState(masterVolume: 0.2f, localeCode: "vi"));

            Assert.That(settings.LoadStatus, Is.EqualTo(UserSettingsLoadStatus.Invalid));
            Assert.That(settings.Current.MasterVolume, Is.EqualTo(1f));
            Assert.That(settings.Current.LocaleCode, Is.EqualTo("en"));
            Assert.That(settings.HasAuthoritativeUserSettings, Is.True);
            Assert.That(settings.PersistenceDiagnostic, Is.Not.Empty);
        }

        [Test]
        public void InputBindingOverridesPersistWithoutAGameplaySlot()
        {
            const string overridesJson =
                "{\"bindings\":[{\"id\":\"22222222-2222-2222-2222-222222222222\",\"path\":\"<Keyboard>/downArrow\"}]}";
            var settingsPath = GetSettingsPath();
            var first = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));
            first.SetInputBindingOverridesJson(overridesJson);
            first.Flush();

            var recreated = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));

            Assert.That(recreated.Current.InputBindingOverridesJson, Is.EqualTo(overridesJson));
            Assert.That(recreated.ValidateRestoreState(recreated.Current).IsValid, Is.True);
        }

        [Test]
        public void DisplaySettingsPersistWithoutAGameplaySlot()
        {
            var settingsPath = GetSettingsPath();
            var display = new DisplaySettingsState(GameDisplayMode.Borderless,
                2560, 1440, 0, 0, 1, 144, "High");
            var first = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));
            first.SetDisplaySettings(display);
            first.Flush();

            var recreated = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));

            Assert.That(recreated.LoadStatus, Is.EqualTo(UserSettingsLoadStatus.Loaded));
            Assert.That(recreated.Current.DisplaySettings, Is.Not.Null);
            Assert.That(recreated.Current.DisplaySettings.Mode, Is.EqualTo(GameDisplayMode.Borderless));
            Assert.That(recreated.Current.DisplaySettings.Width, Is.EqualTo(2560));
            Assert.That(recreated.Current.DisplaySettings.Height, Is.EqualTo(1440));
            Assert.That(recreated.Current.DisplaySettings.VSyncCount, Is.EqualTo(1));
            Assert.That(recreated.Current.DisplaySettings.FrameRateCap, Is.EqualTo(144));
            Assert.That(recreated.Current.DisplaySettings.QualityName, Is.EqualTo("High"));
        }

        [Test]
        public void ExistingSettingsDocumentWithoutDisplayProfileRemainsCompatible()
        {
            var settingsPath = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath,
                "{\"formatVersion\":1,\"settings\":{" +
                "\"masterVolume\":1,\"mouseSensitivity\":1,\"subtitlesEnabled\":true," +
                "\"ambienceVolume\":1,\"sfxVolume\":1,\"uiVolume\":1,\"voiceVolume\":1," +
                "\"brightness\":0.5,\"cameraShakeIntensity\":1,\"headBobEnabled\":true," +
                "\"headBobIntensity\":1,\"sprintInputMode\":0,\"localeCode\":\"en\"," +
                "\"inputBindingOverridesJson\":\"\"}}");

            var recreated = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));

            Assert.That(recreated.LoadStatus, Is.EqualTo(UserSettingsLoadStatus.Loaded));
            Assert.That(recreated.Current.DisplaySettings, Is.Null);
        }

        [Test]
        public void ExistingDisplayProfileWithoutConfiguredMarkerRemainsCompatible()
        {
            var settingsPath = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
            File.WriteAllText(settingsPath,
                "{\"formatVersion\":1,\"settings\":{" +
                "\"masterVolume\":1,\"mouseSensitivity\":1,\"subtitlesEnabled\":true," +
                "\"ambienceVolume\":1,\"sfxVolume\":1,\"uiVolume\":1,\"voiceVolume\":1," +
                "\"brightness\":0.5,\"cameraShakeIntensity\":1,\"headBobEnabled\":true," +
                "\"headBobIntensity\":1,\"sprintInputMode\":0,\"localeCode\":\"en\"," +
                "\"inputBindingOverridesJson\":\"\",\"displaySettings\":{" +
                "\"mode\":1,\"width\":1920,\"height\":1080," +
                "\"refreshNumerator\":0,\"refreshDenominator\":0," +
                "\"vSyncCount\":1,\"frameRateCap\":60,\"qualityName\":\"High\"}}}");

            var recreated = new RuntimeSettingsModel(new PersistentUserSettingsStore(settingsPath));

            Assert.That(recreated.LoadStatus, Is.EqualTo(UserSettingsLoadStatus.Loaded));
            Assert.That(recreated.Current.DisplaySettings, Is.Not.Null);
            Assert.That(recreated.Current.DisplaySettings.Mode, Is.EqualTo(GameDisplayMode.Borderless));
            Assert.That(recreated.Current.DisplaySettings.Width, Is.EqualTo(1920));
            Assert.That(recreated.Current.DisplaySettings.Height, Is.EqualTo(1080));
        }

        private string GetSettingsPath()
        {
            return Path.Combine(testDirectory, "user-settings.json");
        }

        private static void AssertSettings(
            RuntimeSettingsState state,
            float master,
            float ambience,
            float sfx,
            float ui,
            float voice,
            float sensitivity,
            bool subtitles,
            float brightness,
            float shake,
            bool headBobEnabled,
            float headBob,
            SprintInputMode sprint,
            string locale,
            string bindingJson)
        {
            Assert.That(state.MasterVolume, Is.EqualTo(master).Within(0.001f));
            Assert.That(state.AmbienceVolume, Is.EqualTo(ambience).Within(0.001f));
            Assert.That(state.SfxVolume, Is.EqualTo(sfx).Within(0.001f));
            Assert.That(state.UiVolume, Is.EqualTo(ui).Within(0.001f));
            Assert.That(state.VoiceVolume, Is.EqualTo(voice).Within(0.001f));
            Assert.That(state.MouseSensitivity, Is.EqualTo(sensitivity).Within(0.001f));
            Assert.That(state.SubtitlesEnabled, Is.EqualTo(subtitles));
            Assert.That(state.Brightness, Is.EqualTo(brightness).Within(0.001f));
            Assert.That(state.CameraShakeIntensity, Is.EqualTo(shake).Within(0.001f));
            Assert.That(state.HeadBobEnabled, Is.EqualTo(headBobEnabled));
            Assert.That(state.HeadBobIntensity, Is.EqualTo(headBob).Within(0.001f));
            Assert.That(state.SprintInputMode, Is.EqualTo(sprint));
            Assert.That(state.LocaleCode, Is.EqualTo(locale));
            Assert.That(state.InputBindingOverridesJson, Is.EqualTo(bindingJson));
        }
    }
}
