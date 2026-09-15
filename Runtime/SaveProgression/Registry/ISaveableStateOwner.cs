using Setus.HorrorFramework.Core.Services;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public interface ISaveableStateOwner : IRuntimeStateOwner
    {
        string StableId { get; }
        bool HasActiveState { get; }
        SaveRestorePolicy ActiveRestorePolicy { get; }
    }

    public interface ISceneScopedSaveableStateOwner
    {
        string SceneId { get; }
    }
}
