using Setus.HorrorFramework.Debugging.Logging;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Setus.HorrorFramework.Core.Config
{
    [CreateAssetMenu(
        fileName = "HorrorFrameworkConfig",
        menuName = "Setus/Horror Framework/Framework Config")]
    public sealed class HorrorFrameworkConfig : ScriptableObject
    {
        [SerializeField] private bool debugEnabledByDefault;
        [SerializeField] private bool debugHotkeyEnabled = true;
        [SerializeField] private Key debugToggleKey = Key.Backquote;
        [SerializeField] private HorrorLogCategory enabledLogCategories =
            HorrorLogCategory.Core |
            HorrorLogCategory.SaveProgression |
            HorrorLogCategory.Validation |
            HorrorLogCategory.Debug;

        public bool DebugEnabledByDefault => debugEnabledByDefault;
        public bool DebugHotkeyEnabled => debugHotkeyEnabled;
        public Key DebugToggleKey => debugToggleKey;
        public HorrorLogCategory EnabledLogCategories => enabledLogCategories;
    }
}
