using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Settings.Display
{
    [DisallowMultipleComponent]
    public sealed class DisplaySettingsRuntimeApplier : MonoBehaviour
    {
        private RuntimeSettingsModel owner;
        private DisplaySettingsState applied;
        private readonly UnityDisplaySettingsDevice device = new UnityDisplaySettingsDevice();

        private void OnEnable()
        {
            owner = HorrorGameContext.Ensure().RuntimeSettings;
            owner.Changed += OnSettingsChanged;
            Apply(owner.Current.DisplaySettings);
        }

        private void OnDisable()
        {
            if (owner != null)
                owner.Changed -= OnSettingsChanged;
            owner = null;
            applied = null;
        }

        private void OnSettingsChanged(RuntimeSettingsState state) => Apply(state.DisplaySettings);

        private void Apply(DisplaySettingsState settings)
        {
            if (settings == null || ReferenceEquals(settings, applied)) return;
            if (device.Matches(settings))
            {
                applied = settings;
                return;
            }
            if (!device.TryApply(settings, out var reason))
            {
                Debug.LogWarning($"Saved display settings could not be applied: {reason}", this);
                return;
            }
            applied = settings;
        }
    }
}
