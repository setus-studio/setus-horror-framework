using System;
using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.Player.Controller
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonPlayerController))]
    public sealed class PlayerPoseSaveAdapter : MonoBehaviour
    {
        [SerializeField] private FirstPersonPlayerController playerController;

        private IDisposable registration;

        private void Awake()
        {
            if (playerController == null)
            {
                playerController = GetComponent<FirstPersonPlayerController>();
            }
        }

        private void OnEnable()
        {
            if (playerController == null)
            {
                return;
            }

            registration = HorrorGameContext.Ensure().Saveables.RegisterGlobal(playerController);
        }

        private void OnDisable()
        {
            registration?.Dispose();
            registration = null;
        }
    }
}
