using System;

namespace Setus.HorrorFramework.Core.Services
{
    public abstract class RuntimeStateOwnerBase<TState> : IRuntimeStateOwner<TState>
    {
        protected RuntimeStateOwnerBase(string stateKey)
        {
            if (string.IsNullOrWhiteSpace(stateKey))
            {
                throw new ArgumentException("Runtime state owner key must not be empty.", nameof(stateKey));
            }

            StateKey = stateKey;
        }

        public string StateKey { get; }
        public Type StateType => typeof(TState);

        public abstract TState CaptureState();
        public abstract void RestoreState(TState state);

        object IRuntimeStateOwner.CaptureState()
        {
            return CaptureState();
        }

        void IRuntimeStateOwner.RestoreState(object state)
        {
            if (!(state is TState typedState))
            {
                throw new ArgumentException(
                    $"State for '{StateKey}' must be {typeof(TState).FullName}.",
                    nameof(state));
            }

            RestoreState(typedState);
        }
    }
}
