namespace Setus.HorrorFramework.Player.State
{
    public interface IPlayerControlStateTarget
    {
        PlayerControlState CurrentControlState { get; }
        bool SetControlState(PlayerControlState state);
    }

    public interface IPlayerInputLockTarget
    {
        void SetGameplayInputLocked(bool isLocked);
    }
}
