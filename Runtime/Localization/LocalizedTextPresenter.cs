using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
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

        private AsyncOperationHandle<LocalizationSettings> initialization;
        private bool waitingForInitialization;
        private Coroutine pendingRefresh;

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
            if (LocalizationSettings.HasSettings)
            {
                initialization = LocalizationSettings.InitializationOperation;
                if (!initialization.IsDone)
                {
                    waitingForInitialization = true;
                    initialization.Completed += OnLocalizationInitialized;
                }
            }
        }

        private void OnDisable()
        {
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }

            if (waitingForInitialization)
            {
                initialization.Completed -= OnLocalizationInitialized;
                waitingForInitialization = false;
            }

            if (pendingRefresh != null)
            {
                StopCoroutine(pendingRefresh);
                pendingRefresh = null;
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

        private void OnLocaleChanged(Locale _) => ScheduleRefresh();

        private void OnLocalizationInitialized(AsyncOperationHandle<LocalizationSettings> _)
        {
            waitingForInitialization = false;
            ScheduleRefresh();
        }

        private void ScheduleRefresh()
        {
            if (!Application.isPlaying)
            {
                Refresh();
                return;
            }

            if (pendingRefresh == null)
            {
                pendingRefresh = StartCoroutine(RefreshNextFrame());
            }
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            pendingRefresh = null;
            if (isActiveAndEnabled)
            {
                Refresh();
            }
        }
    }
}
