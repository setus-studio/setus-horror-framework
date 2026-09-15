using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Setus.HorrorFramework.AI.Memory;
using Setus.HorrorFramework.AI.Navigation;
using Setus.HorrorFramework.AI.Tuning;
using Setus.HorrorFramework.Core.Config;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.SaveProgression.StableIds;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Setus.HorrorFramework.Tests.PlayMode.AI
{
    public sealed class StalkerAiSaveAdapterPlayModeTests
    {
        private GameObject stalkerObject;
        private StalkerAiTuningProfile profile;
        private HorrorFrameworkConfig config;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (stalkerObject != null)
            {
                Object.Destroy(stalkerObject);
                stalkerObject = null;
            }

            if (profile != null)
            {
                Object.Destroy(profile);
                profile = null;
            }

            yield return null;
            HorrorGameContext.ShutdownActive();
            if (config != null)
            {
                Object.Destroy(config);
                config = null;
            }
        }

        [UnityTest]
        public IEnumerator InitializedControllerRegistersThroughUnityLifecycleAndRestores()
        {
            HorrorGameContext.ShutdownActive();
            config = ScriptableObject.CreateInstance<HorrorFrameworkConfig>();
            var context = HorrorGameContext.Initialize(config);
            profile = ScriptableObject.CreateInstance<StalkerAiTuningProfile>();

            stalkerObject = new GameObject("M8 PlayMode Save Stalker");
            stalkerObject.SetActive(false);
            var agent = stalkerObject.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            var stableId = stalkerObject.AddComponent<StableId>();
            stableId.Assign("m8.test.playmode-save-stalker");
            var controller = stalkerObject.AddComponent<StalkerAiController>();
            var adapter = stalkerObject.AddComponent<StalkerAiSaveAdapter>();
            SetField(controller, "tuningProfile", profile);
            SetField(controller, "stableId", stableId);
            SetField(controller, "agent", agent);
            SetField(adapter, "stalker", controller);
            SetField(adapter, "stableId", stableId);

            stalkerObject.SetActive(true);
            yield return null;

            var captured = (StalkerAiRuntimeState)adapter.CaptureState();
            adapter.RestoreState(captured);

            Assert.That(controller.IsInitialized, Is.True);
            Assert.That(context.Saveables.StableOwners.Count, Is.EqualTo(1));
            Assert.That(adapter.ValidateRestoreState(captured).IsValid, Is.True);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field: {fieldName}");
            field.SetValue(target, value);
        }
    }
}
