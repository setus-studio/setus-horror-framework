namespace Setus.HorrorFramework.UI.Settings.Display
{
    public interface IDisplaySettingsDevice
    {
        DisplaySettingsState CaptureCurrent();
        bool TryApply(DisplaySettingsState settings, out string reason);
        bool Matches(DisplaySettingsState settings);
    }
}
