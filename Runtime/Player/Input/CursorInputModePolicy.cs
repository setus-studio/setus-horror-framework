using Setus.HorrorFramework.Player.State;
using UnityEngine;

namespace Setus.HorrorFramework.Player.Input
{
    public readonly struct CursorInputModePolicy
    {
        public CursorInputModePolicy(
            CursorLockMode cursorLockMode,
            bool cursorVisible,
            PlayerControlState playerControlState)
        {
            CursorLockMode = cursorLockMode;
            CursorVisible = cursorVisible;
            PlayerControlState = playerControlState;
        }

        public CursorLockMode CursorLockMode { get; }
        public bool CursorVisible { get; }
        public PlayerControlState PlayerControlState { get; }

        public static CursorInputModePolicy For(CursorInputMode mode)
        {
            switch (mode)
            {
                case CursorInputMode.Gameplay:
                    return new CursorInputModePolicy(
                        CursorLockMode.Locked,
                        false,
                        PlayerControlState.Normal);
                case CursorInputMode.Menu:
                    return new CursorInputModePolicy(
                        CursorLockMode.None,
                        true,
                        PlayerControlState.Menu);
                case CursorInputMode.Loading:
                    return new CursorInputModePolicy(
                        CursorLockMode.None,
                        false,
                        PlayerControlState.Disabled);
                case CursorInputMode.Cutscene:
                    return new CursorInputModePolicy(
                        CursorLockMode.Locked,
                        false,
                        PlayerControlState.Cutscene);
                case CursorInputMode.Disabled:
                default:
                    return new CursorInputModePolicy(
                        CursorLockMode.None,
                        true,
                        PlayerControlState.Disabled);
            }
        }
    }
}
