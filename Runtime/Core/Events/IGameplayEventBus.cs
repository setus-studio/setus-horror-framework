using System;

namespace Setus.HorrorFramework.Core.Events
{
    public interface IGameplayEventBus
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);
        void Publish<TEvent>(TEvent gameplayEvent);
        void Clear();
    }
}
