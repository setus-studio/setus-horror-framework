using System;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.Debugging.Logging
{
    public sealed class HorrorDebugManager
    {
        public HorrorDebugManager(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; private set; }
        public event Action<bool> DebugStateChanged;

        public void SetEnabled(bool isEnabled)
        {
            if (IsEnabled == isEnabled)
            {
                return;
            }

            IsEnabled = isEnabled;
            DebugStateChanged?.Invoke(IsEnabled);

            var context = HorrorGameContext.Active;
            context?.Events.Publish(new HorrorDebugStateChanged(IsEnabled));
        }

        public void Toggle()
        {
            SetEnabled(!IsEnabled);
        }
    }

    public readonly struct HorrorDebugStateChanged : IGameplayEvent
    {
        public HorrorDebugStateChanged(bool isEnabled)
        {
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; }
    }
}
