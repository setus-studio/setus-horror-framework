using System;
using UnityEngine;

namespace Setus.HorrorFramework.Interaction.Triggers
{
    [Serializable]
    public sealed class InteractionTriggerState
    {
        public InteractionTriggerState(bool hasFired = false)
        {
            this.hasFired = hasFired;
        }

        [SerializeField] private bool hasFired;

        public bool HasFired => hasFired;
    }
}
