using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.UI.Settings;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Setus.HorrorFramework.Accessibility.Display
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class BrightnessVolumeSettingsApplier : MonoBehaviour
    {
        [SerializeField] private Volume volume;
        [SerializeField] private float minimumPostExposure = -2f;
        [SerializeField] private float maximumPostExposure = 2f;

        private RuntimeSettingsModel settings;
        private ColorAdjustments colorAdjustments;

        private void Awake()
        {
            if (volume == null)
            {
                volume = GetComponent<Volume>();
            }

            var runtimeProfile = volume.profile;
            if (runtimeProfile == null)
            {
                runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile = runtimeProfile;
            }

            if (!runtimeProfile.TryGet(out colorAdjustments))
            {
                colorAdjustments = runtimeProfile.Add<ColorAdjustments>(true);
            }

            colorAdjustments.postExposure.overrideState = true;
        }

        private void OnEnable()
        {
            settings = HorrorGameContext.Ensure().RuntimeSettings;
            settings.Changed += Apply;
            Apply(settings.Current);
        }

        private void OnDisable()
        {
            if (settings != null)
            {
                settings.Changed -= Apply;
                settings = null;
            }
        }

        private void Apply(RuntimeSettingsState state)
        {
            if (colorAdjustments != null)
            {
                colorAdjustments.postExposure.value = Mathf.Lerp(
                    minimumPostExposure,
                    maximumPostExposure,
                    state.Brightness);
            }
        }
    }
}
