using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Editor.ContentPipeline;
using Setus.HorrorFramework.Interaction.Interactables;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Setus.HorrorFramework.Tests.EditMode.Authoring
{
    public sealed class ContentPipelineTests
    {
        private const string TestRoot = "Assets/Game/__SetusHorrorFrameworkTests/M10Content";

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AuthoringTestAssetCleanup.Delete(TestRoot);
            StandaloneGameContentTemplate.CreateAt(TestRoot);
        }

        [TearDown]
        public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AuthoringTestAssetCleanup.Delete(TestRoot);
        }

        [Test]
        public void FolderTemplateIsCompleteIdempotentAndPreservesExistingAssetGuid()
        {
            foreach (var relativePath in StandaloneGameContentTemplate.RequiredRelativeFolders)
            {
                Assert.IsTrue(AssetDatabase.IsValidFolder($"{TestRoot}/{relativePath}"), relativePath);
            }

            var assetPath = $"{TestRoot}/Content/Scares/SO_Scare_Existing.asset";
            var definition = ScriptableObject.CreateInstance<ScareDefinition>();
            SetString(definition, "scareId", "test.scare.existing");
            AssetDatabase.CreateAsset(definition, assetPath);
            var originalGuid = AssetDatabase.AssetPathToGUID(assetPath);

            var created = StandaloneGameContentTemplate.CreateAt(TestRoot);

            Assert.AreEqual(0, created.Count);
            Assert.AreEqual(originalGuid, AssetDatabase.AssetPathToGUID(assetPath));
            var reloaded = AssetDatabase.LoadAssetAtPath<ScareDefinition>(assetPath);
            Assert.IsNotNull(reloaded);
            Assert.AreEqual("test.scare.existing", reloaded.ScareId);
        }

        [Test]
        public void AudioCategoryRulesResolveDeterministically()
        {
            AssertRule("Ambience", HorrorAudioCategory.Ambience, AudioClipLoadType.Streaming);
            AssertRule("Music", HorrorAudioCategory.Music, AudioClipLoadType.Streaming);
            AssertRule("OneShots", HorrorAudioCategory.OneShot, AudioClipLoadType.DecompressOnLoad);
            AssertRule("UI", HorrorAudioCategory.Ui, AudioClipLoadType.DecompressOnLoad);
            AssertRule("Voice", HorrorAudioCategory.Voice, AudioClipLoadType.Streaming);
            AssertRule("Stingers", HorrorAudioCategory.Stinger, AudioClipLoadType.CompressedInMemory);
            Assert.IsFalse(HorrorAudioImportRules.TryResolve(
                $"{TestRoot}/Audio/Unknown/AUD_Test.wav",
                out _));
        }

        [Test]
        public void ImportedAmbienceClipReceivesStreamingPolicyAndPassesValidation()
        {
            var audioPath = $"{TestRoot}/Audio/Ambience/AUD_Ambience_Test.wav";
            File.WriteAllBytes(audioPath, CreateSilentWav());
            AssetDatabase.ImportAsset(audioPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(audioPath) as AudioImporter;
            Assert.IsNotNull(importer);
            Assert.IsFalse(importer.forceToMono);
            Assert.IsTrue(importer.loadInBackground);
            Assert.IsFalse(importer.defaultSampleSettings.preloadAudioData);
            Assert.AreEqual(AudioClipLoadType.Streaming, importer.defaultSampleSettings.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, importer.defaultSampleSettings.compressionFormat);
            Assert.AreEqual(0.65f, importer.defaultSampleSettings.quality, 0.001f);

            var result = ContentAssetValidator.Validate(TestRoot);
            Assert.IsFalse(result.Issues.Any(issue => issue.Code.StartsWith("content.audio", StringComparison.Ordinal)));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void PersistentPrefabWithoutAuthoredStableIdFailsValidation()
        {
            var prefabPath = $"{TestRoot}/Prefabs/AI/PF_AI_MissingId.prefab";
            SaveStalkerPrefabWithoutStableId(prefabPath);

            var result = ContentAssetValidator.Validate(TestRoot);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(issue =>
                issue.Code.StartsWith("content.prefab.stable-id", StringComparison.Ordinal) &&
                issue.Message.Contains("PF_AI_MissingId")));
        }

        [Test]
        public void PersistentPrefabWithRequiredComponentsAndStableIdPassesValidation()
        {
            var prefabPath = $"{TestRoot}/Prefabs/Interaction/PF_Interaction_ValidDoor.prefab";
            SaveDoorPrefab(prefabPath, "test.interaction.valid-door");

            var result = ContentAssetValidator.Validate(TestRoot);

            Assert.IsFalse(result.Issues.Any(issue => issue.Code.StartsWith(
                "content.prefab",
                StringComparison.Ordinal)));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void InvalidScareIdFormatFailsContentValidation()
        {
            var assetPath = $"{TestRoot}/Content/Scares/SO_Scare_Invalid.asset";
            var definition = ScriptableObject.CreateInstance<ScareDefinition>();
            SetString(definition, "scareId", "Invalid Scare ID");
            AssetDatabase.CreateAsset(definition, assetPath);

            var result = ContentAssetValidator.Validate(TestRoot);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Issues.Any(issue => issue.Code == "content.scare-id.format"));
        }

        private static void AssertRule(
            string folder,
            HorrorAudioCategory expectedCategory,
            AudioClipLoadType expectedLoadType)
        {
            Assert.IsTrue(HorrorAudioImportRules.TryResolve(
                $"{TestRoot}/Audio/{folder}/AUD_Test.wav",
                out var rule));
            Assert.AreEqual(expectedCategory, rule.Category);
            Assert.AreEqual(expectedLoadType, rule.LoadType);
        }

        private static void SaveDoorPrefab(string path, string stableId)
        {
            var source = new GameObject("Door");
            try
            {
                source.AddComponent<BoxCollider>();
                source.AddComponent<DoorInteractable>();
                source.GetComponent<StableId>().Assign(stableId);

                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(source, path));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void SaveStalkerPrefabWithoutStableId(string path)
        {
            var source = new GameObject("Stalker");
            try
            {
                source.AddComponent<StalkerAiController>();
                Assert.IsNull(source.GetComponent<StableId>());
                Assert.IsNotNull(PrefabUtility.SaveAsPrefabAsset(source, path));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static byte[] CreateSilentWav()
        {
            const int sampleRate = 8000;
            const short channels = 1;
            const short bitsPerSample = 16;
            const int sampleCount = 800;
            var dataSize = sampleCount * channels * (bitsPerSample / 8);
            var bytes = new byte[44 + dataSize];

            WriteAscii(bytes, 0, "RIFF");
            WriteInt32(bytes, 4, 36 + dataSize);
            WriteAscii(bytes, 8, "WAVE");
            WriteAscii(bytes, 12, "fmt ");
            WriteInt32(bytes, 16, 16);
            WriteInt16(bytes, 20, 1);
            WriteInt16(bytes, 22, channels);
            WriteInt32(bytes, 24, sampleRate);
            WriteInt32(bytes, 28, sampleRate * channels * (bitsPerSample / 8));
            WriteInt16(bytes, 32, (short)(channels * (bitsPerSample / 8)));
            WriteInt16(bytes, 34, bitsPerSample);
            WriteAscii(bytes, 36, "data");
            WriteInt32(bytes, 40, dataSize);
            return bytes;
        }

        private static void WriteAscii(byte[] buffer, int offset, string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                buffer[offset + i] = (byte)value[i];
            }
        }

        private static void WriteInt16(byte[] buffer, int offset, short value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, bytes.Length);
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, buffer, offset, bytes.Length);
        }
    }
}
