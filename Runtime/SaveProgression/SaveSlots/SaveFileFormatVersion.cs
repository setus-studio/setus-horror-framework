namespace Setus.HorrorFramework.SaveProgression.SaveSlots
{
    public static class SaveFileFormatVersion
    {
        public const int LegacyClrTypeIdentity = 1;
        public const int Current = 2;

        public static bool IsSupported(int version)
        {
            return version == LegacyClrTypeIdentity || version == Current;
        }
    }
}
