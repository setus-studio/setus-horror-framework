using System;
using UnityEngine;

namespace Setus.HorrorFramework.UI.Menus
{
    [Serializable]
    public sealed class PauseState
    {
        public PauseState(bool isPaused = false)
        {
            IsPaused = isPaused;
        }

        [SerializeField] private bool isPaused;

        public bool IsPaused
        {
            get => isPaused;
            private set => isPaused = value;
        }
    }
}
