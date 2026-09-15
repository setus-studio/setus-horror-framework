using System;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Menus
{
    [Serializable]
    public sealed class UiShellState
    {
        public UiShellState(
            UiShellScreen currentScreen = UiShellScreen.Hidden,
            bool isPaused = false)
        {
            CurrentScreen = currentScreen;
            IsPaused = isPaused;
        }

        [SerializeField] private UiShellScreen currentScreen;
        [SerializeField] private bool isPaused;

        public UiShellScreen CurrentScreen
        {
            get => currentScreen;
            private set => currentScreen = value;
        }

        public bool IsPaused
        {
            get => isPaused;
            private set => isPaused = value;
        }

    }
}
