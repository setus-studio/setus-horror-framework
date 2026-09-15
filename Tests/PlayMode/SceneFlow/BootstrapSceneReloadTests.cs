using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.Core.Bootstrap;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Events;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Setus.HorrorFramework.Tests.PlayMode.SceneFlow
{
    public sealed class BootstrapSceneReloadTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();
        private readonly List<Scene> createdScenes = new List<Scene>();
        private HorrorFrameworkConfig testConfig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (var i = 0; i < createdObjects.Count; i++)
            {
                if (createdObjects[i] != null)
                {
                    Object.Destroy(createdObjects[i]);
                }
            }

            createdObjects.Clear();
            yield return null;

            HorrorGameContext.ShutdownActive();
            if (testConfig != null)
            {
                Object.Destroy(testConfig);
                testConfig = null;
            }

            RestoreExternalActiveScene();

            for (var i = createdScenes.Count - 1; i >= 0; i--)
            {
                var scene = createdScenes[i];
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            createdScenes.Clear();
        }

        [UnityTest]
        public IEnumerator BootstrapContextSurvivesSceneReloadWithoutDuplicatingServices()
        {
            HorrorGameContext.ShutdownActive();
            testConfig = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();

            var firstScene = CreateAndActivateScene("SetusM1BootstrapInitial");
            var firstBootstrapper = CreateBootstrapper("First Bootstrapper");
            yield return null;

            var firstContext = HorrorGameContext.Active;
            Assert.NotNull(firstContext);
            Assert.AreSame(firstContext, firstBootstrapper.Context);

            var firstEventBus = firstContext.Services.GetRequired<IGameplayEventBus>();
            var firstServiceCount = firstContext.Services.Count;

            CreateAndActivateScene("SetusM1BootstrapReloaded");
            var unload = SceneManager.UnloadSceneAsync(firstScene);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }

            var secondBootstrapper = CreateBootstrapper("Second Bootstrapper");
            yield return null;

            var secondContext = HorrorGameContext.Active;
            Assert.AreSame(firstContext, secondContext);
            Assert.AreSame(firstContext, secondBootstrapper.Context);
            Assert.AreSame(firstEventBus, secondContext.Services.GetRequired<IGameplayEventBus>());
            Assert.AreEqual(firstServiceCount, secondContext.Services.Count);
        }

        private Scene CreateAndActivateScene(string sceneName)
        {
            var scene = SceneManager.CreateScene(sceneName);
            createdScenes.Add(scene);
            Assert.IsTrue(SceneManager.SetActiveScene(scene));
            return scene;
        }

        private HorrorGameBootstrapper CreateBootstrapper(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            createdObjects.Add(gameObject);
            var bootstrapper = gameObject.AddComponent<HorrorGameBootstrapper>();
            var configField = typeof(HorrorGameBootstrapper).GetField(
                "config",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(configField);
            configField.SetValue(bootstrapper, testConfig);
            gameObject.SetActive(true);
            return bootstrapper;
        }

        private void RestoreExternalActiveScene()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && !createdScenes.Contains(scene))
                {
                    SceneManager.SetActiveScene(scene);
                    return;
                }
            }
        }
    }
}
