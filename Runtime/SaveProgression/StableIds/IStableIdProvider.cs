using UnityEngine;

namespace Setus.HorrorFramework.SaveProgression.StableIds
{
    public interface IStableIdProvider
    {
        string StableId { get; }
        UnityEngine.Object Owner { get; }
    }
}
