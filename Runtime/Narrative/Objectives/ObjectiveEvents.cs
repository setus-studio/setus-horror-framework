using Setus.HorrorFramework.Core.Events;

namespace Setus.HorrorFramework.Narrative.Objectives
{
    public readonly struct ObjectiveStateChanged : IGameplayEvent
    {
        public ObjectiveStateChanged(ObjectiveRuntimeState state)
        {
            State = state;
        }

        public ObjectiveRuntimeState State { get; }
    }

    public readonly struct ObjectiveCompleted : IGameplayEvent
    {
        public ObjectiveCompleted(string scenarioId, string objectiveId)
        {
            ScenarioId = scenarioId;
            ObjectiveId = objectiveId;
        }

        public string ScenarioId { get; }
        public string ObjectiveId { get; }
    }
}
