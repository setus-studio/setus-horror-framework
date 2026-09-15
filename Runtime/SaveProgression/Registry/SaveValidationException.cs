using System;

namespace Setus.HorrorFramework.SaveProgression.Registry
{
    public sealed class SaveValidationException : Exception
    {
        public SaveValidationException(string message, SaveRestoreReport report)
            : base(message)
        {
            Report = report;
        }

        public SaveRestoreReport Report { get; }
    }
}
