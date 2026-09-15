using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Setus.HorrorFramework.Atmosphere.Scares
{
    [Serializable]
    public sealed class ScareRuntimeState
    {
        [SerializeField] private ScareStateRecord[] scares;

        public ScareRuntimeState(IEnumerable<ScareStateRecord> scares = null)
        {
            this.scares = (scares ?? Enumerable.Empty<ScareStateRecord>())
                .OrderBy(record => record.ScareId, StringComparer.Ordinal)
                .ToArray();
        }

        // Schema 3 slots written before this field existed contain {}. They represent no
        // captured scare records and restore through the scenario's default Armed state.
        public IReadOnlyList<ScareStateRecord> Scares => scares ?? Array.Empty<ScareStateRecord>();
    }

    [Serializable]
    public sealed class ScareStateRecord
    {
        [SerializeField] private string scareId;
        [SerializeField] private ScareLifecycleState state;

        public ScareStateRecord(string scareId, ScareLifecycleState state)
        {
            this.scareId = scareId ?? string.Empty;
            this.state = state;
        }

        public string ScareId => scareId;
        public ScareLifecycleState State => state;
    }
}
