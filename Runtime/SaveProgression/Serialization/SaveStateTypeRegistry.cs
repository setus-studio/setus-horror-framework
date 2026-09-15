using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Setus.HorrorFramework.SaveProgression.Serialization
{
    public sealed class SaveStateTypeRegistry
    {
        private readonly Dictionary<string, ISaveStateCodec> codecsByKey =
            new Dictionary<string, ISaveStateCodec>(StringComparer.Ordinal);

        private readonly Dictionary<Type, ISaveStateCodec> codecsByType =
            new Dictionary<Type, ISaveStateCodec>();

        private readonly Dictionary<string, ISaveStateCodec> codecsByLegacyIdentity =
            new Dictionary<string, ISaveStateCodec>(StringComparer.Ordinal);

        public IReadOnlyList<string> RegisteredTypeKeys =>
            codecsByKey.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        public void Register(ISaveStateCodec codec, params string[] legacyTypeIdentities)
        {
            if (codec == null)
            {
                throw new ArgumentNullException(nameof(codec));
            }

            if (string.IsNullOrWhiteSpace(codec.TypeKey))
            {
                throw new ArgumentException("Codec stable type key must not be empty.", nameof(codec));
            }

            if (codec.StateType == null)
            {
                throw new ArgumentException("Codec state type must not be null.", nameof(codec));
            }

            if (codecsByKey.ContainsKey(codec.TypeKey))
            {
                throw new InvalidOperationException(
                    $"A save state codec is already registered for key '{codec.TypeKey}'.");
            }

            if (codecsByType.ContainsKey(codec.StateType))
            {
                throw new InvalidOperationException(
                    $"A save state codec is already registered for runtime type '{codec.StateType.FullName}'.");
            }

            var aliases = (legacyTypeIdentities ?? Array.Empty<string>())
                .Where(identity => !string.IsNullOrWhiteSpace(identity))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var alias in aliases)
            {
                if (codecsByLegacyIdentity.ContainsKey(alias))
                {
                    throw new InvalidOperationException(
                        $"A save state codec is already registered for legacy identity '{alias}'.");
                }
            }

            codecsByKey.Add(codec.TypeKey, codec);
            codecsByType.Add(codec.StateType, codec);
            foreach (var alias in aliases)
            {
                codecsByLegacyIdentity.Add(alias, codec);
            }
        }

        public bool TryResolve(string typeKey, out ISaveStateCodec codec)
        {
            codec = null;
            return !string.IsNullOrWhiteSpace(typeKey) && codecsByKey.TryGetValue(typeKey, out codec);
        }

        public bool TryResolve(Type stateType, out ISaveStateCodec codec)
        {
            codec = null;
            return stateType != null && codecsByType.TryGetValue(stateType, out codec);
        }

        public bool TryResolveLegacy(string legacyTypeIdentity, out ISaveStateCodec codec)
        {
            codec = null;
            return !string.IsNullOrWhiteSpace(legacyTypeIdentity) &&
                codecsByLegacyIdentity.TryGetValue(legacyTypeIdentity, out codec);
        }

        public ISaveStateCodec Resolve(string typeKey)
        {
            if (!TryResolve(typeKey, out var codec))
            {
                throw new InvalidDataException($"Unknown save state type key: '{typeKey}'.");
            }

            return codec;
        }

        public ISaveStateCodec Resolve(Type stateType)
        {
            if (!TryResolve(stateType, out var codec))
            {
                throw new InvalidDataException(
                    $"No save state codec is registered for runtime type '{stateType?.FullName ?? "<null>"}'.");
            }

            return codec;
        }

        public ISaveStateCodec ResolveLegacy(string legacyTypeIdentity)
        {
            if (!TryResolveLegacy(legacyTypeIdentity, out var codec))
            {
                throw new InvalidDataException(
                    $"Unknown legacy save state type identity: '{legacyTypeIdentity}'.");
            }

            return codec;
        }
    }
}
