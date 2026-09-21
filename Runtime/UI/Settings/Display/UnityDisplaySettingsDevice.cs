using System;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Settings.Display
{
    public sealed class UnityDisplaySettingsDevice : IDisplaySettingsDevice
    {
        private readonly GameAntiAliasingMode fallbackAntiAliasing;
        private PlayerCameraAntiAliasingApplier camera;

        public UnityDisplaySettingsDevice(GameAntiAliasingMode fallbackAntiAliasing = GameAntiAliasingMode.Off)
        {
            this.fallbackAntiAliasing = fallbackAntiAliasing;
        }

        public DisplaySettingsState CaptureCurrent()
        {
            var refresh = Screen.currentResolution.refreshRateRatio;
            var mode = FromUnityMode(Screen.fullScreenMode);
            var names = QualitySettings.names;
            var quality = QualitySettings.GetQualityLevel();
            return new DisplaySettingsState(
                mode, Screen.width, Screen.height,
                mode == GameDisplayMode.Fullscreen ? (int)refresh.numerator : 0,
                mode == GameDisplayMode.Fullscreen ? (int)refresh.denominator : 0,
                QualitySettings.vSyncCount,
                Application.targetFrameRate < 0 ? 0 : Application.targetFrameRate,
                quality >= 0 && quality < names.Length ? names[quality] : string.Empty,
                FindPlayerCamera()?.CurrentMode ?? fallbackAntiAliasing);
        }

        public bool TryApply(DisplaySettingsState settings, out string reason)
        {
            if (settings == null)
            {
                reason = "Display settings are missing.";
                return false;
            }
            if (!settings.TryValidate(out reason))
            {
                return false;
            }

            if (settings.Mode == GameDisplayMode.Fullscreen)
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer)
                {
                    reason = "Exclusive Fullscreen can only be previewed in a Windows player build.";
                    return false;
                }
                if (!HasExclusiveResolution(settings))
                {
                    reason = "The selected fullscreen resolution or refresh rate is not supported by this display.";
                    return false;
                }
            }

            var qualityIndex = ResolveQualityIndex(settings.QualityName);
            if (qualityIndex < 0)
            {
                reason = $"Graphics quality '{settings.QualityName}' is not available in this game.";
                return false;
            }
            var camera = FindPlayerCamera();
            if (camera != null && settings.AntiAliasing != GameAntiAliasingMode.Off &&
                !camera.SupportsPostProcessAntiAliasing)
            {
                reason = "Player camera needs URP post-processing enabled for anti-aliasing.";
                return false;
            }

            try
            {
                var mode = ToUnityMode(settings.Mode);
                if (settings.RefreshNumerator == 0)
                    Screen.SetResolution(settings.Width, settings.Height, mode);
                else
                    Screen.SetResolution(settings.Width, settings.Height, mode, new RefreshRate
                    {
                        numerator = (uint)settings.RefreshNumerator,
                        denominator = (uint)settings.RefreshDenominator
                    });
                QualitySettings.SetQualityLevel(qualityIndex, true);
                QualitySettings.vSyncCount = settings.VSyncCount;
                Application.targetFrameRate = settings.FrameRateCap == 0 ? -1 : settings.FrameRateCap;
                if (camera != null && !camera.TryApplyPreview(settings.AntiAliasing, out reason))
                    return false;
                reason = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                reason = $"Display settings could not be applied: {exception.Message}";
                return false;
            }
        }

        public bool Matches(DisplaySettingsState settings)
        {
            if (settings == null || FromUnityMode(Screen.fullScreenMode) != settings.Mode ||
                QualitySettings.vSyncCount != settings.VSyncCount ||
                (Application.targetFrameRate < 0 ? 0 : Application.targetFrameRate) != settings.FrameRateCap)
                return false;

            if (settings.Mode != GameDisplayMode.Borderless &&
                (Screen.width != settings.Width || Screen.height != settings.Height))
                return false;

            if (settings.Mode == GameDisplayMode.Fullscreen && settings.RefreshNumerator > 0)
            {
                var rate = Screen.currentResolution.refreshRateRatio;
                if ((long)rate.numerator * settings.RefreshDenominator !=
                    (long)settings.RefreshNumerator * rate.denominator)
                    return false;
            }

            if (!string.IsNullOrEmpty(settings.QualityName))
            {
                var names = QualitySettings.names;
                var quality = QualitySettings.GetQualityLevel();
                if (quality < 0 || quality >= names.Length || quality != ResolveQualityIndex(settings.QualityName))
                    return false;
            }

            var camera = FindPlayerCamera();
            if (camera != null && camera.CurrentMode != settings.AntiAliasing)
                return false;

            return true;
        }

        internal static int ResolveQualityIndex(string qualityName)
        {
            if (string.IsNullOrEmpty(qualityName)) return QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            var index = Array.IndexOf(names, qualityName);
            // The bundled Standalone sample used PC before Low/Medium/High were authored.
            return index < 0 && qualityName == "PC" ? Array.IndexOf(names, "High") : index;
        }

        private PlayerCameraAntiAliasingApplier FindPlayerCamera()
        {
            if (camera == null || !camera.isActiveAndEnabled)
                camera = UnityEngine.Object.FindFirstObjectByType<PlayerCameraAntiAliasingApplier>();
            return camera;
        }

        private static bool HasExclusiveResolution(DisplaySettingsState settings)
        {
            foreach (var resolution in Screen.resolutions)
            {
                if (resolution.width != settings.Width || resolution.height != settings.Height)
                    continue;
                if (settings.RefreshNumerator == 0)
                    return true;
                var rate = resolution.refreshRateRatio;
                if ((long)rate.numerator * settings.RefreshDenominator ==
                    (long)settings.RefreshNumerator * rate.denominator)
                    return true;
            }
            return false;
        }

        private static GameDisplayMode FromUnityMode(FullScreenMode mode) => mode switch
        {
            FullScreenMode.ExclusiveFullScreen => GameDisplayMode.Fullscreen,
            FullScreenMode.FullScreenWindow => GameDisplayMode.Borderless,
            FullScreenMode.MaximizedWindow => GameDisplayMode.Borderless,
            _ => GameDisplayMode.Windowed
        };

        private static FullScreenMode ToUnityMode(GameDisplayMode mode) => mode switch
        {
            GameDisplayMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
            GameDisplayMode.Borderless => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed
        };
    }
}
