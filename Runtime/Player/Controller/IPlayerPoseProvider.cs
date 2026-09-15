using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.Player.Controller
{
    public interface IPlayerPoseProvider : IRuntimeStateOwner<PlayerPose>
    {
        PlayerPose CurrentPose { get; }
        void RestorePose(PlayerPose pose);
    }
}
