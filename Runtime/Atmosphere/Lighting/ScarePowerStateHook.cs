using System;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Lighting
{
    [DisallowMultipleComponent]
    public sealed class ScarePowerStateHook : MonoBehaviour
    {
        [SerializeField] private string powerStateId;
        [SerializeField] private Light[] affectedLights = Array.Empty<Light>();

        private HorrorGameContext context;
        private IDisposable subscription;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
        }

        private void OnEnable()
        {
            context ??= HorrorGameContext.Ensure();
            subscription?.Dispose();
            subscription = context.Events.Subscribe<ScarePowerStateChanged>(OnPowerStateChanged);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            SetPowered(true);
        }

        private void OnPowerStateChanged(ScarePowerStateChanged changed)
        {
            if (!string.Equals(changed.PowerStateId, powerStateId, StringComparison.Ordinal))
            {
                return;
            }

            SetPowered(changed.IsPowered);
        }

        private void SetPowered(bool isPowered)
        {
            for (var i = 0; i < affectedLights.Length; i++)
            {
                if (affectedLights[i] != null)
                {
                    affectedLights[i].enabled = isPowered;
                }
            }
        }
    }
}
