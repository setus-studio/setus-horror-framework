using System;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Settings
{
    [Serializable]
    internal sealed class RuntimeSettingsStateV1
    {
        [SerializeField] private float masterVolume;
        [SerializeField] private float mouseSensitivity;
        [SerializeField] private bool subtitlesEnabled;

        public float MasterVolume => masterVolume;
        public float MouseSensitivity => mouseSensitivity;
        public bool SubtitlesEnabled => subtitlesEnabled;
    }
}
