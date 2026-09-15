using System;
using Setus.HorrorFramework.Player.State;
using UnityEngine;

namespace Setus.HorrorFramework.Player.Controller
{
    [Serializable]
    public struct PlayerPose
    {
        [SerializeField] private Vector3 position;
        [SerializeField] private Quaternion rotation;
        [SerializeField] private float cameraPitch;
        [SerializeField] private PlayerControlState controlState;

        public PlayerPose(Vector3 position, Quaternion rotation, float cameraPitch, PlayerControlState controlState)
        {
            this.position = position;
            this.rotation = rotation;
            this.cameraPitch = cameraPitch;
            this.controlState = controlState;
        }

        public Vector3 Position => position;
        public Quaternion Rotation => rotation;
        public float CameraPitch => cameraPitch;
        public PlayerControlState ControlState => controlState;
    }
}
