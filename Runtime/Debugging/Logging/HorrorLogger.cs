using UnityEngine;

namespace Setus.HorrorFramework.Debugging.Logging
{
    public sealed class HorrorLogger : IHorrorLogger
    {
        private HorrorLogCategory enabledCategories;

        public HorrorLogger(HorrorLogCategory enabledCategories)
        {
            this.enabledCategories = enabledCategories;
        }

        public bool IsEnabled(HorrorLogCategory category)
        {
            return category != HorrorLogCategory.None && (enabledCategories & category) != 0;
        }

        public void SetEnabledCategories(HorrorLogCategory categories)
        {
            enabledCategories = categories;
        }

        public void Log(HorrorLogCategory category, string message)
        {
            if (IsEnabled(category))
            {
                Debug.Log(Format(category, message));
            }
        }

        public void Warning(HorrorLogCategory category, string message)
        {
            if (IsEnabled(category))
            {
                Debug.LogWarning(Format(category, message));
            }
        }

        public void Error(HorrorLogCategory category, string message)
        {
            if (IsEnabled(category))
            {
                Debug.LogError(Format(category, message));
            }
        }

        private static string Format(HorrorLogCategory category, string message)
        {
            return $"[Setus:Horror:{category}] {message}";
        }
    }
}
