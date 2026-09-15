using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Setus.HorrorFramework.UI.Menus
{
    [DisallowMultipleComponent]
    public sealed class UiShellEventSystem : MonoBehaviour
    {
        [SerializeField] private string eventSystemName = "SetusUiEventSystem";
        [SerializeField] private bool disableExternalEventSystems = true;

        private EventSystem ownedEventSystem;
        private UiShellLifetime lifetime;

        private void Awake()
        {
            lifetime = GetComponent<UiShellLifetime>();
            if (lifetime == null || !lifetime.IsPrimary)
            {
                enabled = false;
                return;
            }

            EnsureEventSystem();
        }

        private void OnEnable()
        {
            if (lifetime == null || !lifetime.IsPrimary)
            {
                enabled = false;
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureEventSystem();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void EnsureEventSystem()
        {
            if (ownedEventSystem == null)
            {
                ownedEventSystem = GetComponentInChildren<EventSystem>(true);
            }

            if (ownedEventSystem == null)
            {
                if (disableExternalEventSystems)
                {
                    DisableExternalEventSystems();
                }

                var eventSystemObject = new GameObject(eventSystemName);
                eventSystemObject.SetActive(false);
                eventSystemObject.transform.SetParent(transform, false);
                // InputSystemUIInputModule requires EventSystem. Keep the object inactive until both exist.
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
                ownedEventSystem = eventSystemObject.GetComponent<EventSystem>();
                if (ownedEventSystem == null)
                {
                    ownedEventSystem = eventSystemObject.AddComponent<EventSystem>();
                }
            }

            if (!ownedEventSystem.gameObject.activeSelf)
            {
                ownedEventSystem.gameObject.SetActive(true);
            }

            if (disableExternalEventSystems)
            {
                DisableExternalEventSystems();
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureEventSystem();
        }

        private void DisableExternalEventSystems()
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            for (var i = 0; i < eventSystems.Length; i++)
            {
                var eventSystem = eventSystems[i];
                if (eventSystem != null &&
                    eventSystem != ownedEventSystem &&
                    eventSystem.gameObject.activeInHierarchy)
                {
                    eventSystem.gameObject.SetActive(false);
                }
            }
        }
    }
}
