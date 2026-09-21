using Setus.HorrorFramework.Core.Services;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Setus.HorrorFramework.UI.Settings.Display
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera), typeof(UniversalAdditionalCameraData))]
    public sealed class PlayerCameraAntiAliasingApplier : MonoBehaviour
    {
        private RuntimeSettingsModel owner;
        private UniversalAdditionalCameraData cameraData;

        public GameAntiAliasingMode CurrentMode => cameraData == null ? GameAntiAliasingMode.Off :
            cameraData.antialiasing switch
            {
                AntialiasingMode.FastApproximateAntialiasing => GameAntiAliasingMode.Fxaa,
                AntialiasingMode.SubpixelMorphologicalAntiAliasing => GameAntiAliasingMode.Smaa,
                _ => GameAntiAliasingMode.Off
            };
        public bool SupportsPostProcessAntiAliasing => cameraData != null && cameraData.renderPostProcessing;

        private void OnEnable()
        {
            cameraData = GetComponent<UniversalAdditionalCameraData>();
            if (!Application.isPlaying) return;
            if (cameraData == null)
            {
                Debug.LogError("Player camera anti-aliasing requires UniversalAdditionalCameraData.", this);
                enabled = false;
                return;
            }

            owner = HorrorGameContext.Ensure().RuntimeSettings;
            owner.Changed += OnSettingsChanged;
            Apply(owner.Current.DisplaySettings?.AntiAliasing ?? GameAntiAliasingMode.Off);
        }

        private void OnDisable()
        {
            if (owner != null) owner.Changed -= OnSettingsChanged;
            owner = null;
        }

        private void OnSettingsChanged(RuntimeSettingsState state)
        {
            Apply(state.DisplaySettings?.AntiAliasing ?? GameAntiAliasingMode.Off);
        }

        public bool TryApplyPreview(GameAntiAliasingMode mode, out string reason)
        {
            if (cameraData == null) cameraData = GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null ||
                (mode != GameAntiAliasingMode.Off && !cameraData.renderPostProcessing))
            {
                reason = "Player camera needs URP post-processing enabled for anti-aliasing.";
                return false;
            }

            Apply(mode);
            reason = string.Empty;
            return true;
        }

        private void Apply(GameAntiAliasingMode mode)
        {
            cameraData.antialiasing = mode switch
            {
                GameAntiAliasingMode.Fxaa => AntialiasingMode.FastApproximateAntialiasing,
                GameAntiAliasingMode.Smaa => AntialiasingMode.SubpixelMorphologicalAntiAliasing,
                _ => AntialiasingMode.None
            };
        }
    }
}
