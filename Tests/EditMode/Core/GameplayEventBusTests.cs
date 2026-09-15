using NUnit.Framework;
using Setus.HorrorFramework.Core.Events;

namespace Setus.HorrorFramework.Tests.EditMode.Core
{
    public sealed class GameplayEventBusTests
    {
        [Test]
        public void PublishInvokesSubscribedHandler()
        {
            var bus = new GameplayEventBus();
            var received = 0;

            bus.Subscribe<TestGameplayEvent>(gameplayEvent => received = gameplayEvent.Value);
            bus.Publish(new TestGameplayEvent(7));

            Assert.AreEqual(7, received);
        }

        [Test]
        public void DisposedSubscriptionStopsReceivingEvents()
        {
            var bus = new GameplayEventBus();
            var receivedCount = 0;

            var subscription = bus.Subscribe<TestGameplayEvent>(_ => receivedCount++);
            bus.Publish(new TestGameplayEvent(1));
            subscription.Dispose();
            bus.Publish(new TestGameplayEvent(2));

            Assert.AreEqual(1, receivedCount);
        }

        private readonly struct TestGameplayEvent : IGameplayEvent
        {
            public TestGameplayEvent(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }
    }
}
