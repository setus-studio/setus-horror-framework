using Setus.HorrorFramework.Core.Services;
using Setus.HorrorFramework.Player.Controller;
using Setus.HorrorFramework.Player.State;
using UnityEngine;

namespace Setus.HorrorFramework.SceneFlow.Spawn
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField] private string spawnId = "default";
        [SerializeField] private float cameraPitch;
        [SerializeField] private PlayerControlState controlState = PlayerControlState.Normal;

        public string SpawnId => spawnId;

        public PlayerPose CreatePose()
        {
            return new PlayerPose(transform.position, transform.rotation, cameraPitch, controlState);
        }

        public void ApplyTo(IPlayerPoseProvider target)
        {
            target.RestorePose(CreatePose());
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo();
        }

        private void OnDrawGizmos()
        {
            if (HorrorGameContext.Active == null || !HorrorGameContext.Active.Debug.IsEnabled)
            {
                return;
            }

            DrawGizmo();
        }

        private void DrawGizmo()
        {
            Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.75f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.25f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, transform.forward);
        }
    }
}
