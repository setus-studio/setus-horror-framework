using Setus.HorrorFramework.Editor.Windows;
using UnityEditor;

namespace Setus.HorrorFramework.Editor.ContentPipeline
{
    public static class ContentPipelineMenuCommands
    {
        [MenuItem("Setus/Horror Framework/Content/Validate Game Content")]
        public static void ValidateDefaultGameContent()
        {
            var result = ContentAssetValidator.Validate(StandaloneGameContentTemplate.DefaultGameRoot);
            ContentAssetValidator.LogResult(StandaloneGameContentTemplate.DefaultGameRoot, result);
        }

        [MenuItem("Setus/Horror Framework/Content/Open Asset Validation Checklist")]
        public static void OpenValidationChecklist()
        {
            ContentPipelineValidationWindow.Open();
        }
    }
}
