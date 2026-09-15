using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.ContentPipeline
{
    public static class StandaloneGameContentTemplate
    {
        public const string DefaultGameRoot = "Assets/Game";

        private static readonly string[] FolderPaths =
        {
            "Art/Animations",
            "Art/Materials",
            "Art/Models",
            "Art/Textures",
            "Audio/Ambience",
            "Audio/Music",
            "Audio/OneShots",
            "Audio/Stingers",
            "Audio/UI",
            "Audio/Voice",
            "Content/AIProfiles",
            "Content/Inventory",
            "Content/Messages",
            "Content/Notes",
            "Content/Objectives",
            "Content/Scares",
            "Content/Scenarios",
            "Prefabs/AI",
            "Prefabs/Environment",
            "Prefabs/Interaction",
            "Prefabs/Player",
            "Prefabs/UI",
            "Scenes/Debug",
            "Settings",
            "UI"
        };

        public static IReadOnlyList<string> RequiredRelativeFolders => FolderPaths;

        [MenuItem("Setus/Horror Framework/Content/Create or Repair Standalone Game Folders")]
        public static void CreateDefaultTemplateFromMenu()
        {
            var created = CreateAt(DefaultGameRoot);
            Debug.Log(
                created.Count == 0
                    ? $"Standalone game folder template is already complete at '{DefaultGameRoot}'."
                    : $"Created {created.Count} standalone game folder(s) under '{DefaultGameRoot}'.");
        }

        public static IReadOnlyList<string> CreateAt(string gameRoot)
        {
            gameRoot = NormalizeAndValidateRoot(gameRoot);
            var created = new List<string>();
            EnsureFolder(gameRoot, created);

            for (var i = 0; i < FolderPaths.Length; i++)
            {
                EnsureFolder($"{gameRoot}/{FolderPaths[i]}", created);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return created;
        }

        public static string NormalizeAndValidateRoot(string gameRoot)
        {
            var normalized = (gameRoot ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (!string.Equals(normalized, DefaultGameRoot, StringComparison.Ordinal) &&
                !normalized.StartsWith(DefaultGameRoot + "/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Standalone game content roots must be located at or below Assets/Game.",
                    nameof(gameRoot));
            }

            return normalized;
        }

        private static void EnsureFolder(string path, ICollection<string> created)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var slash = path.LastIndexOf('/');
            var parent = slash > 0 ? path.Substring(0, slash) : "Assets";
            var folderName = slash > 0 ? path.Substring(slash + 1) : path;
            EnsureFolder(parent, created);
            AssetDatabase.CreateFolder(parent, folderName);
            created.Add(path);
        }
    }
}
