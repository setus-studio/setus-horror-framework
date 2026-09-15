using System;

namespace Setus.HorrorFramework.Debugging.Logging
{
    [Flags]
    public enum HorrorLogCategory
    {
        None = 0,
        Core = 1 << 0,
        SaveProgression = 1 << 1,
        Interaction = 1 << 2,
        Narrative = 1 << 3,
        AI = 1 << 4,
        Atmosphere = 1 << 5,
        UI = 1 << 6,
        Validation = 1 << 7,
        Debug = 1 << 8,
        Player = 1 << 9,
        SceneFlow = 1 << 10,
        All = ~0
    }
}
