using System;

namespace Setus.HorrorFramework.Core.Services
{
    public readonly struct RestoreStateValidationResult
    {
        private RestoreStateValidationResult(bool isValid, string reason)
        {
            IsValid = isValid;
            Reason = reason ?? string.Empty;
        }

        public bool IsValid { get; }
        public string Reason { get; }

        public static RestoreStateValidationResult Success =>
            new RestoreStateValidationResult(true, string.Empty);

        public static RestoreStateValidationResult Invalid(string reason)
        {
            return new RestoreStateValidationResult(false, reason);
        }
    }

    public interface IRestoreStateValidator
    {
        // Preflight validation must be deterministic and side-effect free: no state mutation,
        // gameplay events, or object lifecycle changes are allowed here.
        RestoreStateValidationResult ValidateRestoreState(object state);
    }

    public interface IRuntimeRollbackOwner
    {
        // Capture without mutation. The returned action restores exact live state without
        // load normalization or gameplay events; it is used only by transaction rollback.
        Action CaptureRollbackAction();
    }

    public interface IRuntimeStateOwner
    {
        string StateKey { get; }
        Type StateType { get; }
        object CaptureState();
        void RestoreState(object state);
    }

    public interface IRuntimeStateOwner<TState> : IRuntimeStateOwner
    {
        new TState CaptureState();
        void RestoreState(TState state);
    }
}
