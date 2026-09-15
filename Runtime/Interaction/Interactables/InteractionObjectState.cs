using System;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Interactables
{
    [Serializable]
    public sealed class InteractionObjectState
    {
        public InteractionObjectState(
            bool isOpen = false,
            bool isLocked = false,
            bool isConsumed = false,
            bool isInspected = false,
            bool isEnabled = true)
        {
            this.isOpen = isOpen;
            this.isLocked = isLocked;
            this.isConsumed = isConsumed;
            this.isInspected = isInspected;
            this.isEnabled = isEnabled;
        }

        [SerializeField] private bool isOpen;
        [SerializeField] private bool isLocked;
        [SerializeField] private bool isConsumed;
        [SerializeField] private bool isInspected;
        [SerializeField] private bool isEnabled = true;

        public bool IsOpen => isOpen;
        public bool IsLocked => isLocked;
        public bool IsConsumed => isConsumed;
        public bool IsInspected => isInspected;
        public bool IsEnabled => isEnabled;

        public InteractionObjectState WithEnabled(bool enabled)
        {
            return new InteractionObjectState(isOpen, isLocked, isConsumed, isInspected, enabled);
        }
    }
}
