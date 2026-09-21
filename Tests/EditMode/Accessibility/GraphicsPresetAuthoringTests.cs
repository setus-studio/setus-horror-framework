using NUnit.Framework;
using Setus.HorrorFramework.Editor.Builders;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.UI.Settings;
using Setus.HorrorFramework.UI.Settings.Display;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Setus.HorrorFramework.Tests.EditMode.Accessibility
{
    public sealed class GraphicsPresetAuthoringTests
    {
        private const string LowPath = "Assets/Game/Settings/Graphics/Low_RPAsset.asset";
        private const string HighPath = "Assets/Game/Settings/Graphics/High_RPAsset.asset";

        [Test]
        public void UnityQualitySettingsExposePresetFields()
        {
            var settings = QualitySettings.GetQualitySettings();
            Assert.That(settings, Is.Not.Null);
            var serialized = new SerializedObject(settings);
            var levels = serialized.FindProperty("m_QualitySettings");
            Assert.That(levels, Is.Not.Null);
            Assert.That(levels.arraySize, Is.GreaterThan(0));
            var level = levels.GetArrayElementAtIndex(levels.arraySize - 1);
            foreach (var field in new[]
                     {
                         "name", "customRenderPipeline", "antiAliasing", "shadowDistance",
                         "lodBias", "globalTextureMipmapLimit", "excludedTargetPlatforms"
                     })
                Assert.That(level.FindPropertyRelative(field), Is.Not.Null, field);
        }

        [Test]
        public void DefaultPresetsAreDistinctAndDoNotMutateTheTemplate()
        {
            var template = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            UniversalRenderPipelineAsset low = null;
            UniversalRenderPipelineAsset medium = null;
            UniversalRenderPipelineAsset high = null;
            try
            {
                template.renderScale = 1f;
                template.msaaSampleCount = 4;
                low = GraphicsSettingsMilestoneBuilder.CreateDefaultPipelineAsset(template, "Low");
                medium = GraphicsSettingsMilestoneBuilder.CreateDefaultPipelineAsset(template, "Medium");
                high = GraphicsSettingsMilestoneBuilder.CreateDefaultPipelineAsset(template, "High");

                Assert.That(low.renderScale, Is.LessThan(medium.renderScale));
                Assert.That(medium.renderScale, Is.LessThan(high.renderScale));
                Assert.That(low.shadowDistance, Is.LessThan(medium.shadowDistance));
                Assert.That(medium.shadowDistance, Is.LessThan(high.shadowDistance));
                Assert.That(low.mainLightShadowmapResolution, Is.GreaterThanOrEqualTo(1024));
                Assert.That(low.shadowCascadeCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(low.supportsSoftShadows, Is.True);
                Assert.That(low.msaaSampleCount, Is.EqualTo(1));
                Assert.That(medium.msaaSampleCount, Is.EqualTo(1));
                Assert.That(high.msaaSampleCount, Is.EqualTo(1));
                Assert.That(template.renderScale, Is.EqualTo(1f));
                Assert.That(template.msaaSampleCount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(low);
                Object.DestroyImmediate(medium);
                Object.DestroyImmediate(high);
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void PlayerCameraApplierUsesUrpPostProcessingWithoutAHostPrefab()
        {
            var cameraObject = new GameObject("TestCamera");
            try
            {
                var applier = cameraObject.AddComponent<PlayerCameraAntiAliasingApplier>();
                var cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
                Assert.That(cameraObject.GetComponent<Camera>(), Is.Not.Null);
                Assert.That(cameraData, Is.Not.Null);
                cameraData.renderPostProcessing = true;
                Assert.That(applier.TryApplyPreview(GameAntiAliasingMode.Smaa, out var reason),
                    Is.True, reason);
                Assert.That(applier.CurrentMode, Is.EqualTo(GameAntiAliasingMode.Smaa));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void CorrectQualityPipelineMappingPasses()
        {
            var expected = NewAsset("Low_RPAsset");
            try
            {
                Assert.That(AccessibilityContentValidator.ValidateGraphicsQualityPipelineMapping(
                    "Low", LowPath, expected, expected), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(expected);
            }
        }

        [Test]
        public void MissingQualityPipelineMappingFailsWithExpectedAndActual()
        {
            var expected = NewAsset("Low_RPAsset");
            try
            {
                var issue = AccessibilityContentValidator.ValidateGraphicsQualityPipelineMapping(
                    "Low", LowPath, expected, null);
                Assert.That(issue, Does.Contain("quality level 'Low'"));
                Assert.That(issue, Does.Contain("Low_RPAsset"));
                Assert.That(issue, Does.Contain("actual '<missing>'"));
            }
            finally
            {
                Object.DestroyImmediate(expected);
            }
        }

        [Test]
        public void SwappedQualityPipelineMappingFailsWithExpectedAndActual()
        {
            var expected = NewAsset("Low_RPAsset");
            var actual = NewAsset("Medium_RPAsset");
            try
            {
                var issue = AccessibilityContentValidator.ValidateGraphicsQualityPipelineMapping(
                    "Low", LowPath, expected, actual);
                Assert.That(issue, Does.Contain("Low_RPAsset"));
                Assert.That(issue, Does.Contain("Medium_RPAsset"));
            }
            finally
            {
                Object.DestroyImmediate(expected);
                Object.DestroyImmediate(actual);
            }
        }

        [Test]
        public void WrongQualityPipelineMappingFailsWithExpectedAndActual()
        {
            var expected = NewAsset("High_RPAsset");
            var actual = NewAsset("Unrelated_RPAsset");
            try
            {
                var issue = AccessibilityContentValidator.ValidateGraphicsQualityPipelineMapping(
                    "High", HighPath, expected, actual);
                Assert.That(issue, Does.Contain("High_RPAsset"));
                Assert.That(issue, Does.Contain("Unrelated_RPAsset"));
            }
            finally
            {
                Object.DestroyImmediate(expected);
                Object.DestroyImmediate(actual);
            }
        }

        private static UniversalRenderPipelineAsset NewAsset(string name)
        {
            var asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            asset.name = name;
            return asset;
        }
    }
}
