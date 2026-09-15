using System;
using System.Collections.Generic;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    [DisallowMultipleComponent]
    public sealed class ScareScenarioAdapter : MonoBehaviour
    {
        [SerializeField] private ScareDefinition[] scares = Array.Empty<ScareDefinition>();
        private IDisposable registration;

        public IReadOnlyList<ScareDefinition> Definitions => scares ?? Array.Empty<ScareDefinition>();

        private void OnEnable()
        {
            registration?.Dispose();
            registration = HorrorGameContext.Ensure().Scares.RegisterDefinitions(scares);
        }

        private void OnDisable()
        {
            registration?.Dispose();
            registration = null;
        }

        private void OnValidate()
        {
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < (scares?.Length ?? 0); i++)
            {
                if (scares[i] == null || string.IsNullOrWhiteSpace(scares[i].ScareId) || !ids.Add(scares[i].ScareId))
                {
                    Debug.LogError("Scare scenario requires non-empty, unique ScareId values.", this);
                    return;
                }
            }
        }
    }
}
