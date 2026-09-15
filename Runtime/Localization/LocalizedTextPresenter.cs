using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Setus.HorrorFramework.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedTextPresenter : MonoBehaviour
    {
        [SerializeField] private Text target;
        [SerializeField] private string tableCollection = LocalizedTextReference.DefaultTable;
        [SerializeField] private string entryKey;
        [SerializeField, TextArea] private string fallbackText;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<Text>();
            }
        }

        private void OnEnable()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
        }

        public void Configure(Text label, string key, string fallback, string table = LocalizedTextReference.DefaultTable)
        {
            target = label;
            entryKey = key;
            fallbackText = fallback;
            tableCollection = table;
            Refresh();
        }

        public void Refresh()
        {
            if (target != null)
            {
                target.text = LocalizationTextResolver.Resolve(
                    new LocalizedTextReference(entryKey, fallbackText, tableCollection));
            }
        }

        private void OnLocaleChanged(Locale _) => Refresh();
    }
}
