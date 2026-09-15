using System;
using System.Collections.Generic;
using System.Linq;

namespace Setus.HorrorFramework.SaveProgression.StableIds
{
    public static class StableIdValidator
    {
        public static StableIdValidationResult Validate(IEnumerable<IStableIdProvider> providers)
        {
            return Validate(providers, null);
        }

        public static StableIdValidationResult Validate(IEnumerable<IStableIdProvider> providers, StableIdManifest manifest)
        {
            return Validate(providers, manifest, null);
        }

        public static StableIdValidationResult Validate(
            IEnumerable<IStableIdProvider> providers,
            StableIdManifest manifest,
            string sceneId)
        {
            if (providers == null)
            {
                throw new ArgumentNullException(nameof(providers));
            }

            var issues = new List<StableIdValidationIssue>();
            var providersById = new Dictionary<string, List<IStableIdProvider>>();

            foreach (var provider in providers)
            {
                if (provider == null)
                {
                    continue;
                }

                var stableId = provider.StableId;
                if (string.IsNullOrWhiteSpace(stableId))
                {
                    issues.Add(new StableIdValidationIssue(
                        StableIdValidationIssueType.MissingId,
                        stableId,
                        new[] { provider }));
                    continue;
                }

                if (!providersById.TryGetValue(stableId, out var matches))
                {
                    matches = new List<IStableIdProvider>();
                    providersById.Add(stableId, matches);
                }

                matches.Add(provider);
            }

            issues.AddRange(
                providersById
                    .Where(pair => pair.Value.Count > 1)
                    .Select(pair => new StableIdValidationIssue(
                        StableIdValidationIssueType.DuplicateId,
                        pair.Key,
                        pair.Value)));

            if (manifest != null)
            {
                ValidateManifestContract(manifest, providersById, issues, sceneId);
            }

            return new StableIdValidationResult(issues);
        }

        private static void ValidateManifestContract(
            StableIdManifest manifest,
            IReadOnlyDictionary<string, List<IStableIdProvider>> providersById,
            ICollection<StableIdValidationIssue> issues,
            string sceneId)
        {
            foreach (var entry in manifest.Entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.StableId) ||
                    !entry.AppliesToScene(sceneId))
                {
                    continue;
                }

                var present = providersById.TryGetValue(entry.StableId, out var providers);
                switch (entry.Kind)
                {
                    case StableIdManifestEntryKind.Required:
                        if (!present)
                        {
                            issues.Add(new StableIdValidationIssue(
                                StableIdValidationIssueType.MissingRequiredId,
                                entry.StableId,
                                Array.Empty<IStableIdProvider>()));
                        }
                        break;
                    case StableIdManifestEntryKind.Optional:
                        if (!present)
                        {
                            issues.Add(new StableIdValidationIssue(
                                StableIdValidationIssueType.MissingOptionalId,
                                entry.StableId,
                                Array.Empty<IStableIdProvider>()));
                        }
                        break;
                    case StableIdManifestEntryKind.Retired:
                        if (present)
                        {
                            issues.Add(new StableIdValidationIssue(
                                StableIdValidationIssueType.RetiredIdPresent,
                                entry.StableId,
                                providers));
                        }
                        break;
                }
            }
        }
    }
}
