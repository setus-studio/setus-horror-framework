using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Editor.ContentPipeline
{
    public enum HorrorAudioCategory
    {
        Ambience = 0,
        Music = 1,
        OneShot = 2,
        Ui = 3,
        Voice = 4,
        Stinger = 5
    }

    public sealed class HorrorAudioImportRule
    {
        public HorrorAudioImportRule(
            HorrorAudioCategory category,
            string folderName,
            AudioClipLoadType loadType,
            AudioCompressionFormat compressionFormat,
            float quality,
            bool forceToMono,
            bool loadInBackground,
            bool preloadAudioData)
        {
            Category = category;
            FolderName = folderName;
            LoadType = loadType;
            CompressionFormat = compressionFormat;
            Quality = quality;
            ForceToMono = forceToMono;
            LoadInBackground = loadInBackground;
            PreloadAudioData = preloadAudioData;
        }

        public HorrorAudioCategory Category { get; }
        public string FolderName { get; }
        public AudioClipLoadType LoadType { get; }
        public AudioCompressionFormat CompressionFormat { get; }
        public float Quality { get; }
        public bool ForceToMono { get; }
        public bool LoadInBackground { get; }
        public bool PreloadAudioData { get; }

        public void Apply(AudioImporter importer)
        {
            if (importer == null)
            {
                throw new ArgumentNullException(nameof(importer));
            }

            importer.forceToMono = ForceToMono;
            importer.loadInBackground = LoadInBackground;

            var settings = importer.defaultSampleSettings;
            settings.loadType = LoadType;
            settings.compressionFormat = CompressionFormat;
            settings.quality = Quality;
            settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            settings.preloadAudioData = PreloadAudioData;
            importer.defaultSampleSettings = settings;
        }
    }

    public static class HorrorAudioImportRules
    {
        private static readonly HorrorAudioImportRule[] Rules =
        {
            new HorrorAudioImportRule(
                HorrorAudioCategory.Ambience,
                "Ambience",
                AudioClipLoadType.Streaming,
                AudioCompressionFormat.Vorbis,
                0.65f,
                false,
                true,
                false),
            new HorrorAudioImportRule(
                HorrorAudioCategory.Music,
                "Music",
                AudioClipLoadType.Streaming,
                AudioCompressionFormat.Vorbis,
                0.7f,
                false,
                true,
                false),
            new HorrorAudioImportRule(
                HorrorAudioCategory.OneShot,
                "OneShots",
                AudioClipLoadType.DecompressOnLoad,
                AudioCompressionFormat.ADPCM,
                1f,
                true,
                false,
                true),
            new HorrorAudioImportRule(
                HorrorAudioCategory.Ui,
                "UI",
                AudioClipLoadType.DecompressOnLoad,
                AudioCompressionFormat.PCM,
                1f,
                false,
                false,
                true),
            new HorrorAudioImportRule(
                HorrorAudioCategory.Voice,
                "Voice",
                AudioClipLoadType.Streaming,
                AudioCompressionFormat.Vorbis,
                0.8f,
                true,
                true,
                false),
            new HorrorAudioImportRule(
                HorrorAudioCategory.Stinger,
                "Stingers",
                AudioClipLoadType.CompressedInMemory,
                AudioCompressionFormat.Vorbis,
                0.75f,
                false,
                false,
                true)
        };

        public static IReadOnlyList<HorrorAudioImportRule> All => Rules;

        public static bool TryResolve(string assetPath, out HorrorAudioImportRule rule)
        {
            rule = null;
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            if (!normalized.StartsWith("Assets/Game/", StringComparison.Ordinal))
            {
                return false;
            }

            var segments = normalized.Split('/');
            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!string.Equals(segments[i], "Audio", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                for (var ruleIndex = 0; ruleIndex < Rules.Length; ruleIndex++)
                {
                    if (string.Equals(
                            segments[i + 1],
                            Rules[ruleIndex].FolderName,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        rule = Rules[ruleIndex];
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        public static bool IsUnderGameAudioRoot(string assetPath)
        {
            var normalized = (assetPath ?? string.Empty).Replace('\\', '/');
            return normalized.StartsWith("Assets/Game/", StringComparison.Ordinal) &&
                   normalized.IndexOf("/Audio/", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
