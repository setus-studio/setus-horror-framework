using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Menus;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Prompt
{
    [DisallowMultipleComponent]
    public sealed class CrosshairPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private bool visibleDuringGameplay = true;

        private HorrorGameContext context;
        private IDisposable uiSubscription;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
            if (root == null)
            {
                root = gameObject;
            }
        }

        private void OnEnable()
        {
            uiSubscription = context.Events.Subscribe<UiShellStateChanged>(_ => Apply());
            Apply();
        }

        private void OnDisable()
        {
            uiSubscription?.Dispose();
            uiSubscription = null;
        }

        public void Configure(GameObject rootObject)
        {
            root = rootObject;
        }

        private void Apply()
        {
            var shouldShow = visibleDuringGameplay && context.UiShell.Current.CurrentScreen == UiShellScreen.Hidden;
            if (root != null && root.activeSelf != shouldShow)
            {
                root.SetActive(shouldShow);
            }
        }
    }
}
