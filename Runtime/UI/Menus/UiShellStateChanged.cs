namespace Setus.HorrorFramework.UI.Menus
{
    public readonly struct UiShellStateChanged
    {
        public UiShellStateChanged(UiShellState previousState, UiShellState currentState)
        {
            PreviousState = previousState;
            CurrentState = currentState;
        }

        public UiShellState PreviousState { get; }
        public UiShellState CurrentState { get; }
    }
}
