using UnityEngine;

namespace Setus.HorrorFramework.UI.Menus
{
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class UiShellLifetime : MonoBehaviour
    {
        private static UiShellLifetime activeInstance;
        private bool isPrimary;

        public bool IsPrimary => isPrimary && activeInstance == this;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeInstance = null;
        }

        private void Awake()
        {
            if (activeInstance != null && activeInstance != this)
            {
                // Disable immediately so sibling components cannot run their own OnEnable lifecycle.
                gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                return;
            }

            activeInstance = this;
            isPrimary = true;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (activeInstance == this)
            {
                activeInstance = null;
            }

            isPrimary = false;
        }
    }
}
