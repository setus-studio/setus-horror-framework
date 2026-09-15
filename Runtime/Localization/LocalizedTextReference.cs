using System;
using UnityEngine;

namespace Setus.HorrorFramework.Localization
{
    [Serializable]
    public struct LocalizedTextReference
    {
        public const string DefaultTable = "Game Text";

        public LocalizedTextReference(string entryKey, string fallbackText, string tableCollection = DefaultTable)
        {
            this.tableCollection = string.IsNullOrWhiteSpace(tableCollection) ? DefaultTable : tableCollection;
            this.entryKey = entryKey ?? string.Empty;
            this.fallbackText = fallbackText ?? string.Empty;
        }

        [SerializeField] private string tableCollection;
        [SerializeField] private string entryKey;
        [SerializeField, TextArea] private string fallbackText;

        public string TableCollection => string.IsNullOrWhiteSpace(tableCollection) ? DefaultTable : tableCollection;
        public string EntryKey => entryKey ?? string.Empty;
        public string FallbackText => fallbackText ?? string.Empty;
        public bool HasKey => !string.IsNullOrWhiteSpace(EntryKey);
    }
}
