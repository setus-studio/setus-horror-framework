using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Tension
{
    [Serializable]
    public sealed class TensionRuntimeState
    {
        [SerializeField] private TensionSourceState[] sources;

        public TensionRuntimeState(IEnumerable<TensionSourceState> sources = null)
        {
            this.sources = (sources ?? Enumerable.Empty<TensionSourceState>())
                .OrderBy(source => source.SourceId, StringComparer.Ordinal)
                .ToArray();
        }

        // Schema 3 slots written before this field existed contain {}. They represent no
        // durable tension sources rather than an invalid partial restore state.
        public IReadOnlyList<TensionSourceState> Sources => sources ?? Array.Empty<TensionSourceState>();
    }

    [Serializable]
    public sealed class TensionSourceState
    {
        [SerializeField] private string sourceId;
        [SerializeField] private float intensity;

        public TensionSourceState(string sourceId, float intensity)
        {
            this.sourceId = sourceId ?? string.Empty;
            this.intensity = intensity;
        }

        public string SourceId => sourceId;
        public float Intensity => intensity;
    }
}
