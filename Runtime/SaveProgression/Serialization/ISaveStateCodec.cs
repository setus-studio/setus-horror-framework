using System;

namespace Setus.HorrorFramework.SaveProgression.Serialization
{
    public interface ISaveStateCodec
    {
        string TypeKey { get; }
        Type StateType { get; }
        string Serialize(object state);
        object Deserialize(string payload);
    }
}
