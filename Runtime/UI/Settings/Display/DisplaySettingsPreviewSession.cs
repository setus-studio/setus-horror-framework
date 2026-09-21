using System;

namespace Setus.HorrorFramework.UI.Settings.Display
{
    public enum DisplayPreviewResult
    {
        None,
        TimedOut,
        ApplyFailed,
        Reverted,
        RevertFailed
    }

    public sealed class DisplaySettingsPreviewSession
    {
        private readonly RuntimeSettingsModel owner;
        private readonly IDisplaySettingsDevice device;
        private DisplaySettingsState previous;
        private DisplaySettingsState proposed;
        private float startedAt;
        private float expiresAt;
        private float nextRevertAttemptAt;

        public DisplaySettingsPreviewSession(RuntimeSettingsModel owner, IDisplaySettingsDevice device)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.device = device ?? throw new ArgumentNullException(nameof(device));
        }

        public bool IsActive => proposed != null;
        public DisplaySettingsState LastBaseline { get; private set; }
        public float SecondsRemaining(float now) => IsActive ? Math.Max(0f, expiresAt - now) : 0f;
        public bool CanConfirm(float now) => IsActive && now - startedAt >= 0.25f &&
            now < expiresAt && device.Matches(proposed);
        public string LastDiagnostic { get; private set; } = string.Empty;

        public bool Begin(DisplaySettingsState candidate, float now, float timeoutSeconds = 15f)
        {
            if (IsActive)
            {
                LastDiagnostic = "A display preview is already active.";
                return false;
            }
            if (candidate == null)
            {
                LastDiagnostic = "Display settings are missing.";
                return false;
            }
            if (!candidate.TryValidate(out var reason))
            {
                LastDiagnostic = reason;
                return false;
            }
            if (timeoutSeconds < 2f)
            {
                LastDiagnostic = "Display confirmation timeout is too short.";
                return false;
            }
            if (float.IsNaN(now) || float.IsInfinity(now))
            {
                LastDiagnostic = "Display preview time is invalid.";
                return false;
            }

            previous = device.CaptureCurrent();
            LastBaseline = previous;
            if (!device.TryApply(candidate, out var applyReason))
            {
                device.TryApply(previous, out _);
                previous = null;
                LastBaseline = null;
                LastDiagnostic = applyReason;
                return false;
            }

            proposed = candidate;
            startedAt = now;
            expiresAt = now + timeoutSeconds;
            nextRevertAttemptAt = float.NegativeInfinity;
            LastDiagnostic = string.Empty;
            return true;
        }

        public DisplayPreviewResult Tick(float now)
        {
            if (!IsActive) return DisplayPreviewResult.None;
            if (now >= expiresAt)
            {
                if (now < nextRevertAttemptAt) return DisplayPreviewResult.None;
                nextRevertAttemptAt = now + 1f;
                return Revert() == DisplayPreviewResult.Reverted
                    ? DisplayPreviewResult.TimedOut : DisplayPreviewResult.RevertFailed;
            }
            if (now - startedAt >= 2f && !device.Matches(proposed))
            {
                LastDiagnostic = "The display did not accept the selected mode or resolution.";
                return Revert() == DisplayPreviewResult.Reverted
                    ? DisplayPreviewResult.ApplyFailed : DisplayPreviewResult.RevertFailed;
            }
            return DisplayPreviewResult.None;
        }

        public bool Confirm(float now)
        {
            if (!CanConfirm(now))
            {
                LastDiagnostic = "Wait until the display change has taken effect before confirming.";
                return false;
            }

            owner.SetDisplaySettings(proposed);
            owner.Flush();
            previous = null;
            proposed = null;
            LastBaseline = null;
            LastDiagnostic = owner.PersistenceDiagnostic;
            return true;
        }

        public DisplayPreviewResult Revert()
        {
            if (!IsActive) return DisplayPreviewResult.None;
            var baseline = previous;
            if (device.TryApply(baseline, out var reason))
            {
                previous = null;
                proposed = null;
                LastDiagnostic = string.Empty;
                return DisplayPreviewResult.Reverted;
            }

            LastDiagnostic = reason;
            return DisplayPreviewResult.RevertFailed;
        }
    }
}
