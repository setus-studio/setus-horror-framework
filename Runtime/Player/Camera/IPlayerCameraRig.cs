using UnityEngine;

namespace Setus.HorrorFramework.Player.Camera
{
    public interface IPlayerCameraRig
    {
        UnityEngine.Camera Camera { get; }
        Transform CameraTransform { get; }
        float Pitch { get; }
        void SetPitch(float pitch);
        void SetHeadBobOffset(Vector3 offset);
        void ResetTransientOffsets();
    }
}
