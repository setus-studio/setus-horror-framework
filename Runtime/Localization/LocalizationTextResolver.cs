using UnityEngine.Localization.Settings;

namespace Setus.HorrorFramework.Localization
{
    public static class LocalizationTextResolver
    {
        public static string Resolve(LocalizedTextReference reference)
        {
            if (!reference.HasKey || !LocalizationSettings.HasSettings)
            {
                return reference.FallbackText;
            }

            var initialization = LocalizationSettings.InitializationOperation;
            if (!initialization.IsDone ||
                initialization.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded ||
                LocalizationSettings.SelectedLocale == null)
            {
                return reference.FallbackText;
            }

            var entry = LocalizationSettings.StringDatabase.GetTableEntry(
                reference.TableCollection,
                reference.EntryKey);
            if (entry.Entry == null)
            {
                return reference.FallbackText;
            }

            var localized = LocalizationSettings.StringDatabase.GetLocalizedString(
                reference.TableCollection,
                reference.EntryKey);
            return string.IsNullOrWhiteSpace(localized) ? reference.FallbackText : localized;
        }
    }
}
