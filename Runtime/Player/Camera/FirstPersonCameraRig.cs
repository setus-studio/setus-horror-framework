using System;
using UnityEngine;

namespace Setus.HorrorFramework.Player.Camera
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraRig : MonoBehaviour, IPlayerCameraRig
    {
        [SerializeField] private UnityEngine.Camera playerCamera;
        [SerializeField] private Transform pitchRoot;

        private Vector3 baseLocalPosition;
        private Vector3 postureOffset;
        private Vector3 headBobOffset;
        private Vector3 reactionPositionOffset;
        private float baseFieldOfView;
        private float reactionFieldOfViewOffset;

        public UnityEngine.Camera Camera => playerCamera;
        public Transform CameraTransform => pitchRoot != null ? pitchRoot : transform;
        public float Pitch { get; private set; }

        private void Awake()
        {
            ResolveReferences();
            baseLocalPosition = CameraTransform.localPosition;
            baseFieldOfView = playerCamera != null ? playerCamera.fieldOfView : 0f;
            SetPitch(Pitch);
            ApplyCameraComposition();
        }

        public void SetPitch(float pitch)
        {
            Pitch = pitch;
            CameraTransform.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
        }

        public void SetHeadBobOffset(Vector3 offset)
        {
            headBobOffset = offset;
            ApplyCameraComposition();
        }

        public void SetPostureOffset(Vector3 offset)
        {
            postureOffset = offset;
            ApplyCameraComposition();
        }

        public void SetReactionOffset(Vector3 positionOffset, float fieldOfViewOffset)
        {
            reactionPositionOffset = positionOffset;
            reactionFieldOfViewOffset = fieldOfViewOffset;
            ApplyCameraComposition();
        }

        public void ResetTransientOffsets()
        {
            headBobOffset = Vector3.zero;
            ApplyCameraComposition();
        }

        internal Action CaptureRollbackAction()
        {
            var rollbackPitch = Pitch;
            var rollbackBaseLocalPosition = baseLocalPosition;
            var rollbackPostureOffset = postureOffset;
            var rollbackHeadBobOffset = headBobOffset;
            var rollbackReactionPositionOffset = reactionPositionOffset;
            var rollbackBaseFieldOfView = baseFieldOfView;
            var rollbackReactionFieldOfViewOffset = reactionFieldOfViewOffset;
            var rollbackLocalPosition = CameraTransform.localPosition;
            var rollbackLocalRotation = CameraTransform.localRotation;
            var rollbackFieldOfView = playerCamera != null ? playerCamera.fieldOfView : 0f;

            return () =>
            {
                Pitch = rollbackPitch;
                baseLocalPosition = rollbackBaseLocalPosition;
                postureOffset = rollbackPostureOffset;
                headBobOffset = rollbackHeadBobOffset;
                reactionPositionOffset = rollbackReactionPositionOffset;
                baseFieldOfView = rollbackBaseFieldOfView;
                reactionFieldOfViewOffset = rollbackReactionFieldOfViewOffset;
                CameraTransform.localPosition = rollbackLocalPosition;
                CameraTransform.localRotation = rollbackLocalRotation;
                if (playerCamera != null)
                {
                    playerCamera.fieldOfView = rollbackFieldOfView;
                }
            };
        }

        private void ApplyCameraComposition()
        {
            CameraTransform.localPosition =
                baseLocalPosition + postureOffset + headBobOffset + reactionPositionOffset;
            if (playerCamera != null)
            {
                playerCamera.fieldOfView = baseFieldOfView + reactionFieldOfViewOffset;
            }
        }

        private void ResolveReferences()
        {
            if (pitchRoot == null)
            {
                pitchRoot = transform;
            }

            if (playerCamera == null)
            {
                playerCamera = GetComponentInChildren<UnityEngine.Camera>(true);
            }
        }
    }
}
