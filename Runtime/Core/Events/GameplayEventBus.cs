using System;
using System.Collections.Generic;

namespace Setus.HorrorFramework.Core.Events
{
    public sealed class GameplayEventBus : IGameplayEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> handlersByEventType = new Dictionary<Type, List<Delegate>>();

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var eventType = typeof(TEvent);
            if (!handlersByEventType.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                handlersByEventType.Add(eventType, handlers);
            }

            handlers.Add(handler);
            return new Subscription(() => Unsubscribe(eventType, handler));
        }

        public void Publish<TEvent>(TEvent gameplayEvent)
        {
            if (!handlersByEventType.TryGetValue(typeof(TEvent), out var handlers) || handlers.Count == 0)
            {
                return;
            }

            var snapshot = handlers.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                ((Action<TEvent>)snapshot[i]).Invoke(gameplayEvent);
            }
        }

        public void Clear()
        {
            handlersByEventType.Clear();
        }

        private void Unsubscribe(Type eventType, Delegate handler)
        {
            if (!handlersByEventType.TryGetValue(eventType, out var handlers))
            {
                return;
            }

            handlers.Remove(handler);
            if (handlers.Count == 0)
            {
                handlersByEventType.Remove(eventType);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;

            public Subscription(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                var callback = dispose;
                dispose = null;
                callback?.Invoke();
            }
        }
    }
}
