using Setus.HorrorFramework.Core.Events;

namespace Setus.HorrorFramework.Inventory.Runtime
{
    public readonly struct InventoryChanged : IGameplayEvent
    {
        public InventoryChanged(InventoryState state)
        {
            State = state;
        }

        public InventoryState State { get; }
    }
}
