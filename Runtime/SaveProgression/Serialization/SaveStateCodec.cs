using System;
using System.IO;
using UnityEngine;

namespace Setus.HorrorFramework.SaveProgression.Serialization
{
    public sealed class SaveStateCodec<TState> : ISaveStateCodec
    {
        private readonly Func<TState, string> serialize;
        private readonly Func<string, TState> deserialize;

        public SaveStateCodec(
            string typeKey,
            Func<TState, string> serialize,
            Func<string, TState> deserialize)
        {
            if (string.IsNullOrWhiteSpace(typeKey))
            {
                throw new ArgumentException("Stable state type key must not be empty.", nameof(typeKey));
            }

            TypeKey = typeKey;
            this.serialize = serialize ?? throw new ArgumentNullException(nameof(serialize));
            this.deserialize = deserialize ?? throw new ArgumentNullException(nameof(deserialize));
        }

        public string TypeKey { get; }
        public Type StateType => typeof(TState);

        public string Serialize(object state)
        {
            if (state == null || !(state is TState typedState))
            {
                throw new InvalidDataException(
                    $"State for codec '{TypeKey}' must be {typeof(TState).FullName}.");
            }

            if (state is UnityEngine.Object)
            {
                throw new InvalidDataException("Unity object references cannot be persisted as save state.");
            }

            return serialize(typedState);
        }

        public object Deserialize(string payload)
        {
            var state = deserialize(payload);
            if (ReferenceEquals(state, null))
            {
                throw new InvalidDataException($"Codec '{TypeKey}' produced a null state.");
            }

            return state;
        }

        public static SaveStateCodec<TState> CreateJson(string typeKey)
        {
            return new SaveStateCodec<TState>(
                typeKey,
                state => JsonUtility.ToJson(state),
                payload =>
                {
                    if (string.IsNullOrWhiteSpace(payload))
                    {
                        throw new InvalidDataException($"JSON payload for '{typeKey}' was empty.");
                    }

                    return JsonUtility.FromJson<TState>(payload);
                });
        }
    }
}
