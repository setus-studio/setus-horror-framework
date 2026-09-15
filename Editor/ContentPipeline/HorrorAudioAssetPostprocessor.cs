using UnityEditor;

namespace Setus.HorrorFramework.Editor.ContentPipeline
{
    public sealed class HorrorAudioAssetPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (assetImporter is AudioImporter importer &&
                HorrorAudioImportRules.TryResolve(assetPath, out var rule))
            {
                rule.Apply(importer);
            }
        }
    }
}
