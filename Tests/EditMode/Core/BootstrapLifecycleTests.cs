using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Bootstrap;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Debugging.Logging;
using UnityEngine;

namespace Setus.HorrorFramework.Tests.EditMode.Core
{
    public sealed class BootstrapLifecycleTests
    {
        [SetUp]
        public void SetUp()
        {
            HorrorGameContext.ShutdownActive();
        }

        [TearDown]
        public void TearDown()
        {
            HorrorGameContext.ShutdownActive();
        }

        [Test]
        public void BootstrapperReusesExistingContext()
        {
            var first = new GameObject("First Bootstrapper");
            var second = new GameObject("Second Bootstrapper");
            var config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();

            try
            {
                var firstBootstrapper = first.AddComponent<HorrorGameBootstrapper>();
                var secondBootstrapper = second.AddComponent<HorrorGameBootstrapper>();
                SetConfig(firstBootstrapper, config);
                SetConfig(secondBootstrapper, config);

                var firstContext = firstBootstrapper.Initialize();
                var firstEventBus = firstContext.Services.GetRequired<IGameplayEventBus>();

                var secondContext = secondBootstrapper.Initialize();
                var secondEventBus = secondContext.Services.GetRequired<IGameplayEventBus>();

                Assert.AreSame(firstContext, secondContext);
                Assert.AreSame(firstEventBus, secondEventBus);
                Assert.IsTrue(firstContext.Services.TryGet<HorrorDebugManager>(out _));
                Assert.IsTrue(firstContext.Services.TryGet<IHorrorLogger>(out _));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void BootstrapperFailsWhenFrameworkConfigIsMissing()
        {
            var bootstrapObject = new GameObject("Bootstrapper");
            try
            {
                var bootstrapper = bootstrapObject.AddComponent<HorrorGameBootstrapper>();

                Assert.Throws<System.InvalidOperationException>(() => bootstrapper.Initialize());
                Assert.That(HorrorGameContext.Active, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(bootstrapObject);
            }
        }

        [Test]
        public void DebugTogglePublishesDebugStateChangedEvent()
        {
            var context = HorrorGameContext.Ensure();
            var observed = false;

            context.Events.Subscribe<HorrorDebugStateChanged>(gameplayEvent => observed = gameplayEvent.IsEnabled);
            context.Debug.Toggle();

            Assert.IsTrue(context.Debug.IsEnabled);
            Assert.IsTrue(observed);
        }

        private static void SetConfig(HorrorGameBootstrapper bootstrapper, HorrorFrameworkConfig config)
        {
            typeof(HorrorGameBootstrapper)
                .GetField("config", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(bootstrapper, config);
        }
    }
}
