using System;
using System.Collections.Generic;
using UnityEngine;

namespace Setus.HorrorFramework.Localization
{
    [CreateAssetMenu(
        fileName = "LocaleFontProfile",
        menuName = "Setus/Horror Framework/Localization/Locale Font Profile")]
    public sealed class LocaleFontProfile : ScriptableObject
    {
        [Serializable]
        private struct LocaleFontBinding
        {
            [SerializeField] private string localeCode;
            [SerializeField] private bool useAuthoredFont;
            [SerializeField] private Font fontOverride;

            public string LocaleCode => localeCode;
            public bool UseAuthoredFont => useAuthoredFont;
            public Font FontOverride => fontOverride;
        }

        [SerializeField] private List<LocaleFontBinding> bindings = new List<LocaleFontBinding>();

        public bool IsLocaleConfigured(string localeCode)
        {
            if (!TryGetBinding(localeCode, out var binding))
            {
                return false;
            }

            return binding.UseAuthoredFont || binding.FontOverride != null;
        }

        public bool TryGetFontOverride(string localeCode, out Font font)
        {
            font = null;
            if (!TryGetBinding(localeCode, out var binding) || binding.UseAuthoredFont)
            {
                return false;
            }

            font = binding.FontOverride;
            return font != null;
        }

        public bool TryGetConfiguration(string localeCode, out bool useAuthoredFont, out Font fontOverride)
        {
            if (TryGetBinding(localeCode, out var binding))
            {
                useAuthoredFont = binding.UseAuthoredFont;
                fontOverride = binding.FontOverride;
                return true;
            }

            useAuthoredFont = false;
            fontOverride = null;
            return false;
        }

        private bool TryGetBinding(string localeCode, out LocaleFontBinding binding)
        {
            if (!string.IsNullOrWhiteSpace(localeCode))
            {
                for (var i = 0; i < bindings.Count; i++)
                {
                    if (string.Equals(
                            bindings[i].LocaleCode,
                            localeCode,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        binding = bindings[i];
                        return true;
                    }
                }
            }

            binding = default;
            return false;
        }
    }
}
