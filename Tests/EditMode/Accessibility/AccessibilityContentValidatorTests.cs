using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Accessibility.Display;
using Setus.HorrorFramework.Atmosphere.Audio;
using Setus.HorrorFramework.Audio.Settings;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
using Setus.HorrorFramework.UI.Menus;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Tests.EditMode.Accessibility
{
    public sealed class AccessibilityContentValidatorTests
    {
        [Test]
        public void CanonicalM11ContentPassesAccessibilityValidation()
        {
            var issues = AccessibilityContentValidator.ValidateProject();
            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void ProductionControlsValidationRejectsMissingPauseActionAsset()
        {
            var root = PrefabUtility.LoadPrefabContents(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            try
            {
                var pause = root.GetComponent<PauseInputAdapter>();
                Assert.IsNotNull(pause);
                var serialized = new SerializedObject(pause);
                serialized.FindProperty("actionsAsset").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var issues = AccessibilityContentValidator.ValidateUiShellReferences(root);
                Assert.That(issues.Any(issue => issue.Contains("PauseInputAdapter.actionsAsset")),
                    Is.True, string.Join("\n", issues));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void DisplayValidationRejectsMissingConfirmationReference()
        {
            var root = PrefabUtility.LoadPrefabContents(SceneFlowUiShellTemplateBuilder.UiShellPrefabPath);
            try
            {
                var display = root.GetComponentInChildren<DisplaySettingsPresenter>(true);
                Assert.IsNotNull(display, "Run Apply Display Settings before this validation test.");
                var serialized = new SerializedObject(display);
                serialized.FindProperty("keep").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var issues = AccessibilityContentValidator.ValidateUiShellReferences(root);
                Assert.That(issues.Any(issue => issue.Contains("field 'keep'")),
                    Is.True, string.Join("\n", issues));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void MissingSettingsReferenceFailsWithOwnerFieldAndReason()
        {
            var root = new GameObject("InvalidSettingsShell");
            try
            {
                root.AddComponent<AccessibilitySettingsPanel>();
                var issues = AccessibilityContentValidator.ValidateUiShellReferences(root);

                Assert.That(issues.Any(issue =>
                    issue.Contains(nameof(AccessibilitySettingsPanel)) &&
                    issue.Contains("masterVolume") &&
                    issue.Contains("missing")), Is.True, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissingRebindReferenceFailsWithOwnerFieldAndReason()
        {
            var root = new GameObject("InvalidRebindShell");
            try
            {
                root.AddComponent<InputRebindPresenter>();
                var issues = AccessibilityContentValidator.ValidateUiShellReferences(root);

                Assert.That(issues.Any(issue =>
                    issue.Contains(nameof(InputRebindPresenter)) &&
                    issue.Contains("actions") &&
                    issue.Contains("missing")), Is.True, string.Join("\n", issues));
                Assert.That(issues.Any(issue =>
                    issue.Contains(nameof(InputRebindPresenter)) &&
                    issue.Contains("statusText") &&
                    issue.Contains("missing")), Is.True, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EmptyLocalizedPresenterKeyFailsWithOwnerFieldAndReason()
        {
            var root = new GameObject("InvalidLocalizedText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            try
            {
                var presenter = root.AddComponent<LocalizedTextPresenter>();
                var serialized = new SerializedObject(presenter);
                serialized.FindProperty("entryKey").stringValue = string.Empty;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var issues = AccessibilityContentValidator.ValidateUiShellReferences(root);
                Assert.That(issues.Any(issue =>
                    issue.Contains(nameof(LocalizedTextPresenter)) &&
                    issue.Contains("entryKey") &&
                    issue.Contains("empty")), Is.True, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissingBrightnessApplierFailsWithOwnerFieldAndReason()
        {
            var root = new GameObject("GameplayWithoutBrightness");
            try
            {
                var issues = AccessibilityContentValidator.ValidateGameplayReferences(new[] { root });
                Assert.That(issues.Any(issue =>
                    issue.Contains(nameof(BrightnessVolumeSettingsApplier)) &&
                    issue.Contains("missing")), Is.True, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BrightnessApplierReferencingAnotherVolumeFailsValidation()
        {
            var root = new GameObject("BrightnessOwner");
            var other = new GameObject("OtherVolume");
            try
            {
                var applier = root.AddComponent<BrightnessVolumeSettingsApplier>();
                root.GetComponent<Volume>().isGlobal = true;
                var otherVolume = other.AddComponent<Volume>();
                otherVolume.isGlobal = true;
                var serialized = new SerializedObject(applier);
                serialized.FindProperty("volume").objectReferenceValue = otherVolume;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var issues = AccessibilityContentValidator.ValidateGameplayReferences(new[] { root, other });
                Assert.That(issues.Any(issue =>
                    issue.Contains(nameof(BrightnessVolumeSettingsApplier)) &&
                    issue.Contains("volume") &&
                    issue.Contains("co-located")), Is.True, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void OneAudioVolumeConsumerPassesValidation()
        {
            var shell = CreateAudioShell(("SFXVolumeRow", true, true));
            var sourceOwner = new GameObject("SfxSource");
            try
            {
                sourceOwner.AddComponent<AudioSource>();
                sourceOwner.AddComponent<AudioSourceSettingsBinding>();

                var issues = AccessibilityContentValidator.ValidateAudioSettingsReferences(
                    shell, new[] { sourceOwner });

                Assert.That(issues, Is.Empty, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(shell);
                Object.DestroyImmediate(sourceOwner);
            }
        }

        [Test]
        public void DuplicateAudioVolumeConsumersOnOneSourceFailValidation()
        {
            var sourceOwner = new GameObject("DuplicateSfxSource");
            try
            {
                var source = sourceOwner.AddComponent<AudioSource>();
                sourceOwner.AddComponent<AudioSourceSettingsBinding>();
                var scare = sourceOwner.AddComponent<ScareAudioCueHook>();
                var serialized = new SerializedObject(scare);
                serialized.FindProperty("cueSource").objectReferenceValue = source;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var issues = AccessibilityContentValidator.ValidateAudioSettingsReferences(
                    null, new[] { sourceOwner });

                Assert.That(issues.Any(issue =>
                    issue.Contains("multiple settings volume consumers") &&
                    issue.Contains(nameof(AudioSourceSettingsBinding)) &&
                    issue.Contains(nameof(ScareAudioCueHook))), Is.True, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(sourceOwner);
            }
        }

        [Test]
        public void ActiveAudioCategoryWithoutConsumerFailsValidation()
        {
            var shell = CreateAudioShell(("VoiceVolumeRow", true, true));
            try
            {
                var issues = AccessibilityContentValidator.ValidateAudioSettingsReferences(
                    shell, new GameObject[0]);

                Assert.That(issues.Any(issue =>
                    issue.Contains("Voice audio category") &&
                    issue.Contains("matching volume consumer")), Is.True, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(shell);
            }
        }

        [Test]
        public void HiddenOrDisabledOptionalAudioCategoriesPassValidation()
        {
            var shell = CreateAudioShell(
                ("UIVolumeRow", true, false),
                ("VoiceVolumeRow", false, true));
            try
            {
                var issues = AccessibilityContentValidator.ValidateAudioSettingsReferences(
                    shell, new GameObject[0]);

                Assert.That(issues, Is.Empty, string.Join("\n", issues));
            }
            finally
            {
                Object.DestroyImmediate(shell);
            }
        }

        private static GameObject CreateAudioShell(
            params (string Name, bool Active, bool Interactable)[] rows)
        {
            var shell = new GameObject("AudioSettingsShell", typeof(RectTransform));
            foreach (var rowDefinition in rows)
            {
                var row = new GameObject(rowDefinition.Name, typeof(RectTransform));
                row.transform.SetParent(shell.transform, false);
                var sliderOwner = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
                sliderOwner.transform.SetParent(row.transform, false);
                sliderOwner.GetComponent<Slider>().interactable = rowDefinition.Interactable;
                row.SetActive(rowDefinition.Active);
            }
            return shell;
        }
    }
}
