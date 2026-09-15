using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Accessibility.Display;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Settings;
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
    }
}
