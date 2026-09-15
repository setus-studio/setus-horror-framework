using System;
using Setus.HorrorFramework.Atmosphere.Scares;
using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Camera;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Camera
{
    [DisallowMultipleComponent]
    public sealed class ScareCameraReactionCoordinator : MonoBehaviour
    {
        [SerializeField] private string scareId;
        [SerializeField] private UnityEngine.Camera targetCamera;
        [SerializeField, Min(0f)] private float shakeDistance = 0.035f;
        [SerializeField, Min(0f)] private float fovKick = 3f;
        [SerializeField, Min(0.1f)] private float shakeFrequency = 18f;

        private HorrorGameContext context;
        private IDisposable subscription;
        private FirstPersonCameraRig cameraRig;
        private bool isPlaying;

        private void Awake()
        {
            context = HorrorGameContext.Ensure();
            ResolveCameraRig();
        }

        private void OnEnable()
        {
            context ??= HorrorGameContext.Ensure();
            ResolveCameraRig();
            subscription?.Dispose();
            subscription = context.Events.Subscribe<ScareStateChanged>(OnScareStateChanged);
        }

        private void OnDisable()
        {
            subscription?.Dispose();
            subscription = null;
            ResetCamera();
        }

        private void Update()
        {
            if (!isPlaying || cameraRig == null)
            {
                return;
            }

            var intensity = context?.RuntimeSettings.Current.CameraShakeIntensity ?? 1f;
            var time = Time.unscaledTime * shakeFrequency;
            var offset = new Vector3(Mathf.Sin(time), Mathf.Cos(time * 0.73f), 0f) *
                shakeDistance * intensity;
            cameraRig.SetReactionOffset(offset, fovKick * intensity);
        }

        private void OnScareStateChanged(ScareStateChanged changed)
        {
            if (!string.Equals(changed.ScareId, scareId, StringComparison.Ordinal))
            {
                return;
            }

            isPlaying = changed.State == ScareLifecycleState.Playing && !changed.Restored;
            if (!isPlaying)
            {
                ResetCamera();
            }
        }

        private void ResetCamera()
        {
            isPlaying = false;
            if (cameraRig != null)
            {
                cameraRig.SetReactionOffset(Vector3.zero, 0f);
            }
        }

        private void ResolveCameraRig()
        {
            cameraRig = targetCamera != null
                ? targetCamera.GetComponentInParent<FirstPersonCameraRig>(true)
                : null;
        }
    }
}
