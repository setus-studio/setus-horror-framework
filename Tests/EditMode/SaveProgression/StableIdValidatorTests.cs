using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;

namespace Setus.HorrorFramework.Tests.EditMode.SaveProgression
{
    public sealed class StableIdValidatorTests
    {
        [Test]
        public void ValidateReportsDuplicateStableIds()
        {
            var first = new GameObject("First Saveable");
            var second = new GameObject("Second Saveable");

            try
            {
                var firstId = first.AddComponent<StableId>();
                var secondId = second.AddComponent<StableId>();
                firstId.Assign("door.front");
                secondId.Assign("door.front");

                var result = StableIdValidator.Validate(new[] { firstId, secondId });

                Assert.IsTrue(result.HasErrors);
                Assert.AreEqual("door.front", result.Duplicates.Single().StableId);
                Assert.AreEqual(2, result.Duplicates.Single().Providers.Count);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ValidateReportsMissingStableId()
        {
            var gameObject = new GameObject("Missing Saveable");

            try
            {
                var stableId = gameObject.AddComponent<StableId>();
                stableId.Assign(string.Empty);

                var result = StableIdValidator.Validate(new[] { stableId });

                Assert.IsTrue(result.HasErrors);
                Assert.AreEqual(StableIdValidationIssueType.MissingId, result.MissingIds.Single().IssueType);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ValidateWithManifestSeparatesMissingRequiredAndOptionalIds()
        {
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();
            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry("door.required", StableIdManifestEntryKind.Required),
                new StableIdManifestEntry("note.optional", StableIdManifestEntryKind.Optional)
            });

            var result = StableIdValidator.Validate(System.Array.Empty<IStableIdProvider>(), manifest);

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.HasWarnings);
            Assert.AreEqual("door.required", result.MissingRequiredIds.Single().StableId);
            Assert.AreEqual("note.optional", result.MissingOptionalIds.Single().StableId);
        }

        [Test]
        public void ValidateWithManifestOnlyRequiresEntriesApplicableToCurrentScene()
        {
            var manifest = ScriptableObject.CreateInstance<StableIdManifest>();
            manifest.ReplaceEntries(new[]
            {
                new StableIdManifestEntry(
                    "door.scene-a",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneA"),
                new StableIdManifestEntry(
                    "door.scene-b",
                    StableIdManifestEntryKind.Required,
                    sceneId: "SceneB")
            });

            try
            {
                var result = StableIdValidator.Validate(
                    System.Array.Empty<IStableIdProvider>(),
                    manifest,
                    "SceneB");

                Assert.That(result.MissingRequiredIds.Count, Is.EqualTo(1));
                Assert.That(result.MissingRequiredIds.Single().StableId, Is.EqualTo("door.scene-b"));
            }
            finally
            {
                Object.DestroyImmediate(manifest);
            }
        }
    }
}
