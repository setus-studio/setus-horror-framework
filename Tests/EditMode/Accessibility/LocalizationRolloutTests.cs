using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Setus.HorrorFramework.Editor.Localization;
using Setus.HorrorFramework.Editor.Validators;
using Setus.HorrorFramework.Localization;
using UnityEditor;
using UnityEngine;

namespace Setus.HorrorFramework.Tests.EditMode.Accessibility
{
    public sealed class LocalizationRolloutTests
    {
        [Test]
        public void SupportedLocaleContractContainsSevenUniqueReleaseLocales()
        {
            var codes = LocalizationRolloutContract.SupportedLocales
                .Select(locale => locale.Code)
                .ToArray();

            Assert.That(codes, Is.EquivalentTo(new[]
            {
                "en", "vi", "ja", "ko", "es", "zh-Hans", "zh-Hant"
            }));
            Assert.That(codes.Distinct().Count(), Is.EqualTo(codes.Length));
            Assert.That(LocalizationRolloutContract.SupportedLocales.All(locale =>
                !string.IsNullOrWhiteSpace(locale.DisplayName) &&
                !string.IsNullOrWhiteSpace(locale.GlyphProbe)), Is.True);
        }

        [Test]
        public void LocaleFontProfileRequiresExplicitConfiguration()
        {
            var profile = ScriptableObject.CreateInstance<LocaleFontProfile>();
            try
            {
                Assert.That(profile.IsLocaleConfigured("ja"), Is.False);

                ConfigureAuthoredFont(profile, "ja");

                Assert.That(profile.IsLocaleConfigured("ja"), Is.True);
                Assert.That(profile.TryGetFontOverride("ja", out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void TranslationSeedProvidesLocalizedCriticalTextAndSafeUnknownFallback()
        {
            Assert.That(
                FrameworkTranslationSeed.Get("vi", "ui.settings.brightness", "Brightness"),
                Is.EqualTo("Độ sáng"));
            Assert.That(
                FrameworkTranslationSeed.Get("zh-Hans", "ui.common.settings", "Settings"),
                Is.EqualTo("设置"));
            Assert.That(
                FrameworkTranslationSeed.Get("zh-Hant", "ui.common.settings", "Settings"),
                Is.EqualTo("設定"));
            Assert.That(
                FrameworkTranslationSeed.Get("ja", "game.authored.unknown", "Authored fallback"),
                Is.EqualTo("Authored fallback"));
        }

        [Test]
        public void RuntimeLocaleControllerKeepsEnglishBackwardCompatibilityButGatesOtherLocales()
        {
            var owner = new GameObject("LocaleController");
            owner.SetActive(false);
            var profile = ScriptableObject.CreateInstance<LocaleFontProfile>();
            try
            {
                var controller = owner.AddComponent<RuntimeLocaleController>();
                Assert.That(controller.IsLocaleReady("en"), Is.True);
                Assert.That(controller.IsLocaleReady("vi"), Is.False);

                ConfigureAuthoredFont(profile, "vi");
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("fontProfile").objectReferenceValue = profile;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(controller.IsLocaleReady("vi"), Is.True);
                Assert.That(controller.IsLocaleReady("ko"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FullValidLocalizationCorpusPassesGlyphValidation()
        {
            var corpus = new[]
            {
                Entry("ui.settings.title", "Cài đặt"),
                Entry("ui.settings.language", "Ngôn ngữ")
            };
            var characters = new HashSet<char>(corpus.SelectMany(entry => entry.Value));

            var issues = LocalizationRolloutValidator.ValidateGlyphCorpus(
                "vi",
                "Game Text_vi",
                "Assets/Game/Localization/String Tables/Game Text_vi.asset",
                corpus,
                new[]
                {
                    new LocalizationRolloutValidator.GlyphCoverageSource(
                        "CompleteVietnameseFont",
                        characters.Contains)
                });

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void CanonicalFullLocalizationCorpusPassesValidation()
        {
            var issues = LocalizationRolloutValidator.ValidateProject();

            Assert.That(issues, Is.Empty, string.Join("\n", issues));
        }

        [Test]
        public void MissingVietnameseGlyphFailsWithLocaleTableKeyAndGlyph()
        {
            var issues = LocalizationRolloutValidator.ValidateGlyphCorpus(
                "vi",
                "Game Text_vi",
                "Assets/Game/Localization/String Tables/Game Text_vi.asset",
                new[] { Entry("ui.settings.confirm", "Xác nhận hệ thống") },
                new[]
                {
                    new LocalizationRolloutValidator.GlyphCoverageSource(
                        "IncompleteVietnameseFont",
                        character => character != 'ệ')
                });

            Assert.That(issues.Any(issue => issue.Contains("locale 'vi'")), Is.True);
            Assert.That(issues.Any(issue => issue.Contains("table 'Game Text_vi'")), Is.True);
            Assert.That(issues.Any(issue => issue.Contains("key 'ui.settings.confirm'")), Is.True);
            Assert.That(issues.Any(issue => issue.Contains("glyph 'ệ' (U+1EC7)")), Is.True);
        }

        [Test]
        public void MissingCjkGlyphFailsWithExactEntryDiagnostic()
        {
            var issues = LocalizationRolloutValidator.ValidateGlyphCorpus(
                "ja",
                "Game Text_ja",
                "Assets/Game/Localization/String Tables/Game Text_ja.asset",
                new[] { Entry("ui.settings.world", "世界設定") },
                new[]
                {
                    new LocalizationRolloutValidator.GlyphCoverageSource(
                        "IncompleteJapaneseFont",
                        character => character != '界')
                });

            Assert.That(issues.Any(issue => issue.Contains("locale 'ja'")), Is.True);
            Assert.That(issues.Any(issue => issue.Contains("key 'ui.settings.world'")), Is.True);
            Assert.That(issues.Any(issue => issue.Contains("glyph '界' (U+754C)")), Is.True);
        }

        [Test]
        public void FallbackFontGlyphSatisfiesCorpusValidation()
        {
            var issues = LocalizationRolloutValidator.ValidateGlyphCorpus(
                "zh-Hans",
                "Game Text_zh-Hans",
                "Assets/Game/Localization/String Tables/Game Text_zh-Hans.asset",
                new[] { Entry("ui.settings.title", "设置") },
                new[]
                {
                    new LocalizationRolloutValidator.GlyphCoverageSource(
                        "PrimaryLatinFont",
                        character => character < 128),
                    new LocalizationRolloutValidator.GlyphCoverageSource(
                        "ChineseFallbackFont",
                        character => character == '设' || character == '置')
                });

            Assert.That(issues, Is.Empty);
        }

        private static KeyValuePair<string, string> Entry(string key, string value) =>
            new KeyValuePair<string, string>(key, value);

        private static void ConfigureAuthoredFont(LocaleFontProfile profile, string localeCode)
        {
            var serialized = new SerializedObject(profile);
            var bindings = serialized.FindProperty("bindings");
            bindings.arraySize = 1;
            var binding = bindings.GetArrayElementAtIndex(0);
            binding.FindPropertyRelative("localeCode").stringValue = localeCode;
            binding.FindPropertyRelative("useAuthoredFont").boolValue = true;
            binding.FindPropertyRelative("fontOverride").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
