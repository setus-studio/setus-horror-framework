using System;
using System.Collections.Generic;

namespace Setus.HorrorFramework.Editor.Localization
{
    public readonly struct SupportedLocaleDefinition
    {
        public SupportedLocaleDefinition(string code, string displayName, string assetName, string glyphProbe)
        {
            Code = code;
            DisplayName = displayName;
            AssetName = assetName;
            GlyphProbe = glyphProbe;
        }

        public string Code { get; }
        public string DisplayName { get; }
        public string AssetName { get; }
        public string GlyphProbe { get; }
    }

    public static class LocalizationRolloutContract
    {
        public const string LocalizationRoot = "Assets/Game/Localization";
        public const string LocalesPath = LocalizationRoot + "/Locales";
        public const string TablesPath = LocalizationRoot + "/String Tables";
        public const string FontsPath = LocalizationRoot + "/Fonts";
        public const string FontProfilePath = FontsPath + "/GameLocaleFontProfile.asset";

        public static readonly IReadOnlyList<SupportedLocaleDefinition> SupportedLocales =
            new[]
            {
                new SupportedLocaleDefinition("en", "English", "English (en)", "Settings Save Load"),
                new SupportedLocaleDefinition("vi", "Tiếng Việt", "Tiếng Việt (vi)", "Tiếng Việt Độ sáng Âm lượng"),
                new SupportedLocaleDefinition("ja", "日本語", "日本語 (ja)", "日本語 設定 保存 読み込み"),
                new SupportedLocaleDefinition("ko", "한국어", "한국어 (ko)", "한국어 설정 저장 불러오기"),
                new SupportedLocaleDefinition("es", "Español", "Español (es)", "Español Configuración Volumen"),
                new SupportedLocaleDefinition("zh-Hans", "简体中文", "简体中文 (zh-Hans)", "简体中文 设置 保存 加载"),
                new SupportedLocaleDefinition("zh-Hant", "繁體中文", "繁體中文 (zh-Hant)", "繁體中文 設定 儲存 載入")
            };

        public static bool IsTargetLocale(string code)
        {
            foreach (var locale in SupportedLocales)
            {
                if (string.Equals(locale.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
