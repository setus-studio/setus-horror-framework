using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Objectives;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Menus;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Objectives
{
    [DisallowMultipleComponent]
    public sealed class ObjectivePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;

        private HorrorGameContext context;
        private IDisposable subscription;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            if (context == null)
            {
                context = HorrorGameContext.Ensure();
            }

            subscription?.Dispose();
            subscription = context.Events.Subscribe<ObjectiveStateChanged>(_ => Refresh());
            context.UiShell.Changed += OnUiShellChanged;
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
            Refresh();
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            if (context != null)
            {
                context.UiShell.Changed -= OnUiShellChanged;
            }
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
        }

        private void Refresh()
        {
            if (context?.Objectives == null ||
                context.UiShell.Current.CurrentScreen != UiShellScreen.Hidden ||
                !context.Objectives.TryGetActiveDefinition(out var objective))
            {
                SetVisible(false);
                return;
            }

            if (titleText != null)
            {
                titleText.text = LocalizationTextResolver.Resolve(objective.LocalizedTitle);
            }

            if (descriptionText != null)
            {
                descriptionText.text = LocalizationTextResolver.Resolve(objective.LocalizedDescription);
            }

            SetVisible(true);
        }

        private void OnUiShellChanged(UiShellStateChanged _)
        {
            Refresh();
        }

        private void OnLocaleChanged(Locale _) => Refresh();

        private void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
            {
                root.SetActive(visible);
            }
        }
    }
}
