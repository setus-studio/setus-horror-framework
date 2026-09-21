using System;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Settings
{
    public enum GameDisplayMode
    {
        Windowed = 0,
        Borderless = 1,
        Fullscreen = 2
    }

    public enum GameAntiAliasingMode
    {
        Off = 0,
        Fxaa = 1,
        Smaa = 2
    }

    [Serializable]
    public sealed class DisplaySettingsState
    {
        [SerializeField] private bool configured;
        [SerializeField] private GameDisplayMode mode;
        [SerializeField] private int width;
        [SerializeField] private int height;
        [SerializeField] private int refreshNumerator;
        [SerializeField] private int refreshDenominator;
        [SerializeField] private int vSyncCount;
        [SerializeField] private int frameRateCap;
        [SerializeField] private string qualityName;
        [SerializeField] private GameAntiAliasingMode antiAliasing;

        public DisplaySettingsState(GameDisplayMode mode, int width, int height,
            int refreshNumerator, int refreshDenominator, int vSyncCount,
            int frameRateCap, string qualityName,
            GameAntiAliasingMode antiAliasing = GameAntiAliasingMode.Off)
        {
            configured = true;
            this.mode = mode;
            this.width = width;
            this.height = height;
            this.refreshNumerator = refreshNumerator;
            this.refreshDenominator = refreshDenominator;
            this.vSyncCount = vSyncCount;
            this.frameRateCap = frameRateCap;
            this.qualityName = qualityName?.Trim() ?? string.Empty;
            this.antiAliasing = antiAliasing;
        }

        public GameDisplayMode Mode => mode;
        public int Width => width;
        public int Height => height;
        public int RefreshNumerator => refreshNumerator;
        public int RefreshDenominator => refreshDenominator;
        public int VSyncCount => vSyncCount;
        public int FrameRateCap => frameRateCap;
        public string QualityName => qualityName ?? string.Empty;
        public GameAntiAliasingMode AntiAliasing => antiAliasing;

        internal bool HasConfiguration => configured || width != 0 || height != 0 ||
            refreshNumerator != 0 || refreshDenominator != 0 || vSyncCount != 0 ||
            frameRateCap != 0 || !string.IsNullOrEmpty(qualityName);

        public bool TryValidate(out string reason)
        {
            if (!Enum.IsDefined(typeof(GameDisplayMode), mode))
            {
                reason = $"Display mode '{mode}' is unsupported.";
                return false;
            }
            if (width < 320 || height < 240 || width > 16384 || height > 16384)
            {
                reason = "Display resolution must be between 320x240 and 16384x16384.";
                return false;
            }
            if ((refreshNumerator == 0) != (refreshDenominator == 0) ||
                refreshNumerator < 0 || refreshDenominator < 0 ||
                refreshNumerator > 1000000 || refreshDenominator > 1000000)
            {
                reason = "Display refresh rate must be automatic (0/0) or a positive ratio.";
                return false;
            }
            if (frameRateCap != 0 && (frameRateCap < 1 || frameRateCap > 1000))
            {
                reason = "Frame-rate cap must be unlimited (0) or between 1 and 1000.";
                return false;
            }
            if (vSyncCount < 0 || vSyncCount > 4)
            {
                reason = "VSync count must be between 0 and 4.";
                return false;
            }
            if (QualityName.Length > 128)
            {
                reason = "Graphics quality name is too long.";
                return false;
            }
            if (!Enum.IsDefined(typeof(GameAntiAliasingMode), antiAliasing))
            {
                reason = $"Anti-aliasing mode '{antiAliasing}' is unsupported.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
