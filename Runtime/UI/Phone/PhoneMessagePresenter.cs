using System;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Narrative.Scenario;
using Setus.HorrorFramework.Localization;
using Setus.HorrorFramework.UI.Menus;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Setus.HorrorFramework.UI.Phone
{
    [DisallowMultipleComponent]
    public sealed class PhoneMessagePresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text senderText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text statusText;

        private HorrorGameContext context;
        private IDisposable deliveredSubscription;
        private IDisposable readSubscription;
        private IDisposable repliedSubscription;
        private IDisposable stateSubscription;

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

            DisposeSubscriptions();
            deliveredSubscription = context.Events.Subscribe<NarrativePhoneMessageDelivered>(OnDelivered);
            readSubscription = context.Events.Subscribe<NarrativePhoneMessageRead>(OnRead);
            repliedSubscription = context.Events.Subscribe<NarrativePhoneMessageReplied>(OnReplied);
            stateSubscription = context.Events.Subscribe<NarrativeStateChanged>(_ => Refresh());
            context.UiShell.Changed += OnUiShellChanged;
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
            Refresh();
        }

        private void OnDisable()
        {
            DisposeSubscriptions();
            if (context != null)
            {
                context.UiShell.Changed -= OnUiShellChanged;
            }
            if (LocalizationSettings.HasSettings)
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
        }

        private void OnDelivered(NarrativePhoneMessageDelivered delivered)
        {
            Refresh();
        }

        private void OnRead(NarrativePhoneMessageRead read)
        {
            Refresh();
        }

        private void OnReplied(NarrativePhoneMessageReplied replied)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (context?.Narrative == null || context.UiShell.Current.CurrentScreen != UiShellScreen.Hidden)
            {
                SetVisible(false);
                return;
            }

            if (!context.Narrative.TryGetLatestDeliveredPhoneMessageId(out var messageId) ||
                !context.Narrative.TryGetPhoneProgress(messageId, out var progress) ||
                !context.Narrative.TryGetPhoneDefinition(messageId, out var definition))
            {
                SetVisible(false);
                return;
            }

            if (senderText != null)
            {
                senderText.text = LocalizationTextResolver.Resolve(definition.LocalizedSender);
            }

            if (messageText != null)
            {
                messageText.text = LocalizationTextResolver.Resolve(definition.LocalizedMessage);
            }

            if (statusText != null)
            {
                var status = progress.Replied
                    ? new LocalizedTextReference(FrameworkTextKeys.PhoneStatusReplied, "Replied")
                    : progress.Read
                        ? new LocalizedTextReference(FrameworkTextKeys.PhoneStatusRead, "Read")
                        : new LocalizedTextReference(FrameworkTextKeys.PhoneStatusNew, "New message");
                statusText.text = LocalizationTextResolver.Resolve(status);
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

        private void DisposeSubscriptions()
        {
            deliveredSubscription?.Dispose();
            readSubscription?.Dispose();
            repliedSubscription?.Dispose();
            stateSubscription?.Dispose();
            deliveredSubscription = null;
            readSubscription = null;
            repliedSubscription = null;
            stateSubscription = null;
        }
    }
}
