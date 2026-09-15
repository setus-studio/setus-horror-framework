namespace Setus.HorrorFramework.Debugging.Logging
{
    public interface IHorrorLogger
    {
        bool IsEnabled(HorrorLogCategory category);
        void Log(HorrorLogCategory category, string message);
        void Warning(HorrorLogCategory category, string message);
        void Error(HorrorLogCategory category, string message);
    }
}
