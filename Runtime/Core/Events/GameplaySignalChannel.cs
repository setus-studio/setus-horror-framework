using Setus.HorrorFramework.Core.Services;
using UnityEngine;

namespace Setus.HorrorFramework.Core.Events
{
    [CreateAssetMenu(
        fileName = "GameplaySignalChannel",
        menuName = "Setus/Horror Framework/Gameplay Signal Channel")]
    public sealed class GameplaySignalChannel : ScriptableObject
    {
        [SerializeField] private string signalId;
        [SerializeField, TextArea] private string description;

        public string SignalId => signalId;
        public string Description => description;

        public void Raise()
        {
            Raise(HorrorGameContext.Active?.Events);
        }

        public void Raise(IGameplayEventBus eventBus)
        {
            if (eventBus == null)
            {
                return;
            }

            eventBus.Publish(new GameplaySignalRaised(signalId, this));
        }
    }
}
