namespace Setus.HorrorFramework.UI.Menus
{
    public readonly struct PauseStateChanged
    {
        public PauseStateChanged(bool isPaused)
        {
            IsPaused = isPaused;
        }

        public bool IsPaused { get; }
    }
}
